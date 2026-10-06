using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace CS2PracticeHost
{
    /// <summary>
    /// 앱의 네 상태를 게임 없이 만든다. 앱이 게임에서 보는 것은 셋뿐이다 —
    /// cs2라는 이름의 프로세스가 있나, 그 명령줄에 -condebug가 있나,
    /// console.log에 시작 시각 뒤의 ServerSteamID 줄이 있나.
    /// 그래서 이름만 cs2인 빈 프로세스와 손으로 쓴 로그로 전부 재현된다.
    /// </summary>
    internal static class StateChecks
    {
        private static string _dummy;

        public static void Run(string dummyExe)
        {
            Fake.Install();

            _dummy = dummyExe ?? Default();
            if (!File.Exists(_dummy))
            {
                Report.Fail("가짜 cs2.exe가 없다: " + _dummy +
                            "  — tests/cs2-dummy 를 먼저 빌드하거나 경로를 인자로 달라");
                return;
            }
            Report.Note("가짜 cs2.exe: " + _dummy);

            try
            {
                RetakeLocksBotOptions();
                BannerOff();
                BannerNoLog();
                BannerNoServer();
                BannerServerOpen();
            }
            finally { KillDummies(); }
        }

        private static string Default()
        {
            return Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                @"..\..\..\..\cs2-dummy\bin\Debug\net48\cs2.exe"));
        }

        /// <summary>탈환은 모드 규칙이 봇을 정해서 세 칸이 잠겨야 한다.</summary>
        private static void RetakeLocksBotOptions()
        {
            Report.Head("탈환을 고르면 봇 칸 셋이 잠기나");

            Settings(mode: "casual");
            using (var form = new MainForm(global::CS2PracticeHost.Settings.Load()))
            {
                Report.Is("캐주얼일 때 봇 수", Peek.Enabled(form, "_bots"), true);
                Report.Is("캐주얼일 때 난이도", Peek.Enabled(form, "_level"), true);
                Report.Is("캐주얼일 때 봇 팀", Peek.Enabled(form, "_team"), true);

                Peek.Pick(form, "_mode", "탈환");

                Report.Is("탈환일 때 봇 수", Peek.Enabled(form, "_bots"), false);
                Report.Is("탈환일 때 난이도", Peek.Enabled(form, "_level"), false);
                Report.Is("탈환일 때 봇 팀", Peek.Enabled(form, "_team"), false);

                Peek.Pick(form, "_mode", "캐주얼");
                Report.Is("되돌렸을 때 봇 수", Peek.Enabled(form, "_bots"), true);
                Report.Is("되돌렸을 때 난이도", Peek.Enabled(form, "_level"), true);
            }
        }

        private static void BannerOff()
        {
            Report.Head("CS2가 꺼져 있을 때 (회색 띠)");
            KillDummies();
            Check(Cs2State.Off, "○  CS2 꺼져 있음 · 아래 ▶ 버튼으로 시작",
                  Color.Gainsboro, "▶  CS2 켜고 서버 열기");
        }

        private static void BannerNoLog()
        {
            Report.Head("앱 밖에서 켠 CS2일 때 (노란 띠)");
            KillDummies();
            Start("");   // -condebug 없음 = 앱이 켠 게 아니다
            Check(Cs2State.NoLog, "●  CS2 실행 중 · 주소를 읽으려면 ▶로 다시 켜기",
                  Color.FromArgb(255, 243, 205), "▶  CS2 다시 켜고 서버 열기");
        }

        private static void BannerNoServer()
        {
            Report.Head("앱이 켰지만 서버가 없을 때 (파란 띠)");
            KillDummies();
            try { File.Delete(Cs2Paths.ConsoleLog); } catch (Exception) { }
            Start("-condebug");
            Check(Cs2State.NoServer, "●  CS2 실행 중 · 서버 없음 (메인 화면)",
                  Color.FromArgb(209, 231, 248), "▶  CS2 다시 켜고 서버 열기");
        }

        private static void BannerServerOpen()
        {
            Report.Head("서버가 열렸을 때 (초록 띠 + 주소)");
            KillDummies();
            Start("-condebug");

            // ⚠️ 커스텀 형식의 "/"는 문화권 날짜 구분자로 바뀐다(한국어는 "-").
            // 앱 파서는 InvariantCulture라 "/"를 글자 그대로 보므로 반드시 Invariant로 쓴다.
            DateTime at = DateTime.Now.AddSeconds(2);
            File.WriteAllText(Cs2Paths.ConsoleLog,
                at.ToString("MM/dd HH:mm:ss", CultureInfo.InvariantCulture)
                + " ServerSteamID=[A:1:1509803029:51595]\r\n");

            Check(Cs2State.ServerOpen,
                  "●  서버 열림 (" + at.ToString("HH:mm", CultureInfo.InvariantCulture) + ") · 맵 안에서 F10",
                  Color.FromArgb(212, 237, 218), "▶  CS2 다시 켜고 서버 열기");

            Cs2Status s = Cs2Watcher.Read();
            if (s.Server == null) { Report.Fail("서버 정보가 비었다"); return; }
            Report.Is("주소", s.Server.Address, "[A:1:1509803029:51595]");
            Report.Is("콘솔 명령", s.Server.ConnectCommand, "connect [A:1:1509803029:51595]");
            Report.Is("참가 링크", s.Server.JoinLink, "https://cs2.logstone.net/j/?a=A:1:1509803029:51595");
        }

        private static void Check(Cs2State want, string banner, Color back, string launch)
        {
            Report.Is("상태", Cs2Watcher.Read().State.ToString(), want.ToString());

            Settings(mode: "casual");
            using (var form = new MainForm(global::CS2PracticeHost.Settings.Load()))
            {
                Peek.Refresh(form);
                Label strip = Peek.Field<Label>(form, "_banner");
                Report.Is("띠 글", strip.Text, banner);
                Report.Is("띠 색", strip.BackColor.ToArgb().ToString("X8"), back.ToArgb().ToString("X8"));
                Report.Is("켜기 버튼", Peek.Field<Button>(form, "_launch").Text, launch);
            }
        }

        private static void Settings(string mode)
        {
            Strings.Init("ko");
            Fake.WriteSettings("{\"mode\":\"" + mode + "\",\"map\":\"de_dust2\",\"bots\":3," +
                               "\"level\":\"1\",\"team\":\"any\",\"installDir\":null,\"lang\":\"ko\"}");
        }

        private static void Start(string arguments)
        {
            Process p = Process.Start(new ProcessStartInfo(_dummy, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
            });
            for (int i = 0; i < 40 && Cs2Process.Find() == null; i++) Thread.Sleep(50);
            Thread.Sleep(150);   // WMI가 명령줄을 보여줄 때까지
            Report.Note("cs2 흉내 띄움 (pid " + p.Id + ", 인자 「" + arguments + "」)");
        }

        /// <summary>
        /// ⚠️ 확인하면서 앱의 ▶ 버튼은 절대 누르지 않는다 —
        /// Cs2Process.CloseAll()이 cs2 이름의 프로세스를 전부 죽인다.
        /// </summary>
        private static void KillDummies()
        {
            foreach (Process p in Process.GetProcessesByName("cs2"))
            {
                try { p.Kill(); p.WaitForExit(5000); } catch (Exception) { }
            }
            for (int i = 0; i < 40 && Cs2Process.Find() != null; i++) Thread.Sleep(50);
        }
    }
}
