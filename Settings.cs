using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace CS2PracticeHost
{
    [DataContract]
    internal sealed class Settings
    {
        [DataMember(Name = "mode")] public string Mode { get; set; }
        [DataMember(Name = "map")] public string Map { get; set; }
        [DataMember(Name = "bots")] public int Bots { get; set; }
        [DataMember(Name = "level")] public string Level { get; set; }
        [DataMember(Name = "team")] public string Team { get; set; }

        /// <summary>자동 탐지가 실패해 사용자가 직접 고른 CS2 설치 폴더.</summary>
        [DataMember(Name = "installDir")] public string InstallDir { get; set; }

        /// <summary>"ko" | "en". 비어 있으면 윈도우 표시 언어를 따른다.</summary>
        [DataMember(Name = "lang")] public string Lang { get; set; }

        /// <summary>별 찍은 맵 아이디(`de_dust2`, `workshop/번호/이름`). 모드와 무관 — 맵 단위다.</summary>
        [DataMember(Name = "favorites")] public string[] Favorites { get; set; }

        /// <summary>
        /// 사용자가 단 맵 종류, `아이디=종류` 꼴. 종류는 MapKind의 값. Steam 정보에도 맵 파일에도
        /// 폭탄 지점 수가 읽을 수 있는 꼴로 없어서 사람이 단다(2026-10-24 디렉터 결정).
        /// </summary>
        [DataMember(Name = "mapKinds")] public string[] MapKinds { get; set; }

        /// <summary>
        /// 사용자가 맵에 단 메모, `아이디=메모` 꼴. 썸네일만으로는 맵을 찾기 힘들다고 해서 넣었다
        /// (2026-11-28 디렉터). 한 줄짜리라 줄바꿈은 저장 전에 공백으로 바꾼다.
        /// </summary>
        [DataMember(Name = "mapNotes")] public string[] MapNotes { get; set; }

        public const int NoteMaxLength = 60;

        public bool IsFavorite(string mapId)
        {
            return Favorites != null && Array.IndexOf(Favorites, mapId) >= 0;
        }

        public void ToggleFavorite(string mapId)
        {
            var list = new System.Collections.Generic.List<string>(Favorites ?? new string[0]);
            if (!list.Remove(mapId)) list.Add(mapId);
            Favorites = list.ToArray();
        }

        /// <summary>사용자가 단 종류. 안 달았으면 null.</summary>
        public string KindOf(string mapId)
        {
            if (MapKinds == null) return null;
            string prefix = mapId + "=";
            foreach (string row in MapKinds)
                if (row != null && row.StartsWith(prefix, StringComparison.Ordinal)) return row.Substring(prefix.Length);
            return null;
        }

        /// <summary>kind가 null이면 표시를 지운다.</summary>
        public void SetKind(string mapId, string kind)
        {
            var list = new System.Collections.Generic.List<string>(MapKinds ?? new string[0]);
            list.RemoveAll(row => row != null && row.StartsWith(mapId + "=", StringComparison.Ordinal));
            if (kind != null) list.Add(mapId + "=" + kind);
            MapKinds = list.ToArray();
        }

        /// <summary>사용자가 단 메모. 없으면 null.</summary>
        public string NoteOf(string mapId)
        {
            if (MapNotes == null) return null;
            string prefix = mapId + "=";
            foreach (string row in MapNotes)
                if (row != null && row.StartsWith(prefix, StringComparison.Ordinal)) return row.Substring(prefix.Length);
            return null;
        }

        /// <summary>note가 비었으면 메모를 지운다. 줄바꿈은 공백으로, 길면 자른다.</summary>
        public void SetNote(string mapId, string note)
        {
            note = CleanNote(note);
            var list = new System.Collections.Generic.List<string>(MapNotes ?? new string[0]);
            list.RemoveAll(row => row != null && row.StartsWith(mapId + "=", StringComparison.Ordinal));
            if (note != null) list.Add(mapId + "=" + note);
            MapNotes = list.ToArray();
        }

        public static string CleanNote(string note)
        {
            if (note == null) return null;
            note = System.Text.RegularExpressions.Regex.Replace(note, @"\s+", " ").Trim();
            if (note.Length > NoteMaxLength) note = note.Substring(0, NoteMaxLength).TrimEnd();
            return note.Length == 0 ? null : note;
        }

        /// <summary>
        /// 별·종류·메모만 파일로 내보낸다. 다른 PC로 옮길 때 쓴다(2026-11-28 디렉터).
        /// 설치 폴더·언어 같은 그 PC 몫은 넣지 않는다 — 옮겨 가면 틀린 값이 된다.
        /// </summary>
        public void ExportMarks(string path)
        {
            var marks = new MarksFile
            {
                App = MarksFile.AppName, Version = 1,
                Favorites = Favorites ?? new string[0], MapKinds = MapKinds ?? new string[0], MapNotes = MapNotes ?? new string[0],
            };
            using (var stream = File.Create(path))
                MarksSerializer.WriteObject(stream, marks);
        }

        /// <summary>
        /// 내보낸 파일을 지금 것에 합친다. 이 PC에만 있는 표시는 그대로 두고, 같은 맵은 가져온 쪽으로 바꾼다.
        /// 돌려주는 값은 가져온 맵 수(별·종류·메모 중 하나라도 있는 맵). 우리 파일이 아니면 예외.
        /// </summary>
        public int ImportMarks(string path)
        {
            MarksFile marks;
            using (var stream = File.OpenRead(path))
                marks = (MarksFile)MarksSerializer.ReadObject(stream);
            if (marks == null || marks.App != MarksFile.AppName) throw new InvalidDataException();

            var touched = new System.Collections.Generic.HashSet<string>();
            foreach (string id in marks.Favorites ?? new string[0])
            {
                if (string.IsNullOrEmpty(id)) continue;
                if (!IsFavorite(id)) ToggleFavorite(id);
                touched.Add(id);
            }
            foreach (string row in marks.MapKinds ?? new string[0])
            {
                string id, value;
                if (!SplitRow(row, out id, out value)) continue;
                SetKind(id, value);
                touched.Add(id);
            }
            foreach (string row in marks.MapNotes ?? new string[0])
            {
                string id, value;
                if (!SplitRow(row, out id, out value)) continue;
                SetNote(id, value);
                touched.Add(id);
            }
            return touched.Count;
        }

        /// <summary>`아이디=값`을 가른다. 아이디에는 `=`가 없고 값에는 있을 수 있어서 첫 `=`에서 자른다.</summary>
        private static bool SplitRow(string row, out string id, out string value)
        {
            id = value = null;
            int at = row == null ? -1 : row.IndexOf('=');
            if (at <= 0 || at == row.Length - 1) return false;
            id = row.Substring(0, at);
            value = row.Substring(at + 1);
            return true;
        }

        private static readonly DataContractJsonSerializer MarksSerializer =
            new DataContractJsonSerializer(typeof(MarksFile));

        private static string FilePath
        {
            get
            {
                return Path.Combine(AppFolder.Dir, "settings.json");
            }
        }

        public static Settings Load()
        {
            if (!File.Exists(FilePath)) return new Settings();
            try
            {
                using (var stream = File.OpenRead(FilePath))
                    return (Settings)Serializer.ReadObject(stream) ?? new Settings();
            }
            catch (Exception)
            {
                // 깨진 파일은 다음 저장에서 덮인다. 즐겨찾기·종류 표시처럼 사람이 쌓은 것이 말없이
                // 사라지지 않게, 기본값으로 시작하기 전에 따로 떼어 둔다(껐다 켜니 표시가 사라졌다 — B 실기)
                try { File.Copy(FilePath, BrokenPath(), true); } catch (Exception) { }
                return new Settings();
            }
        }

        private static string BrokenPath()
        {
            return Path.Combine(Path.GetDirectoryName(FilePath),
                                "settings.broken-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json");
        }

        public void Save()
        {
            try
            {
                // 비우고 쓰는 사이에 끊기면(끄기·강제 종료) 파일이 깨진다 — 다 쓴 임시 파일로 바꿔치기한다
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                string temp = FilePath + ".tmp";
                using (var stream = File.Create(temp))
                    Serializer.WriteObject(stream, this);
                if (File.Exists(FilePath)) File.Replace(temp, FilePath, null);
                else File.Move(temp, FilePath);
            }
            catch (Exception)
            {
                // 설정 저장 실패로 앱이 멈출 이유는 없다
            }
        }

        private static readonly DataContractJsonSerializer Serializer =
            new DataContractJsonSerializer(typeof(Settings));
    }

    /// <summary>내보내기 파일. `app`으로 우리 파일인지 가린다 — 엉뚱한 JSON을 골라도 설정을 건드리지 않게.</summary>
    [DataContract]
    internal sealed class MarksFile
    {
        public const string AppName = "CS2PracticeHost.marks";

        [DataMember(Name = "app", Order = 0)] public string App { get; set; }
        [DataMember(Name = "version", Order = 1)] public int Version { get; set; }
        [DataMember(Name = "favorites", Order = 2)] public string[] Favorites { get; set; }
        [DataMember(Name = "mapKinds", Order = 3)] public string[] MapKinds { get; set; }
        [DataMember(Name = "mapNotes", Order = 4)] public string[] MapNotes { get; set; }
    }
}
