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
            Console.WriteLine("확인 대상: " + (mode == "all" ? "lang, modes, maps, state, update" : mode));
            Console.WriteLine("이 확인기 버전: " + Assembly.GetExecutingAssembly().GetName().Version);
            Console.WriteLine();

            Fake.KeepRealSettings();
            try
            {
                if (mode == "all" || mode == "lang") LangChecks.Run();
                if (mode == "all" || mode == "modes") ModeChecks.Run();
                if (mode == "all" || mode == "maps") MapChecks.Run();
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
            // 실제 배치를 그대로 흉내낸다 — 창작마당 폴더가 두 칸 위 steamapps 아래에 생겨야 한다
            string root = Path.Combine(Path.GetTempPath(), Root,
                "steamapps", "common", "Counter-Strike Global Offensive");
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

        /// <summary>
        /// 받아 둔 창작마당 맵 흉내. 폴더 이름이 구독 번호고 그 안의 vpk도 번호로 저장된다 —
        /// 실제 폴더가 `3447707473/3447707473_dir.vpk` 꼴이고 맵 이름은 vpk 목차 안에만 있다.
        /// 형식은 `번호=맵,맵:조각수`. 맵을 안 적으면 `ws_번호` 하나, `번호=`처럼 비우면 빈 파일(못 읽는 vpk).
        /// 조각수를 붙이면 쪼개진 맵(`_dir` + `_000`·`_001`)을 만든다.
        /// </summary>
        public static void WorkshopMaps(params string[] specs)
        {
            string root = Cs2Paths.WorkshopDir;
            if (root == null) throw new Exception("창작마당 폴더를 못 찾았다 — 설치 폴더가 먼저 잡혀야 한다");
            foreach (string spec in specs)
            {
                string[] parts = spec.Split(':');
                int chunks = parts.Length > 1 ? int.Parse(parts[1]) : 0;
                string[] named = parts[0].Split('=');
                string id = named[0];
                string[] maps = named.Length == 1 ? new[] { "ws_" + id }
                              : named[1].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                string dir = Path.Combine(root, id);
                Directory.CreateDirectory(dir);

                string tree = Path.Combine(dir, id + (chunks == 0 ? ".vpk" : "_dir.vpk"));
                if (maps.Length == 0) File.WriteAllText(tree, "");
                else File.WriteAllBytes(tree, VpkTree(maps));

                for (int i = 0; i < chunks; i++)
                    File.WriteAllText(Path.Combine(dir, id + "_" + i.ToString("000") + ".vpk"), "");
            }
        }

        /// <summary>Steam이 받은 폴더에 같이 두는 파일. 제작자의 작업 폴더 이름이 들어 있다.</summary>
        public static void PublishData(string id, string sourceFolder)
        {
            string dir = Path.Combine(Cs2Paths.WorkshopDir, id);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "publish_data.txt"),
                "\"publish_data\"\n{\n\t\"title\"\t\t\"" + id + "\"\n\t\"source_folder\"\t\t\"" + sourceFolder + "\"\n}\n");
        }

        /// <summary>
        /// 실제 창작마당 vpk와 같은 꼴의 목차(v2). 맵 옆에 하늘 상자(`maps/maps/prefabs/...`)와
        /// 재질 파일도 넣는다 — 실제 vpk에 늘 있고, 맵으로 세면 안 되는 것들이다.
        /// </summary>
        private static byte[] VpkTree(string[] maps)
        {
            var tree = new MemoryStream();
            var w = new BinaryWriter(tree);
            Action<string> text = s => { w.Write(System.Text.Encoding.UTF8.GetBytes(s)); w.Write((byte)0); };
            Action entry = () =>
            {
                w.Write(0u);                // CRC
                w.Write((ushort)2);         // 미리 실은 바이트 수 — 건너뛰는지 보려고 0이 아니게
                w.Write((ushort)0x7FFF);    // 조각 번호
                w.Write(0u); w.Write(0u);   // 위치·길이
                w.Write((ushort)0xFFFF);    // 끝 표시
                w.Write((ushort)0xABCD);    // 미리 실은 바이트
            };

            text("vpk");
            text("maps");
            foreach (string m in maps) { text(m); entry(); }
            text("");
            text("maps/maps/prefabs/" + maps[0]);
            text(maps[0] + "_skybox"); entry();
            text("");
            text("");
            text("vmat_c");
            text("maps/" + maps[0] + "/_vrf");
            text("wood"); entry();
            text("");
            text("");
            text("");
            w.Flush();

            var file = new MemoryStream();
            var h = new BinaryWriter(file);
            h.Write(0x55AA1234u);
            h.Write(2u);
            h.Write((uint)tree.Length);
            h.Write(new byte[16]);
            h.Write(tree.ToArray());
            h.Flush();
            return file.ToArray();
        }

        /// <summary>
        /// Steam에 묻지 않고 제목·태그를 미리 적어 둔다. 확인기가 바깥 요청을 내지 않게 하려는 것이다.
        /// 형식은 "번호|제목|태그,태그".
        /// </summary>
        public static void WorkshopInfoCache(params string[] rows)
        {
            var items = new List<WorkshopItem>();
            foreach (string row in rows)
            {
                string[] parts = row.Split('|');
                items.Add(new WorkshopItem
                {
                    Id = parts[0],
                    Title = parts[1],
                    Tags = parts.Length > 2 && parts[2].Length > 0 ? parts[2].Split(',') : new string[0],
                });
            }

            string path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "CS2PracticeHost", "workshop.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var stream = File.Create(path))
                new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(WorkshopCache))
                    .WriteObject(stream, new WorkshopCache { Items = items.ToArray() });

            WorkshopInfo.Forget();   // 다음 읽기가 방금 쓴 파일을 보게 한다
        }

        /// <summary>앞 확인이 만든 가짜 구독 폴더를 치운다. 안 치우면 뒤 확인의 개수가 흐트러진다.</summary>
        public static void ClearWorkshopMaps()
        {
            string root = Cs2Paths.WorkshopDir;
            if (root == null || !Directory.Exists(root)) return;
            foreach (string dir in Directory.GetDirectories(root))
            {
                try { Directory.Delete(dir, true); } catch (Exception) { }
            }
        }

        public static void ForgetWorkshopInfo()
        {
            try { File.Delete(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "CS2PracticeHost", "workshop.json")); }
            catch (Exception) { }
            WorkshopInfo.Forget();
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

        /// <summary>
        /// 링크는 Click이 아니라 LinkClicked로 걸려 있어 OnClick을 올려도 아무 일도 안 한다.
        /// 올릴 것을 잘못 고르면 「눌렀는데 통과」가 되므로 링크는 이 쪽으로 누른다.
        /// </summary>
        public static void ClickLink(LinkLabel link)
        {
            Report.Note("[「" + link.Text + "」 링크 누름]");
            typeof(LinkLabel).GetMethod("OnLinkClicked", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(link, new object[] { new LinkLabelLinkClickedEventArgs(new LinkLabel.Link()) });
        }

        public static void Refresh(Form form)
        {
            typeof(MainForm).GetMethod("RefreshStatus", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(form, null);
        }
    }
}
