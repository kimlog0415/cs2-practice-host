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
            Fake.WorkshopMaps("3070596071", "1234567890", "999");

            // 큰 맵은 _dir 하나와 _000·_001·_002로 쪼개져 저장된다. 맵 하나로 세어져야 한다
            Fake.WorkshopMaps("555:3");

            SplitVpkCountsOnce();
            FoundInList();
            WorkshopLaunchesByNumber();
            NotOfferedWhileParked();
            StaleWorkshopPickFallsBack();
            LanguageTogglesThePrefix();
            SortedByModeTag();
            TitleShownWhenKnown();
            UnknownShowsTheNumber();
            Fake.ForgetWorkshopInfo();
        }

        /// <summary>
        /// 제작자가 단 태그로 갈라서 윙맨용 맵은 윙맨에만 뜬다.
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

            Report.Is("윙맨에 뜨는 것", Joined("wingman"), "Big Wingman Map");
            Report.Is("데스매치에 뜨는 것", Joined("deathmatch"), "Night Cache");
            Report.Is("경쟁에 뜨는 것", Joined("competitive"), "Aim Botz, Night Cache, Yprac Mirage");
            Report.Is("무기 레이스에 뜨는 것", Joined("armsrace"), "");
            Report.Is("모드 태그 없는 연습맵이 캐주얼에 뜨나", Joined("casual").Contains("Aim Botz"), true);
            Report.Note("Aim Botz는 Custom·Training뿐이고 모드 태그가 없다 — 그래서 Classic으로 친다");
        }

        private static GameMode Mode(string key) { return GameData.Modes.First(m => m.Key == key); }

        /// <summary>보이는 이름에서 「창작마당 · 」을 뗀 쪽. 제목을 알면 제목, 모르면 번호다.</summary>
        private static string Joined(string key)
        {
            return string.Join(", ", WorkshopMaps.For(Mode(key)).Select(Shown));
        }

        private static string Shown(GameMap map)
        {
            int cut = map.Name.IndexOf('·');
            return cut < 0 ? map.Name : map.Name.Substring(cut + 1).Trim();
        }

        private static void Names(string key, string shown)
        {
            Report.Note(shown + ": " + (Joined(key).Length == 0 ? "(없음)" : Joined(key)));
        }

        private static void TitleShownWhenKnown()
        {
            Report.Head("제목을 알면 제목으로 보이나");

            Strings.Init("ko");
            GameMap botz = ById("3070596071");
            Report.Is("보이는 이름", botz.Name, "창작마당 · Aim Botz");
            Report.Is("켤 때 넘기는 값은 그대로", botz.Id, "workshop/3070596071");
            Report.Note("제목이 바뀌어도 게임에 넘기는 값은 번호라 안 흔들린다");
        }

        /// <summary>
        /// 제목을 못 물어봤으면 번호를 보여 준다. 파일 이름은 쓸 수가 없다 — 그것도 번호다.
        /// 인터넷이 없어도 캐주얼·경쟁에서는 고를 수 있어야 한다.
        /// </summary>
        private static void UnknownShowsTheNumber()
        {
            Report.Head("아직 못 물어본 맵은 어떻게 보이나");

            Fake.ForgetWorkshopInfo();
            Report.Is("캐주얼", WorkshopMaps.For(Mode("casual")).Length, 4);
            Report.Is("윙맨", WorkshopMaps.For(Mode("wingman")).Length, 0);
            Report.Is("번호로 보이나", ById("3070596071").Name, "창작마당 · 3070596071");
        }

        /// <summary>
        /// 받은 맵 넷이 열댓 개로 보이던 자리. vpk가 _dir 하나와 _000·_001... 로 쪼개져 있는데
        /// 전부 맵으로 세고 있었다. 지금은 파일을 아예 안 세고 번호 폴더를 센다.
        /// </summary>
        private static void SplitVpkCountsOnce()
        {
            Report.Head("쪼개진 vpk가 맵 하나로 세어지나");

            GameMap[] big = WorkshopMaps.Installed().Where(m => m.WorkshopId == "555").ToArray();
            Report.Is("파일 넷이 맵 몇 개로", big.Length, 1);
            Report.Is("맵 아이디", big.Length == 1 ? big[0].Id : "(없음)", "workshop/555");
        }

        private static void FoundInList()
        {
            Report.Head("받아 둔 창작마당 맵이 목록에 뜨나");

            GameMap[] found = WorkshopMaps.Installed();
            Report.Is("찾은 개수", found.Length, 4);
            Report.Is("번호", string.Join(", ", found.Select(m => m.WorkshopId)),
                      "1234567890, 3070596071, 555, 999");
        }

        /// <summary>
        /// 창작마당 맵은 경로가 아니라 구독 번호로 받는다. `+map workshop/번호/이름`이나
        /// 전용 서버용 `host_workshop_map`으로 주면 서버가 안 열려 대기 화면에 선다.
        /// 맵 이름을 덧붙이면 서버는 열리는데 그 이름의 맵이 없다고 끊긴다 —
        /// vpk가 번호로 저장돼서 맵 이름을 알 길이 없다. 그래서 번호만 준다.
        /// </summary>
        private static void WorkshopLaunchesByNumber()
        {
            Report.Head("창작마당 맵을 켜고 바꾸는 명령");

            GameMap one = ById("3070596071");
            Report.Is("창작마당 맵으로 잡히나", one.IsWorkshop, true);
            Report.Is("켤 때", one.LaunchArgument, "+map_workshop 3070596071");
            Report.Is("바꿀 때", one.ChangeCommand, "map_workshop 3070596071");
            Report.Is("맵 이름이 안 붙나", one.LaunchArgument.Split(' ').Length, 2);
            Report.Is("아이디는 되살릴 때 쓰는 값 그대로", one.Id, "workshop/3070596071");

            GameMap builtin = GameData.Modes.First(m => m.Key == "casual").Maps.First(m => m.Id == "de_dust2");
            Report.Is("기본 맵은 켤 때", builtin.LaunchArgument, "+map de_dust2");
            Report.Is("기본 맵은 바꿀 때", builtin.ChangeCommand, "changelevel de_dust2");
        }

        /// <summary>
        /// 1.2.0은 창작마당 맵을 목록에 안 붙인다 — 받아 둔 맵을 찾는 것까지는 되는데
        /// CS2가 콘솔로 그 맵을 여는 것을 받아 주지 않아 대기 화면에 선다.
        /// 고를 수 있는데 안 열리는 게 제일 나쁘니 목록에서 뺀 채로 내보낸다.
        /// </summary>
        private static void NotOfferedWhileParked()
        {
            Report.Head("창작마당 맵이 목록에 안 뜨나 (아직 막아 둔 상태)");

            Strings.Init("ko");
            Report.Is("찾기는 되나", WorkshopMaps.Installed().Length, 4);

            Fake.WriteSettings("{\"mode\":\"casual\",\"map\":\"de_dust2\",\"bots\":0," +
                               "\"level\":\"1\",\"team\":\"any\",\"installDir\":null,\"lang\":\"ko\"}");
            using (var form = new MainForm(Settings.Load()))
            {
                var box = Peek.Field<System.Windows.Forms.ComboBox>(form, "_map");
                Report.Is("기본 맵이 그대로 있나", box.Items.Contains("더스트2"), true);
                Report.Is("창작마당 맵이 안 붙었나", box.Items.Contains("창작마당 · 3070596071"), false);
                Report.Note("맵 칸 항목 수: " + box.Items.Count);
            }
        }

        /// <summary>앞 판에서 창작마당 맵을 골라 둔 설정이 남아 있어도 빈 칸이 되면 안 된다.</summary>
        private static void StaleWorkshopPickFallsBack()
        {
            Report.Head("설정에 남은 창작마당 맵은 어떻게 되나");

            Strings.Init("ko");
            Fake.WriteSettings("{\"mode\":\"casual\",\"map\":\"workshop/3070596071\",\"bots\":0," +
                               "\"level\":\"1\",\"team\":\"any\",\"installDir\":null,\"lang\":\"ko\"}");
            using (var form = new MainForm(Settings.Load()))
            {
                Report.Is("고른 맵", Peek.Combo(form, "_map"), "더스트2");
            }
        }

        private static void LanguageTogglesThePrefix()
        {
            Report.Head("언어를 바꾸면 표시도 바뀌나");

            Strings.Init("en");
            Report.Is("영어 표시", ById("3070596071").Name, "Workshop · 3070596071");
            Strings.Init("ko");
        }

        private static GameMap ById(string id)
        {
            return WorkshopMaps.Installed().First(m => m.WorkshopId == id);
        }
    }
}
