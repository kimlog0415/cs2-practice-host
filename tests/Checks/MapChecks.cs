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

            FoundInList();
            IdIsWorkshopForm();
            NotMixedWithBuiltIn();
            LanguageTogglesThePrefix();
        }

        private static void FoundInList()
        {
            Report.Head("받아 둔 창작마당 맵이 목록에 뜨나");

            GameMap[] found = WorkshopMaps.Installed();
            Report.Is("찾은 개수", found.Length, 3);
            Report.Is("이름순으로 서나", string.Join(", ", found.Select(m => m.WorkshopFile)),
                      "aim_botz, de_cache_night, yprac_mirage");
        }

        private static void IdIsWorkshopForm()
        {
            Report.Head("켤 때 넘기는 값이 workshop/번호/이름 꼴인가");

            GameMap one = WorkshopMaps.Installed().First(m => m.WorkshopFile == "aim_botz");
            Report.Is("맵 아이디", one.Id, "workshop/3070596071/aim_botz");
            Report.Is("창작마당 맵으로 잡히나", one.IsWorkshop, true);
            Report.Note("이 값이 +map 과 changelevel 에 그대로 들어간다");
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
