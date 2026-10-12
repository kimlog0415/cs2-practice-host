using System.IO;
using System.Linq;

namespace CS2PracticeHost
{
    /// <summary>
    /// 모드마다 게임에 넘기는 번호와 맵·봇 설정 파일이 맞는지.
    /// 번호가 틀리면 앱은 멀쩡히 켜지는데 다른 모드가 열린다 — 제일 알아채기 어려운 종류라 표로 박아 둔다.
    /// </summary>
    internal static class ModeChecks
    {
        public static void Run()
        {
            Numbers();
            BotCfgNames();
            WingmanAndRushMaps();
            BotsLockedWhereModeDecides();
            SavedByKeyNotName();
            CfgFilesWritten();
        }

        /// <summary>
        /// F10이 안 먹는다는 보고가 있어 실제로 써지는 내용을 찍어 본다.
        /// 바인드는 cs2host_bind.cfg에, 맵 바꾸기는 cs2host_change.cfg에 들어가고
        /// 켤 때 +exec cs2host_bind 로 불린다.
        /// </summary>
        private static void CfgFilesWritten()
        {
            Report.Head("F10 관련 cfg가 어떻게 써지나");

            Fake.Install();
            Strings.Init("ko");
            Fake.WriteSettings("{\"mode\":\"casual\",\"map\":\"de_dust2\",\"bots\":3," +
                               "\"level\":\"1\",\"team\":\"any\",\"installDir\":null,\"lang\":\"ko\"}");
            using (new MainForm(Settings.Load())) { }

            Dump("cs2host_bind");
            Dump("cs2host_change");
            Dump("gamemode_casual_server");

            Report.Is("바인드 파일이 있나", File.Exists(CfgPath("cs2host_bind")), true);
            Report.Is("맵 바꾸기 파일이 있나", File.Exists(CfgPath("cs2host_change")), true);
            Report.Is("기본 맵은 changelevel로",
                      File.ReadAllLines(CfgPath("cs2host_change")).Last(), "changelevel de_dust2");

            // 창작마당 맵을 골랐을 때는 번호로 바꾼다
            Fake.WorkshopMaps("777");
            Fake.WorkshopInfoCache("777|Aim Practice|Classic,Map");
            Fake.WriteSettings("{\"mode\":\"casual\",\"map\":\"workshop/777\",\"bots\":3," +
                               "\"level\":\"1\",\"team\":\"any\",\"installDir\":null,\"lang\":\"ko\"}");
            using (new MainForm(Settings.Load())) { }
            Report.Is("창작마당은 번호로",
                      File.ReadAllLines(CfgPath("cs2host_change")).Last(), "map_workshop 777");

            Fake.ForgetWorkshopInfo();
            Fake.ClearWorkshopMaps();   // 뒤 확인이 자기 맵만 보게 한다
        }

        private static string CfgPath(string name)
        {
            return System.IO.Path.Combine(Cs2Paths.CfgDir, name + ".cfg");
        }

        private static void Dump(string name)
        {
            string path = CfgPath(name);
            if (!File.Exists(path)) { Report.Note(name + ".cfg — 없음"); return; }
            Report.Note(name + ".cfg:");
            foreach (string line in File.ReadAllLines(path)) Report.Note("    " + line);
        }

        private static void Numbers()
        {
            Report.Head("모드마다 게임에 넘기는 번호");

            Expect("casual", 0, 0);
            Expect("competitive", 0, 1);
            Expect("wingman", 0, 2);
            Expect("rush", 0, 6);
            Expect("deathmatch", 1, 2);
            Expect("armsrace", 1, 0);
            Expect("retake", 0, 5);
        }

        private static void Expect(string key, int type, int mode)
        {
            GameMode m = GameData.Modes.FirstOrDefault(x => x.Key == key);
            if (m == null) { Report.Fail("그런 모드가 없다: " + key); return; }
            Report.Is(m.Ko + " (game_type/game_mode)", m.Type + "/" + m.Mode, type + "/" + mode);
        }

        /// <summary>
        /// 봇 설정은 gamemode_&lt;이름&gt;_server.cfg 에 들어간다. 이름이 틀리면 게임이 안 읽어
        /// 봇 설정이 조용히 안 먹는다 — 윙맨은 모드 이름과 파일 이름이 다르다.
        /// </summary>
        private static void BotCfgNames()
        {
            Report.Head("봇 설정 파일 이름");

            Report.Is("윙맨", GameData.Modes.First(m => m.Key == "wingman").Cfg, "competitive2v2");
            Report.Is("경쟁", GameData.Modes.First(m => m.Key == "competitive").Cfg, "competitive");

            foreach (GameMode m in GameData.Modes.Where(x => x.Cfg != null))
                Report.Is(m.Ko + " 파일이 목록에 있나", GameData.ServerCfgs.Contains(m.Cfg), true);
        }

        private static void WingmanAndRushMaps()
        {
            Report.Head("윙맨·러시 맵");

            string[] wingman = GameData.Modes.First(m => m.Key == "wingman").Maps.Select(x => x.Id).ToArray();
            Report.Is("윙맨 맵 수", wingman.Length, 7);
            Report.Is("윙맨 전용 셋이 있나",
                      wingman.Contains("de_debris") && wingman.Contains("de_eldorado") && wingman.Contains("de_poseidon"),
                      true);

            GameMap[] rush = GameData.Modes.First(m => m.Key == "rush").Maps;
            Report.Is("러시 맵 수", rush.Length, 1);
            Report.Is("러시 맵", rush[0].Id, "rush_001");
        }

        /// <summary>모드가 봇을 직접 정하는 곳은 칸을 잠가야 한다. 고를 수 있는데 안 먹는 게 제일 나쁘다.</summary>
        private static void BotsLockedWhereModeDecides()
        {
            Report.Head("봇을 모드가 정하는 곳은 잠기나");

            Report.Is("탈환", GameData.Modes.First(m => m.Key == "retake").BotsConfigurable, false);
            Report.Is("러시", GameData.Modes.First(m => m.Key == "rush").BotsConfigurable, false);
            Report.Is("경쟁은 고를 수 있나", GameData.Modes.First(m => m.Key == "competitive").BotsConfigurable, true);
            Report.Is("윙맨은 고를 수 있나", GameData.Modes.First(m => m.Key == "wingman").BotsConfigurable, true);

            Strings.Init("ko");
            Report.Is("러시 안내", Strings.ModeBotsNote("rush").Trim(),
                      "※ 러시는 봇을 게임이 정해요 (3대3, 빈자리를 채워요)");
            Report.Is("탈환 안내", Strings.ModeBotsNote("retake").Trim(),
                      "※ 탈환은 봇을 게임이 정해요 (수비 테러리스트 봇)");
        }

        /// <summary>새 모드도 보이는 이름이 아니라 키로 되살아나야 한다.</summary>
        private static void SavedByKeyNotName()
        {
            Report.Head("새 모드가 설정에서 되살아나나");

            Fake.Install("de_debris", "de_eldorado", "de_poseidon", "rush_001");
            Strings.Init("ko");

            Fake.WriteSettings("{\"mode\":\"wingman\",\"map\":\"de_poseidon\",\"bots\":0," +
                               "\"level\":\"1\",\"team\":\"any\",\"installDir\":null,\"lang\":\"ko\"}");
            using (var form = new MainForm(Settings.Load()))
            {
                Report.Is("모드", Peek.Combo(form, "_mode"), "윙맨");
                Report.Is("맵", Peek.Combo(form, "_map"), "포세이돈");
            }

            Fake.WriteSettings("{\"mode\":\"rush\",\"map\":\"rush_001\",\"bots\":0," +
                               "\"level\":\"1\",\"team\":\"any\",\"installDir\":null,\"lang\":\"ko\"}");
            using (var form = new MainForm(Settings.Load()))
            {
                Report.Is("모드", Peek.Combo(form, "_mode"), "러시");
                Report.Is("맵", Peek.Combo(form, "_map"), "컴플렉스");
                Report.Is("봇 칸이 잠겼나", Peek.Enabled(form, "_bots"), false);
            }
        }
    }
}
