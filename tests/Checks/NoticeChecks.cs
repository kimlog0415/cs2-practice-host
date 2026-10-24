using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CS2PracticeHost
{
    /// <summary>
    /// 사이트에서 받는 안내문(<see cref="Notices"/>). 언제 붙고 언제 안 붙는지, 사이트에 올릴 파일이 읽히는지 본다.
    /// 네트워크는 안 탄다 — 받은 글 대신 <see cref="Notices.Use"/>로 넣는다.
    /// </summary>
    internal static class NoticeChecks
    {
        private const string Sample = @"[
  { ""id"": ""a"", ""when"": ""workshop"", ""from"": """", ""until"": """", ""ko"": ""창작마당 안내"", ""en"": ""workshop note"" },
  { ""id"": ""b"", ""when"": ""always"", ""from"": ""2000-01-01"", ""until"": """", ""ko"": ""늘 안내"", ""en"": """" },
  { ""id"": ""c"", ""when"": ""wingman"", ""ko"": ""윙맨 안내"", ""en"": ""wingman note"" },
  { ""id"": ""d"", ""when"": ""always"", ""until"": ""2000-01-01"", ""ko"": ""지난 안내"" },
  { ""id"": ""e"", ""when"": ""always"", ""from"": ""2999-01-01"", ""ko"": ""앞으로 안내"" },
  { ""id"": ""f"", ""when"": ""someday-new-rule"", ""ko"": ""모르는 조건"" }
]";

        public static void Run()
        {
            Report.Head("안내문이 맞는 자리에만 붙나");

            Strings.Init("ko");
            Notices.Use(Sample);
            string workshop = Notices.For("casual", true);
            Report.Has("창작마당을 고르면 창작마당 안내", workshop, "※ 창작마당 안내");
            Report.Has("늘 안내도 붙는다", workshop, "※ 늘 안내");
            Report.Is("기본 맵이면 창작마당 안내는 없다", Notices.For("casual", false).Contains("창작마당 안내"), false);
            Report.Has("모드 키로 고른 안내", Notices.For("wingman", false), "윙맨 안내");
            Report.Is("다른 모드에선 안 붙는다", Notices.For("casual", false).Contains("윙맨"), false);
            Report.Is("끝난 안내는 안 붙는다", workshop.Contains("지난 안내"), false);
            Report.Is("아직 안 된 안내는 안 붙는다", workshop.Contains("앞으로 안내"), false);
            Report.Is("모르는 조건은 안 붙는다(옛 판 보호)", Notices.For("casual", true).Contains("모르는 조건"), false);

            Strings.Init("en");
            string en = Notices.For("casual", true);
            Report.Has("영어로 붙는다", en, "* workshop note");
            Report.Is("영어가 비어 있으면 한국어를 끌어오지 않는다", en.Contains("늘 안내"), false);

            Report.Head("안내문이 상태 칸을 넘치지 않나");
            Strings.Init("ko");
            Notices.Use("[" + string.Join(",", new[] { "1", "2", "3", "4" }.Select(i =>
                "{\"when\":\"always\",\"ko\":\"" + i + new string('가', 300) + "\"}")) + "]");
            string many = Notices.For("casual", false);
            Report.Is("두 줄까지만", many.Split('\n').Length - 1, 2);
            Report.Is("한 줄은 160자 안", many.Split('\n')[1].Length <= 2 + 160, true);
            Notices.Use("[{\"when\":\"always\",\"ko\":\"줄\\n바꿈\"}]");
            Report.Is("줄바꿈은 한 줄로 편다", Notices.For("casual", false), "\n※ 줄 바꿈");

            Report.Head("깨진 글·빈 글");
            Report.Is("깨진 글은 null(받아 둔 것을 덮지 않게)", Notices.Parse("{not json") == null, true);
            Notices.Use("{not json");
            Report.Is("깨진 글이면 아무것도 안 붙는다", Notices.For("casual", true), "");

            SiteFileParses();
            ShowsInWindow();

            Notices.Use(null);
        }

        /// <summary>사이트에 올릴 파일 자체가 읽히나. 저장소에서 확인기를 돌릴 때만 찾는다.</summary>
        private static void SiteFileParses()
        {
            Report.Head("site/notices.json이 읽히나");
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            string path = null;
            for (int i = 0; i < 8 && dir != null; i++, dir = Path.GetDirectoryName(dir.TrimEnd('\\')))
            {
                string candidate = Path.Combine(dir, "site", "notices.json");
                if (File.Exists(candidate)) { path = candidate; break; }
            }
            if (path == null) { Report.Note("저장소 밖에서 돌아 site/notices.json을 못 찾았다 — 건너뜀"); return; }

            Notice[] all = Notices.Parse(File.ReadAllText(path));
            Report.Is("읽힌다", all != null, true);
            if (all == null) return;
            Report.Note(path + " — " + all.Length + "개");
            foreach (Notice n in all)
            {
                Report.Is(n.Id + " 한국어가 있다", !string.IsNullOrWhiteSpace(n.Ko), true);
                Report.Is(n.Id + " 영어가 있다", !string.IsNullOrWhiteSpace(n.En), true);
                Report.Is(n.Id + " 160자 안(한)", (n.Ko ?? "").Length <= 160, true);
                Report.Is(n.Id + " 160자 안(영)", (n.En ?? "").Length <= 160, true);
            }
        }

        /// <summary>창에서 창작마당 맵을 고르면 상태 칸에 실제로 붙나.</summary>
        private static void ShowsInWindow()
        {
            Report.Head("창의 상태 칸에 붙나");
            Fake.Install();
            Fake.ForgetWorkshopInfo();
            Fake.WorkshopMaps("3070596071=aim_botz");
            Strings.Init("ko");
            Notices.Use(Sample);
            Fake.WriteSettings("{\"mode\":\"casual\",\"map\":\"workshop/3070596071/aim_botz\",\"bots\":0," +
                               "\"level\":\"1\",\"team\":\"any\",\"installDir\":null,\"lang\":\"ko\"}");
            using (var form = new MainForm(Settings.Load()))
            {
                string text = Peek.Field<Label>(form, "_status").Text;
                Report.Has("창작마당 맵이면 붙는다", text, "※ 창작마당 안내");
                Report.Has("원래 안내도 그대로", text, "같은 링크로 다시 들어오면");
            }
        }
    }
}
