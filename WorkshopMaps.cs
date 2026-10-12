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
                        found.Add(GameMap.Workshop(id, Path.GetFileNameWithoutExtension(vpk)));
                }
            }
            catch (IOException) { return new GameMap[0]; }
            catch (UnauthorizedAccessException) { return new GameMap[0]; }

            return found
                .OrderBy(m => m.WorkshopFile, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}
