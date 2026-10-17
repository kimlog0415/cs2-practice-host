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
            Report.Note("한국어 알림: " + Strings.UpdateTitle + " / " + Strings.UpdateGet);
            Strings.Init("en");
            Report.Note("영어 알림  : " + Strings.UpdateTitle + " / " + Strings.UpdateGet);

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
            Panel overlay = Peek.Field<Panel>(form, "_update");
            Label versions = Peek.Field<Label>(form, "_updateVersions");
            Button get = Peek.Field<Button>(form, "_updateGet");
            LinkLabel later = Peek.Field<LinkLabel>(form, "_updateLater");
            string kept = (string)typeof(MainForm)
                .GetField("_newerVersion", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(form);
            string mine = (string)typeof(MainForm)
                .GetProperty("MyVersion", BindingFlags.Static | BindingFlags.NonPublic)
                .GetValue(null, null);

            Report.Is("덮는 판이 보이나", overlay.Visible, true);
            Report.Is("기억한 새 버전", kept, newer);
            Report.Is("두 판을 나란히 보여 주나", versions.Text, Strings.UpdateVersions(mine, newer));
            Report.Is("제목", Peek.Field<Label>(form, "_updateTitle").Text, Strings.UpdateTitle);
            Report.Is("받는 버튼", get.Text, Strings.UpdateGet);
            Report.Note("받는 법 안내: " + Strings.UpdateHow.Replace('\n', ' '));

            // 창을 덮어야 놓치지 않는다 — 한 줄로 두니 지나갔던 자리다(2026-10-04)
            Report.Is("창 전체를 덮나", overlay.Size == form.ClientSize, true);
            Report.Is("맨 위에 있나", form.Controls.GetChildIndex(overlay), 0);

            // ⚠️ 못 닫게 하면 버전 확인이 실패했을 때 멀쩡한 도구가 안 열린다
            Report.Is("닫는 길이 있나", later.Text, Strings.UpdateLater);
            Peek.ClickLink(later);
            Report.Is("닫으면 사라지나", overlay.Visible, false);

            // 누르면 어디로 가나 — 받을 것이 넷인 배포 페이지면 안 된다 (1.1.2에서 고친 자리)
            string site = (string)typeof(MainForm)
                .GetProperty("SiteUrl", BindingFlags.Static | BindingFlags.NonPublic)
                .GetValue(null, null);
            Report.Note("누르면 가는 곳: " + site);
            Report.Is("가는 곳에 github.com이 들어가나", site.Contains("github.com"), false);
        }
    }
}
