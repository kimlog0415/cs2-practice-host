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

        private static string FilePath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CS2PracticeHost");
                return Path.Combine(dir, "settings.json");
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
}
