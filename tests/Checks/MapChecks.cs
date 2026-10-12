using System.Linq;

namespace CS2PracticeHost
{
    /// <summary>
    /// 받아 둔 창작마당 맵이 목록에 뜨는지, 켤 때 넘기는 값이 맞는지.
    /// 켜면 어떻게 되는지는 게임이 있어야 보이므로 여기서는 「무엇을 넘기나」까지만 본다.
    /// </summary>
    internal static class MapChecks
    {
        public static void Run()
        {
            Fake.Install();
            Fake.WorkshopMaps("3070596071/aim_botz", "1234567890/de_cache_night", "999/yprac_mirage");

            // 큰 맵은 쪼개져 저장된다. 하나로 세어져야 한다
            Fake.WorkshopMaps("555/de_big_dir", "555/de_big_000", "555/de_big_001", "555/de_big_002");

            SplitVpkCountsOnce();
            FoundInList();
            WorkshopLaunchesByNumber();
            NotMixedWithBuiltIn();
            LanguageTogglesThePrefix();
            SortedByModeTag();
            TitleShownWhenKnown();
            UnknownGoesToClassic();
            Fake.ForgetWorkshopInfo();
        }

        /// <summary>
        /// 이번 변경의 핵심. 제작자가 단 태그로 갈라서 윙맨용 맵은 윙맨에만 뜬다.
        /// 러시·탈환은 전용 배치가 필요해 창작마당 맵을 아예 안 받는다.
        /// </summary>
        private static void SortedByModeTag()
        {
            Report.Head("모드마다 맞는 창작마당 맵만 뜨나");

            Fake.WorkshopInfoCache(
                "3070596071|Aim Botz|Custom,Training,Map",
                "1234567890|Night Cache|Classic,Deathmatch,Map",
                "999|Yprac Mirage|Classic,Map",
                "555|Big Wingman Map|Wingman,Map");

            Names("casual", "캐주얼");
            Names("competitive", "경쟁");
            Names("wingman", "윙맨");
            Names("deathmatch", "데스매치");
            Names("armsrace", "무기 레이스");

            Report.Is("러시는 안 받나", WorkshopMaps.For(Mode("rush")).Length, 0);
            Report.Is("탈환은 안 받나", WorkshopMaps.For(Mode("retake")).Length, 0);

            Report.Is("윙맨에 뜨는 것", Joined("wingman"), "de_big");
            Report.Is("데스매치에 뜨는 것", Joined("deathmatch"), "de_cache_night");
            Report.Is("경쟁에 뜨는 것", Joined("competitive"), "aim_botz, de_cache_night, yprac_mirage");
            Report.Is("무기 레이스에 뜨는 것", Joined("armsrace"), "");
            Report.Is("모드 태그 없는 연습맵이 캐주얼에 뜨나", Joined("casual").Contains("aim_botz"), true);
            Report.Note("Aim Botz는 Custom·Training뿐이고 모드 태그가 없다 — 그래서 Classic으로 친다");
        }

        private static GameMode Mode(string key) { return GameData.Modes.First(m => m.Key == key); }

        private static string Joined(string key)
        {
            return string.Join(", ", WorkshopMaps.For(Mode(key)).Select(m => m.WorkshopFile));
        }

        private static void Names(string key, string shown)
        {
            Report.Note(shown + ": " + (Joined(key).Length == 0 ? "(없음)" : Joined(key)));
        }

        private static void TitleShownWhenKnown()
        {
            Report.Head("제목을 알면 제목으로 보이나");

            Strings.Init("ko");
            GameMap botz = WorkshopMaps.Installed().First(m => m.WorkshopFile == "aim_botz");
            Report.Is("보이는 이름", botz.Name, "창작마당 · Aim Botz");
            Report.Is("켤 때 넘기는 값은 그대로", botz.Id, "workshop/3070596071/aim_botz");
            Report.Note("제목이 바뀌어도 게임에 넘기는 값은 파일 이름이라 안 흔들린다");
        }

        /// <summary>인터넷이 없어도 캐주얼·경쟁에서는 고를 수 있어야 한다.</summary>
        private static void UnknownGoesToClassic()
        {
            Report.Head("아직 못 물어본 맵은 어디에 뜨나");

            Fake.ForgetWorkshopInfo();
            Report.Is("캐주얼", WorkshopMaps.For(Mode("casual")).Length, 4);
            Report.Is("윙맨", WorkshopMaps.For(Mode("wingman")).Length, 0);
            Report.Is("파일 이름으로 보이나",
                      WorkshopMaps.Installed().First(m => m.WorkshopFile == "aim_botz").Name,
                      "창작마당 · aim_botz");
        }

        /// <summary>
        /// 받은 맵 넷이 열댓 개로 보이던 자리. vpk가 _dir 하나와 _000·_001... 로 쪼개져 있는데
        /// 전부 맵으로 세고 있었다.
        /// </summary>
        private static void SplitVpkCountsOnce()
        {
            Report.Head("쪼개진 vpk가 맵 하나로 세어지나");

            GameMap[] big = WorkshopMaps.Installed().Where(m => m.WorkshopFile.StartsWith("de_big")).ToArray();
            Report.Is("파일 넷이 맵 몇 개로", big.Length, 1);
            Report.Is("이름에 _dir이 안 붙나", big.Length == 1 ? big[0].WorkshopFile : "(없음)", "de_big");
            Report.Is("맵 아이디", big.Length == 1 ? big[0].Id : "(없음)", "workshop/555/de_big");
        }

        private static void FoundInList()
        {
            Report.Head("받아 둔 창작마당 맵이 목록에 뜨나");

            GameMap[] found = WorkshopMaps.Installed();
            Report.Is("찾은 개수", found.Length, 4);
            Report.Is("이름순으로 서나", string.Join(", ", found.Select(m => m.WorkshopFile)),
                      "aim_botz, de_big, de_cache_night, yprac_mirage");
        }

        /// <summary>
        /// 창작마당 맵은 경로가 아니라 구독 번호로 받는다. `+map workshop/번호/이름`으로 주면
        /// 서버가 안 열리고 대기 화면에 선다 — 디렉터가 실제로 겪은 자리다.
        /// 서버가 없으면 changelevel도 바꿀 게 없어 F10까지 안 먹는다.
        /// </summary>
        private static void WorkshopLaunchesByNumber()
        {
            Report.Head("창작마당 맵을 켜고 바꾸는 명령");

            GameMap one = WorkshopMaps.Installed().First(m => m.WorkshopFile == "aim_botz");
            Report.Is("창작마당 맵으로 잡히나", one.IsWorkshop, true);
            Report.Is("켤 때", one.LaunchArgument, "+map_workshop 3070596071 aim_botz");
            Report.Is("바꿀 때", one.ChangeCommand, "map_workshop 3070596071 aim_botz");
            Report.Is("아이디는 되살릴 때 쓰는 값 그대로", one.Id, "workshop/3070596071/aim_botz");

            GameMap builtin = GameData.Modes.First(m => m.Key == "casual").Maps.First(m => m.Id == "de_dust2");
            Report.Is("기본 맵은 켤 때", builtin.LaunchArgument, "+map de_dust2");
            Report.Is("기본 맵은 바꿀 때", builtin.ChangeCommand, "changelevel de_dust2");
        }

        /// <summary>기본 맵과 섞여 보이면 어느 쪽인지 모른다.</summary>
        private static void NotMixedWithBuiltIn()
        {
            Report.Head("기본 맵과 구분되나");

            Strings.Init("ko");
            GameMap workshop = WorkshopMaps.Installed().First();
            Report.Is("보이는 이름", workshop.Name, "창작마당 · aim_botz");

            Fake.WriteSettings("{\"mode\":\"casual\",\"map\":\"de_dust2\",\"bots\":0," +
                               "\"level\":\"1\",\"team\":\"any\",\"installDir\":null,\"lang\":\"ko\"}");
            using (var form = new MainForm(Settings.Load()))
            {
                var box = Peek.Field<System.Windows.Forms.ComboBox>(form, "_map");
                Report.Is("기본 맵이 그대로 있나", box.Items.Contains("더스트2"), true);
                Report.Is("창작마당 맵이 붙었나", box.Items.Contains("창작마당 · aim_botz"), true);
                Report.Note("맵 칸 항목 수: " + box.Items.Count);
            }
        }

        private static void LanguageTogglesThePrefix()
        {
            Report.Head("언어를 바꾸면 표시도 바뀌나");

            Strings.Init("en");
            Report.Is("영어 표시", WorkshopMaps.Installed().First().Name, "Workshop · aim_botz");
            Strings.Init("ko");
        }
    }
}
