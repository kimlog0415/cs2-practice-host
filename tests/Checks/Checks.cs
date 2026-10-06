using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace CS2PracticeHost
{
    /// <summary>
    /// 게임 없이 앱을 확인한다. 게임이 깔린 PC를 못 쓰는 날에도 돌릴 수 있고,
    /// 손으로 눌러서는 순서를 밟아야 보이는 것까지 한 번에 본다.
    ///
    /// 쓰는 법은 tests/README.md 참고.
    /// </summary>
    internal static class Checks
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();

            string mode = args.Length > 0 ? args[0] : "all";
            Console.WriteLine("확인 대상: " + (mode == "all" ? "lang, state, update" : mode));
            Console.WriteLine("이 확인기 버전: " + Assembly.GetExecutingAssembly().GetName().Version);
            Console.WriteLine();

            Fake.KeepRealSettings();
            try
            {
                if (mode == "all" || mode == "lang") LangChecks.Run();
                if (mode == "all" || mode == "state") StateChecks.Run(args.Skip(1).FirstOrDefault());
                if (mode == "all" || mode == "update") UpdateChecks.Run();
            }
            catch (Exception ex)
            {
                Report.Fail("확인 도중 예외: " + ex.GetType().Name + ": " + ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                Fake.RestoreRealSettings();
                Fake.Clean();
            }

            return Report.Summary();
        }
    }

    /// <summary>통과·실패를 한곳에서 센다.</summary>
    internal static class Report
    {
        private static int _failed;

        public static void Head(string what)
        {
            Console.WriteLine();
            Console.WriteLine("## " + what);
        }

        public static void Note(string line) { Console.WriteLine("    " + line); }

        public static void Is(string what, object got, object want)
        {
            string g = got == null ? "null" : got.ToString();
            string w = want == null ? "null" : want.ToString();
            bool ok = g == w;
            if (!ok) _failed++;
            Console.WriteLine("  " + (ok ? "통과" : "실패") + "  " + what +
                (ok ? " = " + g : " → 받은 것 「" + g + "」 / 기대한 것 「" + w + "」"));
        }

        public static void Has(string what, string text, string needle)
        {
            bool ok = text.Contains(needle);
            if (!ok) _failed++;
            Console.WriteLine("  " + (ok ? "통과" : "실패") + "  " + what + " — " + needle + (ok ? "" : " 가 없다"));
        }

        public static void HasNot(string what, string text, string needle)
        {
            bool ok = !text.Contains(needle);
            if (!ok) _failed++;
            Console.WriteLine("  " + (ok ? "통과" : "실패") + "  " + what + (ok ? "" : " — 「" + needle + "」가 남아 있다"));
        }

        public static void Fail(string why)
        {
            _failed++;
            Console.WriteLine();
            Console.WriteLine("  실패  " + why);
        }

        public static int Summary()
        {
            Console.WriteLine();
            Console.WriteLine(_failed == 0 ? "전부 통과" : _failed + "개 실패");
            return _failed == 0 ? 0 : 1;
        }
    }

    /// <summary>가짜 CS2 설치 폴더와 설정 파일. 앱이 게임에서 보는 것은 맵 파일이 있는지뿐이다.</summary>
    internal static class Fake
    {
        public const string Root = "cs2host-checks";

        public static string Install(params string[] extraMaps)
        {
            string root = Path.Combine(Path.GetTempPath(), Root, "Counter-Strike Global Offensive");
            string maps = Path.Combine(root, @"game\csgo\maps");
            Directory.CreateDirectory(maps);
            Directory.CreateDirectory(Path.Combine(root, @"game\csgo\cfg"));

            var ids = new List<string>
            {
                "de_dust2", "de_nuke", "de_mirage", "de_inferno", "de_anubis",
                "de_ancient", "de_train", "de_vertigo", "de_overpass", "de_cache",
                "ar_baggage", "ar_pool_day",
            };
            ids.AddRange(extraMaps);

            foreach (string id in ids)
            {
                string f = Path.Combine(maps, id + ".vpk");
                if (!File.Exists(f)) File.WriteAllText(f, "");
            }

            if (!Cs2Paths.UseManual(root))
                throw new Exception("가짜 설치 폴더를 앱이 안 받아들였다: " + root);
            return root;
        }

        public static void Clean()
        {
            try { Directory.Delete(Path.Combine(Path.GetTempPath(), Root), true); }
            catch (Exception) { }
        }

        /// <summary>
        /// ⚠️ 설정은 실제 %APPDATA%에 쓴다. GetFolderPath는 환경변수로 못 옮긴다.
        /// 돌리기 전에 그 폴더가 원래 있었는지 보고, 없었으면 끝나고 지운다.
        /// </summary>
        public static string SettingsPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "CS2PracticeHost", "settings.json");
        }

        public static void WriteSettings(string json)
        {
            string path = SettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, json);
        }

        public static string ReadSettings() { return File.ReadAllText(SettingsPath()); }

        private static string _saved;
        private static bool _existed;

        /// <summary>
        /// 확인기는 쓰는 사람의 실제 설정 파일을 덮어쓴다(GetFolderPath는 환경변수로 못 옮긴다).
        /// 그래서 시작할 때 내용을 들고 있다가 끝나면 그대로 돌려놓는다.
        /// </summary>
        public static void KeepRealSettings()
        {
            _existed = File.Exists(SettingsPath());
            _saved = _existed ? File.ReadAllText(SettingsPath()) : null;
        }

        public static void RestoreRealSettings()
        {
            try
            {
                if (_existed) File.WriteAllText(SettingsPath(), _saved);
                else if (File.Exists(SettingsPath())) File.Delete(SettingsPath());
            }
            catch (Exception) { }
        }
    }

    /// <summary>창을 띄우지 않고 MainForm 속을 들여다본다.</summary>
    internal static class Peek
    {
        public static T Field<T>(Form form, string name)
        {
            FieldInfo info = typeof(MainForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (info == null) throw new Exception("그런 칸이 없다: " + name);
            return (T)info.GetValue(form);
        }

        public static string Combo(Form form, string name) { return Field<ComboBox>(form, name).Text; }
        public static bool Enabled(Form form, string name) { return Field<ComboBox>(form, name).Enabled; }

        /// <summary>줄 이름 라벨. 띠가 0번이라 하나 밀어 센다.</summary>
        public static string RowLabel(Form form, int index)
        {
            return form.Controls.OfType<Label>().ElementAt(index + 1).Text;
        }

        public static Button Button(Form form, int index)
        {
            return form.Controls.OfType<Button>().ElementAt(index);
        }

        public static void Pick(Form form, string name, string item)
        {
            ComboBox box = Field<ComboBox>(form, name);
            int at = box.Items.IndexOf(item);
            if (at < 0)
                throw new Exception("목록에 「" + item + "」이 없다: " +
                    string.Join(" / ", box.Items.Cast<object>().Select(o => o.ToString())));
            Report.Note("[「" + item + "」 고름]");
            box.SelectedIndex = at;
        }

        /// <summary>
        /// ⚠️ PerformClick은 창이 안 보이면 아무 일도 하지 않는다(CanSelect가 false).
        /// 조용히 통과하므로 창을 안 띄우는 확인에서는 Click 이벤트를 직접 올린다.
        /// </summary>
        public static void Click(Form form, string caption)
        {
            Button target = form.Controls.OfType<Button>().FirstOrDefault(b => b.Text == caption);
            if (target == null)
            {
                Report.Fail("「" + caption + "」 버튼이 없다. 있는 것: " +
                    string.Join(" / ", form.Controls.OfType<Button>().Select(b => b.Text)));
                return;
            }
            Report.Note("[「" + caption + "」 누름]");
            typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(target, new object[] { EventArgs.Empty });
        }

        public static void Refresh(Form form)
        {
            typeof(MainForm).GetMethod("RefreshStatus", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(form, null);
        }
    }
}
