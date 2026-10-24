using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CS2PracticeHost
{
    /// <summary>
    /// 방장이 받아 둔 창작마당 맵을 찾는다. 기본 맵과 같은 방식이다 — 폴더에 vpk가 있으면 있는 것으로 본다.
    /// 앱이 받아 주지는 않는다. 받는 것은 Steam에서 구독하는 일이고 여기서는 이미 받은 것만 보여 준다.
    /// </summary>
    internal static class WorkshopMaps
    {
        /// <summary>
        /// Steam은 vpk를 구독 번호로 저장한다(`3447707473_dir.vpk`, 큰 맵은 `_000`·`_001`로 쪼개진다).
        /// 맵 이름은 파일 이름이 아니라 vpk 목차 안에 있다 — `maps/de_dust2_n.vpk` 꼴로.
        /// `map_workshop`은 그 이름까지 받아야 맵을 연다(번호만 주거나 번호를 이름 자리에 넣으면
        /// 「필요한 맵이 클라이언트에 없습니다」). 구독 하나에 맵이 둘 든 것도 있어 맵마다 하나씩 센다.
        /// </summary>
        public static GameMap[] Installed()
        {
            string root = Cs2Paths.WorkshopDir;
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return new GameMap[0];

            var found = new List<GameMap>();
            try
            {
                foreach (string dir in Directory.GetDirectories(root))
                {
                    string id = Path.GetFileName(dir);

                    // 폴더 이름이 구독 번호다. 숫자가 아니면 Steam이 만든 게 아니라 건너뛴다
                    long ignored;
                    if (!long.TryParse(id, out ignored)) continue;

                    // 이름을 못 읽은 맵은 띄우지 않는다 — 고를 수 있는데 안 열리는 게 제일 나쁘다
                    string[] names = PlayableOf(MapNamesIn(TreeFile(dir, id)),
                                                PublishData(dir, "source_folder"), PublishData(dir, "title"));
                    foreach (string name in names)
                        found.Add(GameMap.Workshop(id, name, names.Length > 1));
                }
            }
            catch (IOException) { return new GameMap[0]; }
            catch (UnauthorizedAccessException) { return new GameMap[0]; }

            return found.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        /// <summary>
        /// `maps/` 바로 아래 vpk가 다 고를 맵은 아니다. 하늘 상자(`de_bikinibottom_skybox`)나
        /// 본 맵이 끌어다 쓰는 작은 맵(Shortdust의 `dust1`)도 같은 자리에 있다. 그걸 고르면 빈 하늘에 선다.
        /// `_skybox`는 이름으로 빼고, 그래도 여럿이면 제작자가 올린 폴더 이름과 맞는 쪽만 남긴다
        /// (`shortdust` → `de_shortdust`). 폴더 이름이 어느 쪽과도 안 맞으면 올린 제목과 같은 이름만 남긴다
        /// (`rats_addon` 안의 `de_rats_room`과 제작자가 남긴 `test` — `test`는 안 열린다).
        /// 그것도 없으면 진짜 맵이 여럿 든 묶음으로 보고 다 둔다.
        /// </summary>
        internal static string[] PlayableOf(string[] names, string sourceFolder, string title = null)
        {
            // Labbo는 하늘 상자를 `labbo_3dskybox`로 싣는다
            string[] maps = names.Where(n => !n.EndsWith("_skybox", StringComparison.OrdinalIgnoreCase) &&
                                             !n.EndsWith("_3dskybox", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (maps.Length <= 1) return maps;

            string[] main = NamedLike(maps, sourceFolder);
            if (main.Length > 0) return main;

            // 제목도 폴더 이름처럼 접두사를 붙여 맵 이름이 된다 — 「Labbo」 → `de_labbo`
            string[] titled = NamedLike(maps, title);
            return titled.Length > 0 ? titled : maps;
        }

        /// <summary>`이름` 그대로이거나 `접두사_이름`인 맵 (`shortdust` → `de_shortdust`).</summary>
        private static string[] NamedLike(string[] maps, string name)
        {
            if (string.IsNullOrEmpty(name)) return new string[0];
            name = name.Trim();
            return maps.Where(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase) ||
                                   n.EndsWith("_" + name, StringComparison.OrdinalIgnoreCase))
                       .ToArray();
        }

        /// <summary>
        /// Steam이 받은 폴더에 같이 두는 `publish_data.txt`의 한 칸.
        /// `source_folder`는 제작자의 작업 폴더 이름, `title`은 올린 제목이다.
        /// </summary>
        private static string PublishData(string dir, string key)
        {
            try
            {
                foreach (string line in File.ReadLines(Path.Combine(dir, "publish_data.txt")))
                {
                    string[] parts = line.Split('"');   // 	"source_folder"		"shortdust"
                    if (parts.Length >= 4 && parts[1] == key) return parts[3];
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return null;
        }

        /// <summary>목차가 든 파일. 쪼개진 맵은 `_dir`에만 목차가 있고, 안 쪼개진 맵은 번호 그대로다.</summary>
        private static string TreeFile(string dir, string id)
        {
            string split = Path.Combine(dir, id + "_dir.vpk");
            return File.Exists(split) ? split : Path.Combine(dir, id + ".vpk");
        }

        private const uint VpkSignature = 0x55AA1234;

        /// <summary>
        /// vpk 목차에서 `maps` 바로 아래의 `.vpk` 이름들. 그게 게임이 여는 맵이다.
        /// `maps/maps/prefabs/...`의 하늘 상자 같은 것은 바로 아래가 아니라 안 걸린다.
        /// </summary>
        internal static string[] MapNamesIn(string vpk)
        {
            var names = new List<string>();
            try
            {
                using (var r = new BinaryReader(File.OpenRead(vpk)))
                {
                    if (r.ReadUInt32() != VpkSignature) return new string[0];
                    uint version = r.ReadUInt32();
                    r.ReadUInt32();                         // 목차 크기
                    if (version == 2) r.ReadBytes(16);      // v2는 머리에 크기 넷이 더 있다
                    else if (version != 1) return new string[0];

                    // 목차는 확장자 → 경로 → 파일 이름 세 겹이고 각 겹이 빈 문자열로 끝난다
                    string ext;
                    while ((ext = ReadText(r)).Length > 0)
                    {
                        string path;
                        while ((path = ReadText(r)).Length > 0)
                        {
                            string file;
                            while ((file = ReadText(r)).Length > 0)
                            {
                                r.ReadBytes(4);                     // CRC
                                ushort preload = r.ReadUInt16();
                                r.ReadBytes(12);                    // 조각 번호·위치·길이·끝 표시
                                r.ReadBytes(preload);
                                if (ext == "vpk" && path == "maps") names.Add(file);
                            }
                        }
                    }
                }
            }
            catch (IOException) { return new string[0]; }      // 없는 파일, 중간에 끊긴 파일(EndOfStream)
            catch (UnauthorizedAccessException) { return new string[0]; }

            return names.ToArray();
        }

        private static string ReadText(BinaryReader r)
        {
            var bytes = new List<byte>();
            byte b;
            while ((b = r.ReadByte()) != 0) bytes.Add(b);
            return System.Text.Encoding.UTF8.GetString(bytes.ToArray());
        }

        /// <summary>
        /// 이 모드에 붙일 창작마당 맵. 제작자가 단 태그로 가른다 — 윙맨용 맵은 윙맨에만 뜬다.
        /// 아직 못 물어봤거나 태그가 없는 맵은 Classic으로 본다. 숨기지는 않는다 —
        /// 인터넷이 없어도 캐주얼·경쟁에서는 고를 수 있어야 한다.
        /// </summary>
        public static GameMap[] For(GameMode mode)
        {
            if (mode == null || mode.WorkshopTag == null) return new GameMap[0];

            return Installed().Where(m => Fits(m, mode.WorkshopTag)).ToArray();
        }

        /// <summary>제작자가 모드를 가리킬 때 쓰는 태그. 이 중 하나도 없으면 모드를 안 밝힌 맵이다.</summary>
        private static readonly string[] ModeTags =
        {
            "Classic", "Wingman", "Armsrace", "Deathmatch", "Demolition",
        };

        private static bool Fits(GameMap map, string tag)
        {
            WorkshopItem known = WorkshopInfo.Of(map.WorkshopId);
            if (known == null) return tag == "Classic";       // 아직 못 물어봤다
            if (known.HasTag(tag)) return true;

            // 에임·연습·서핑 맵은 Custom·Training만 달고 모드 태그를 안 단다.
            // 그런 맵을 아무 데도 안 띄우면 받아 둔 게 통째로 사라진다 — Classic으로 친다.
            return !ModeTags.Any(known.HasTag) && tag == "Classic";
        }

        /// <summary>받아 둔 맵의 구독 번호. Steam에 제목·태그를 물어볼 때 쓴다.</summary>
        public static string[] InstalledIds()
        {
            return Installed().Select(m => m.WorkshopId).Distinct().ToArray();
        }
    }
}
