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

                    foreach (string vpk in Directory.GetFiles(dir, "*.vpk"))
                    {
                        string name = MapNameOf(Path.GetFileNameWithoutExtension(vpk));
                        if (name != null) found.Add(GameMap.Workshop(id, name));
                    }
                }
            }
            catch (IOException) { return new GameMap[0]; }
            catch (UnauthorizedAccessException) { return new GameMap[0]; }

            return found
                .OrderBy(m => m.WorkshopFile, StringComparer.OrdinalIgnoreCase)
                .ToArray();
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

        /// <summary>
        /// 큰 vpk는 쪼개져 저장된다 — 목록을 담은 `이름_dir.vpk` 하나와 데이터가 든
        /// `이름_000.vpk`·`_001.vpk`... 가 따로 있다. 게임이 쓰는 이름은 `_dir`을 뗀 쪽이고
        /// 번호 붙은 것은 그 속 재료라 맵으로 세면 안 된다. 안 세면 맵 하나가 여러 개로 보인다.
        /// </summary>
        private static string MapNameOf(string fileName)
        {
            if (fileName.EndsWith("_dir", StringComparison.OrdinalIgnoreCase))
                return fileName.Substring(0, fileName.Length - 4);

            if (IsNumberedChunk(fileName)) return null;

            return fileName;   // 안 쪼개진 vpk는 그대로가 맵 이름이다
        }

        private static bool IsNumberedChunk(string fileName)
        {
            int cut = fileName.LastIndexOf('_');
            if (cut < 0 || cut == fileName.Length - 1) return false;

            string tail = fileName.Substring(cut + 1);
            return tail.Length == 3 && tail.All(char.IsDigit);
        }
    }
}
