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
            Fake.WorkshopMaps("3070596071=aim_botz", "1234567890=de_cache_night", "999=yprac_mirage");

            // 큰 맵은 _dir 하나와 _000·_001·_002로 쪼개져 저장된다. 맵 하나로 세어져야 한다
            Fake.WorkshopMaps("555=de_big_wingman:3");

            // 실제 구독 셋의 꼴. Shortdust는 본 맵 옆에 dust1을, Bikinibottom은 하늘 상자를 `maps/` 바로 아래 싣는다
            Fake.WorkshopMaps("777=dust1,de_shortdust:2");
            Fake.PublishData("777", "shortdust");
            Fake.WorkshopMaps("666=de_bikini,de_bikini_skybox");

            // 진짜 맵이 둘 든 묶음 — 폴더 이름과 맞는 게 없으면 둘 다 둔다
            Fake.WorkshopMaps("444=aim_a,aim_b");
            Fake.PublishData("444", "aim_pack");

            // 목차를 못 읽는 vpk — 받다가 끊긴 것. 이름을 모르면 열 수 없으니 안 띄운다
            Fake.WorkshopMaps("888=");

            MapNameFromVpk();
            SplitVpkCountsOnce();
            SubMapsLeftOut();
            TwoMapsInOneItem();
            UnreadableSkipped();
            FoundInList();
            WorkshopLaunchesWithMapName();
            OfferedInList();
            StaleWorkshopPickFallsBack();
            LanguageTogglesThePrefix();
            SortedByModeTag();
            TitleShownWhenKnown();
            UnknownShowsTheMapName();
            Fake.ForgetWorkshopInfo();
        }

        /// <summary>
        /// 맵 이름은 vpk 파일 이름이 아니라 목차 안의 `maps/이름.vpk`다. 하늘 상자(`maps/maps/prefabs/...`)와
        /// 재질 파일도 같은 목차에 들어 있어 경로가 정확히 `maps`인 것만 골라야 한다.
        /// </summary>
        private static void MapNameFromVpk()
        {
            Report.Head("vpk 목차에서 맵 이름을 읽나");

            Report.Is("맵 이름", ById("3070596071").WorkshopMapName, "aim_botz");
            Report.Is("하늘 상자·재질은 안 세나",
                      WorkshopMaps.Installed().Count(m => m.WorkshopId == "3070596071"), 1);
        }

        /// <summary>
        /// 받은 맵 넷이 열댓 개로 보이던 자리. vpk가 _dir 하나와 _000·_001... 로 쪼개져 있는데
        /// 전부 맵으로 세고 있었다. 목차는 _dir에만 있다.
        /// </summary>
        private static void SplitVpkCountsOnce()
        {
            Report.Head("쪼개진 vpk가 맵 하나로 세어지나");

            GameMap[] big = WorkshopMaps.Installed().Where(m => m.WorkshopId == "555").ToArray();
            Report.Is("파일 넷이 맵 몇 개로", big.Length, 1);
            Report.Is("맵 아이디", big.Length == 1 ? big[0].Id : "(없음)", "workshop/555/de_big_wingman");
        }

        /// <summary>하늘 상자나 본 맵이 끌어다 쓰는 작은 맵을 고르면 빈 하늘에 선다.</summary>
        private static void SubMapsLeftOut()
        {
            Report.Head("하늘 상자·딸린 맵은 빠지나");

            Report.Is("Shortdust", Maps("777"), "de_shortdust");
            Report.Is("Bikinibottom", Maps("666"), "de_bikini");
            Report.Is("하나만 남으면 맵 이름을 안 덧붙이나", ById("777").OneOfSeveral, false);
        }

        private static void TwoMapsInOneItem()
        {
            Report.Head("진짜 맵이 둘 든 묶음은 둘 다 뜨나");

            Report.Is("맵 이름", Maps("444"), "aim_a, aim_b");
            Report.Is("맵 이름을 덧붙이나", ById("444").OneOfSeveral, true);
        }

        private static string Maps(string id)
        {
            return string.Join(", ", WorkshopMaps.Installed().Where(m => m.WorkshopId == id)
                                                             .Select(m => m.WorkshopMapName).OrderBy(n => n));
        }

        private static void UnreadableSkipped()
        {
            Report.Head("목차를 못 읽는 vpk는 안 띄우나");

            Report.Is("빈 vpk", WorkshopMaps.Installed().Count(m => m.WorkshopId == "888"), 0);
        }

        private static void FoundInList()
        {
            Report.Head("받아 둔 창작마당 맵이 목록에 뜨나");

            GameMap[] found = WorkshopMaps.Installed();
            Report.Is("찾은 개수", found.Length, 8);
            Report.Is("번호", string.Join(", ", found.Select(m => m.WorkshopId).Distinct().OrderBy(n => n)),
                      "1234567890, 3070596071, 444, 555, 666, 777, 999");
        }

        /// <summary>
        /// 창작마당 맵은 `map_workshop 번호 맵이름`으로 연다. 지금까지 실기에서 안 된 넷 —
        /// `map workshop/번호/이름`·`host_workshop_map 번호`는 대기 화면,
        /// `map_workshop 번호`와 `map_workshop 번호 번호`는 「필요한 맵이 클라이언트에 없습니다」.
        /// 마지막 둘은 맵 이름을 몰라서였다. 이름은 vpk 목차에 있다.
        /// </summary>
        private static void WorkshopLaunchesWithMapName()
        {
            Report.Head("창작마당 맵을 켜고 바꾸는 명령");

            GameMap one = ById("3070596071");
            Report.Is("창작마당 맵으로 잡히나", one.IsWorkshop, true);
            Report.Is("켤 때", one.LaunchArgument, "+map_workshop 3070596071 aim_botz");
            Report.Is("바꿀 때(F10)", one.ChangeCommand,
                      "changelevel aim_botz customgamemode=3070596071 nomapvalidation=true");
            Report.Is("아이디는 되살릴 때 쓰는 값", one.Id, "workshop/3070596071/aim_botz");

            GameMap builtin = GameData.Modes.First(m => m.Key == "casual").Maps.First(m => m.Id == "de_dust2");
            Report.Is("기본 맵은 켤 때", builtin.LaunchArgument, "+map de_dust2");
            Report.Is("기본 맵은 바꿀 때", builtin.ChangeCommand, "changelevel de_dust2");
        }

        /// <summary>기본 맵 뒤에 붙고, 골라 두면 다시 켰을 때 그 맵이 남고, F10 파일에도 들어가나.</summary>
        private static void OfferedInList()
        {
            Report.Head("창작마당 맵이 목록에 붙나");

            Strings.Init("ko");
            Fake.ForgetWorkshopInfo();
            Fake.WriteSettings("{\"mode\":\"casual\",\"map\":\"workshop/3070596071/aim_botz\",\"bots\":0," +
                               "\"level\":\"1\",\"team\":\"any\",\"installDir\":null,\"lang\":\"ko\"}");
            using (var form = new MainForm(Settings.Load()))
            {
                var box = Peek.Field<System.Windows.Forms.ComboBox>(form, "_map");
                Report.Is("기본 맵이 그대로 있나", box.Items.Contains("더스트2"), true);
                Report.Is("창작마당 맵이 붙었나", box.Items.Contains("창작마당 · aim_botz"), true);
                Report.Is("고른 맵이 남나", Peek.Combo(form, "_map"), "창작마당 · aim_botz");
                Report.Has("맵 없는 친구 안내", Peek.Field<System.Windows.Forms.Label>(form, "_status").Text, "같은 링크로 다시 들어오면");
                Report.Note("맵 칸 항목 수: " + box.Items.Count);
            }

            string change = System.IO.Path.Combine(Cs2Paths.CfgDir, "cs2host_change.cfg");
            Report.Is("F10 파일", System.IO.File.ReadAllLines(change).Last(l => l.Length > 0),
                      "changelevel aim_botz customgamemode=3070596071 nomapvalidation=true");
        }

        /// <summary>1.2.0 전 판은 `workshop/번호`로 적었다. 그 설정이 남아 있어도 빈 칸이 되면 안 된다.</summary>
        private static void StaleWorkshopPickFallsBack()
        {
            Report.Head("옛 꼴로 적힌 창작마당 맵은 어떻게 되나");

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
            Report.Is("영어 표시", ById("3070596071").Name, "Workshop · aim_botz");
            Strings.Init("ko");
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
                "555|Big Wingman Map|Wingman,Map",
                "777|Shortdust|Classic,Map",
                "666|Bikini Bottom|Classic,Map",
                "444|Aim Pack|Custom,Map");

            Names("casual", "캐주얼");
            Names("competitive", "경쟁");
            Names("wingman", "윙맨");
            Names("deathmatch", "데스매치");
            Names("armsrace", "무기 레이스");

            Report.Is("러시는 안 받나", WorkshopMaps.For(Mode("rush")).Length, 0);
            Report.Is("탈환은 안 받나", WorkshopMaps.For(Mode("retake")).Length, 0);

            Report.Is("윙맨에 뜨는 것", Joined("wingman"), "Big Wingman Map");
            Report.Is("데스매치에 뜨는 것", Joined("deathmatch"), "Night Cache");
            Report.Is("경쟁에 뜨는 것", Joined("competitive"),
                      "Aim Botz, Aim Pack (aim_a), Aim Pack (aim_b), Bikini Bottom, Night Cache, Shortdust, Yprac Mirage");
            Report.Is("무기 레이스에 뜨는 것", Joined("armsrace"), "");
            Report.Is("모드 태그 없는 연습맵이 캐주얼에 뜨나", Joined("casual").Contains("Aim Botz"), true);
            Report.Note("Aim Botz는 Custom·Training뿐이고 모드 태그가 없다 — 그래서 Classic으로 친다");
        }

        private static GameMode Mode(string key) { return GameData.Modes.First(m => m.Key == key); }

        /// <summary>보이는 이름에서 「창작마당 · 」을 뗀 쪽. 제목을 알면 제목, 모르면 맵 이름이다.</summary>
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
            Report.Is("켤 때 넘기는 값은 그대로", botz.LaunchArgument, "+map_workshop 3070596071 aim_botz");
            Report.Note("제목이 바뀌어도 게임에 넘기는 값은 번호와 맵 이름이라 안 흔들린다");
        }

        /// <summary>
        /// 제목을 못 물어봤으면 맵 이름을 보여 준다. 인터넷이 없어도 캐주얼·경쟁에서는 고를 수 있어야 한다.
        /// </summary>
        private static void UnknownShowsTheMapName()
        {
            Report.Head("아직 못 물어본 맵은 어떻게 보이나");

            Fake.ForgetWorkshopInfo();
            Report.Is("캐주얼", WorkshopMaps.For(Mode("casual")).Length, 8);
            Report.Is("윙맨", WorkshopMaps.For(Mode("wingman")).Length, 0);
            Report.Is("맵 이름으로 보이나", ById("3070596071").Name, "창작마당 · aim_botz");
        }

        private static GameMap ById(string id)
        {
            return WorkshopMaps.Installed().First(m => m.WorkshopId == id);
        }
    }
}
