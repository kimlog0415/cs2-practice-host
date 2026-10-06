using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace CS2PracticeHost
{
    /// <summary>
    /// 새 버전 알림. 번호를 받아 오는 것과 그걸 창에 올리는 것은 다른 자리라 둘 다 본다 —
    /// 2026-10-03에 앞의 것만 보고 「확인 완료」로 닫았다가 뒤의 것이 안 돌아간 걸 놓쳤다.
    ///
    /// 이 확인기의 버전을 「지금 쓰는 사람의 판」으로 쓴다. csproj가 1.0.0이라 늘 낮고,
    /// 반대 경우(최신을 쓰는 사람에게 안 뜨는지)는 -p:Version=99.0.0 으로 빌드해서 본다.
    /// </summary>
    internal static class UpdateChecks
    {
        public static void Run()
        {
            Version mine = Assembly.GetExecutingAssembly().GetName().Version;
            bool pretendOld = mine.Major < 90;

            Report.Head("새 버전 번호를 받아 오는지 (이 확인기 " + mine.ToString(3) + " 기준)");

            string newer = UpdateCheck.NewerVersion();
            Report.Note("UpdateCheck.NewerVersion() = " + (newer ?? "null (새 버전 없음)"));

            if (!pretendOld)
            {
                Report.Is("최신을 쓰는 사람에게는 안 떠야 한다", newer ?? "null", "null");
                return;
            }

            if (newer == null)
            {
                Report.Fail("새 버전을 못 봤다 — 인터넷이 막혔거나 GitHub API 한도(IP당 시간당 60회)일 수 있다");
                return;
            }

            Strings.Init("ko");
            Report.Note("한국어 알림: " + Strings.UpdateFound(newer));
            Strings.Init("en");
            Report.Note("영어 알림  : " + Strings.UpdateFound(newer));

            ShowsInWindow(newer);
        }

        /// <summary>
        /// ⚠️ 창을 띄워야 Shown에 걸린 확인이 돈다. 창을 안 띄우는 방식으로는
        /// 여기가 통째로 안 돌아가고, 그걸 모르면 「확인했다」가 반쪽이 된다.
        /// </summary>
        private static void ShowsInWindow(string newer)
        {
            Report.Head("그 번호가 창에 실제로 뜨는지");

            Fake.Install();
            Strings.Init("ko");
            Fake.WriteSettings("{\"mode\":\"casual\",\"map\":\"de_dust2\",\"bots\":0," +
                               "\"level\":\"1\",\"team\":\"any\",\"installDir\":null,\"lang\":\"ko\"}");

            var form = new MainForm(Settings.Load());
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-3000, -3000);   // 화면 밖 — 보이는 상태여야 Shown이 돈다
            form.ShowInTaskbar = false;

            var timer = new Timer { Interval = 7000 };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                Look(form, newer);
                form.Close();
            };
            form.Shown += (s, e) => timer.Start();

            Application.Run(form);
        }

        private static void Look(Form form, string newer)
        {
            LinkLabel line = Peek.Field<LinkLabel>(form, "_update");
            string kept = (string)typeof(MainForm)
                .GetField("_newerVersion", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(form);

            Report.Is("알림줄이 보이나", line.Visible, true);
            Report.Is("알림줄 글", line.Text, Strings.UpdateFound(newer));
            Report.Is("기억한 새 버전", kept, newer);

            int bottom = line.Location.Y + line.Size.Height;
            Report.Is("창 안에 들어오나 (알림줄 아래끝 " + bottom + " / 창 높이 " + form.ClientSize.Height + ")",
                      bottom <= form.ClientSize.Height, true);

            // 누르면 어디로 가나 — 받을 것이 넷인 배포 페이지면 안 된다 (1.1.2에서 고친 자리)
            string site = (string)typeof(MainForm)
                .GetProperty("SiteUrl", BindingFlags.Static | BindingFlags.NonPublic)
                .GetValue(null, null);
            Report.Note("누르면 가는 곳: " + site);
            Report.Is("가는 곳에 github.com이 들어가나", site.Contains("github.com"), false);
        }
    }
}
