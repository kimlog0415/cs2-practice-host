using System;
using System.IO;
using System.Linq;
using System.Text;

namespace CS2PracticeHost
{
    /// <summary>CS2 cfg 폴더에 설정 파일을 쓴다.</summary>
    internal static class CfgWriter
    {
        public const string Marker = "// CS2 Practice Host가 만든 파일";

        /// <summary>이름을 바꾸기 전 마커. 이걸 안 알아보면 옛 파일을 남의 것으로 여겨 갱신을 멈춘다.</summary>
        private const string OldMarker = "// CS2 방장 도우미가 만든 파일";

        // 사용자의 기존 cfg와 겹치지 않도록 접두사를 붙인다
        public const string ChangeCfg = "cs2host_change";
        public const string BindCfg = "cs2host_bind";

        /// <summary>F9가 쓰던 cfg. 키를 없애면서 안 쓰게 됐고, 남아 있으면 낡은 설정이 된다.</summary>
        private const string RetiredHostCfg = "cs2host_host";

        private static readonly UTF8Encoding NoBom = new UTF8Encoding(false);

        public static void WriteMatchCfgs(GameMode mode, GameMap map)
        {
            // 접속한 친구들을 유지한 채 맵만 바꾼다. 창작마당은 명령이 달라 맵이 들고 있다
            Write(ChangeCfg, new[]
            {
                "game_type " + mode.Type, "game_mode " + mode.Mode, map.ChangeCommand
            });

            Delete(RetiredHostCfg);
        }

        /// <summary>
        /// F10 바인드. 실행 인자 +exec 로 불러서 사용자가 콘솔에 칠 필요가 없게 한다.
        /// F9는 없앴다 — 서버를 새로 여는 키라 친구가 전부 끊기는데 F10과 생김새가 비슷해
        /// 게임 중에 헷갈린다. 바인드는 CS2에 저장돼 남으므로 예전에 걸린 것을 직접 풀어 준다.
        /// </summary>
        public static void WriteBindCfg()
        {
            Write(BindCfg, new[]
            {
                "unbind F9",
                "bind F10 \"exec " + ChangeCfg + "\"",
            });
        }

        /// <summary>봇 설정을 gamemode_*_server.cfg에 넣는다. 맵을 열 때마다 CS2가 자동으로 읽는다.</summary>
        public static void WriteBotCfgs(int botCountIndex, int difficulty, string team)
        {
            string[] lines = null;
            if (botCountIndex > 0)
            {
                int count = botCountIndex - 1;
                lines = new[]
                {
                    "bot_quota_mode normal",
                    "bot_quota " + count,
                    "bot_difficulty " + difficulty,
                    "bot_join_team " + team,
                };
                if (count == 0) lines = lines.Concat(new[] { "bot_kick" }).ToArray();
            }

            foreach (string name in GameData.ServerCfgs)
            {
                string file = "gamemode_" + name + "_server";
                if (!IsOursOrMissing(file)) continue;   // 사용자가 직접 만든 파일은 건드리지 않는다

                if (lines != null) Write(file, lines);
                else Delete(file);
            }
        }

        private static string PathOf(string name)
        {
            return Path.Combine(Cs2Paths.CfgDir, name + ".cfg");
        }

        private static bool IsOursOrMissing(string name)
        {
            string path = PathOf(name);
            if (!File.Exists(path)) return true;
            try
            {
                using (var reader = new StreamReader(path, NoBom))
                {
                    string first = reader.ReadLine();
                    return first == Marker || first == OldMarker;
                }
            }
            catch (IOException) { return false; }
        }

        private static void Write(string name, string[] lines)
        {
            string body = Marker + "\r\n" + string.Join("\r\n", lines) + "\r\n";
            try { File.WriteAllText(PathOf(name), body, NoBom); }
            catch (Exception) { /* cfg를 못 써도 앱이 죽을 이유는 없다 */ }
        }

        private static void Delete(string name)
        {
            try { if (File.Exists(PathOf(name))) File.Delete(PathOf(name)); }
            catch (Exception) { }
        }
    }
}
