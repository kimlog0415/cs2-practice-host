using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace CS2PracticeHost
{
    /// <summary>
    /// 1.4.0 화면 — 모드는 위쪽 탭, 맵은 타일. 탭·타일은 보이지 않는 콤보(_mode·_map)를 바꾸는 쪽이라,
    /// 누르면 콤보가 따라 바뀌고 저장·F10 파일까지 가는지를 본다. 창을 안 띄우니 눌러서 보이는 모양은 실기로 본다.
    /// </summary>
    internal static class TileChecks
    {
        public static void Run()
        {
            Fake.Install("de_debris");
            Fake.ClearWorkshopMaps();
            Fake.WorkshopMaps("3070596071=aim_botz", "555=de_big_wingman");
            Fake.WorkshopInfoCache("3070596071|Aim Botz|Classic", "555|Big Wingman Map|Wingman");

            TabsFollowModes();
            TilesMatchMaps();
            TileClickPicksMap();
            TabClickSwitchesMode();
            WorkshopHeaderHidesWhenEmpty();
            PictureShowsWhenSaved();

            Fake.ClearWorkshopMaps();
            Fake.ForgetWorkshopInfo();
        }

        private static MainForm Open(string mode, string map)
        {
            Strings.Init("ko");
            Fake.WriteSettings("{\"mode\":\"" + mode + "\",\"map\":\"" + map + "\",\"bots\":0," +
                               "\"level\":\"1\",\"team\":\"any\",\"installDir\":null,\"lang\":\"ko\"}");
            return new MainForm(Settings.Load());
        }

        private static RadioButton[] Tabs(Form form)
        {
            return Peek.Field<Panel>(form, "_tabs").Controls.OfType<RadioButton>().ToArray();
        }

        /// <summary>지금 타일 칸에 보이는 것 — 왼쪽 탭이 고른 한쪽(기본 맵 또는 창작마당)만이다.</summary>
        private static MapTile[] Tiles(Form form)
        {
            return Peek.Field<FlowLayoutPanel>(form, "_tileArea").Controls.OfType<MapTile>().ToArray();
        }

        private static void ShowWorkshop(Form form) { Click(Peek.Field<RadioButton>(form, "_kindWorkshop")); }
        private static void ShowBuiltIn(Form form) { Click(Peek.Field<RadioButton>(form, "_kindBuiltIn")); }

        private static void Click(Control control)
        {
            typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(control, new object[] { EventArgs.Empty });
        }

        private static void TabsFollowModes()
        {
            Report.Head("모드 탭이 게임 순서대로 있나, 고른 탭이 켜져 있나");

            using (MainForm form = Open("wingman", "de_debris"))
            {
                RadioButton[] tabs = Tabs(form);
                Report.Is("탭 수", tabs.Length, GameData.Modes.Length);
                Report.Is("탭 이름", string.Join(" / ", tabs.Select(t => t.Text)),
                          string.Join(" / ", GameData.Modes.Select(m => m.Ko)));
                Report.Is("켜진 탭", string.Join(",", tabs.Where(t => t.Checked).Select(t => t.Text)), "윙맨");

                Peek.Click(form, "EN");
                Report.Is("영어 탭 이름", Tabs(form)[0].Text, "Casual");
                Report.Is("언어를 바꿔도 켜진 탭", string.Join(",", Tabs(form).Where(t => t.Checked).Select(t => t.Text)), "Wingman");
                Peek.Click(form, "한");
            }
        }

        private static void TilesMatchMaps()
        {
            Report.Head("타일이 맵 목록과 같은가");

            using (MainForm form = Open("casual", "de_dust2"))
            {
                ComboBox map = Peek.Field<ComboBox>(form, "_map");
                MapTile[] tiles = Tiles(form);
                Report.Is("처음엔 기본 맵 칸 (고른 맵이 기본 맵)", tiles.All(t => !t.Map.IsWorkshop), true);
                Report.Is("고른 타일", string.Join(",", tiles.Where(t => t.Selected).Select(t => t.Map.Id)), "de_dust2");
                int builtIn = tiles.Length;

                ShowWorkshop(form);
                tiles = Tiles(form);
                Report.Is("창작마당 칸으로 바뀌나", tiles.All(t => t.Map.IsWorkshop), true);
                Report.Is("두 칸을 합치면 맵 수", builtIn + tiles.Length, map.Items.Count);
                Report.Is("창작마당 타일 이름은 「창작마당 · 」 없이", tiles.First().Map.TileName, "Aim Botz");
                Report.Is("윙맨 태그 맵은 캐주얼에 없나", tiles.Any(t => t.Map.WorkshopId == "555"), false);
                Report.Is("고른 맵은 그대로", Peek.Combo(form, "_map"), "더스트2");
                Report.Has("탭에 맵 수", Peek.Field<RadioButton>(form, "_kindWorkshop").Text, "1");
            }
        }

        private static void TileClickPicksMap()
        {
            Report.Head("타일을 누르면 그 맵을 고르고 F10 파일까지 가나");

            using (MainForm form = Open("casual", "de_dust2"))
            {
                ShowWorkshop(form);
                MapTile botz = Tiles(form).First(t => t.Map.WorkshopId == "3070596071");
                Click(botz);

                Report.Is("고른 맵", Peek.Combo(form, "_map"), "창작마당 · Aim Botz");
                Report.Is("켜진 타일", string.Join(",", Tiles(form).Where(t => t.Selected).Select(t => t.Map.Id)),
                          "workshop/3070596071/aim_botz");
                Report.Is("설정에 남았나", Settings.Load().Map, "workshop/3070596071/aim_botz");

                string change = Path.Combine(Cs2Paths.CfgDir, "cs2host_change.cfg");
                Report.Is("F10 파일", File.ReadAllLines(change).Last(l => l.Length > 0),
                          "changelevel aim_botz customgamemode=3070596071 nomapvalidation=true");
            }
        }

        private static void TabClickSwitchesMode()
        {
            Report.Head("탭을 누르면 모드가 바뀌고 그 모드의 맵 타일로 바뀌나");

            using (MainForm form = Open("casual", "de_dust2"))
            {
                RadioButton wingman = Tabs(form).First(t => t.Text == "윙맨");
                Click(wingman);

                Report.Is("고른 모드", Peek.Combo(form, "_mode"), "윙맨");
                Report.Is("켜진 탭", string.Join(",", Tabs(form).Where(t => t.Checked).Select(t => t.Text)), "윙맨");
                ShowWorkshop(form);
                Report.Is("윙맨 태그 맵이 떴나", Tiles(form).Any(t => t.Map.WorkshopId == "555"), true);
                Report.Is("Classic 맵은 빠졌나", Tiles(form).Any(t => t.Map.WorkshopId == "3070596071"), false);

                Click(Tabs(form).First(t => t.Text == "탈환"));
                Report.Is("탈환이면 봇 칸이 잠기나", Peek.Enabled(form, "_bots"), false);
            }
        }

        private static void WorkshopHeaderHidesWhenEmpty()
        {
            Report.Head("창작마당 맵이 없는 모드는 창작마당 탭이 꺼지나");

            using (MainForm form = Open("retake", "de_dust2"))
            {
                Report.Is("탈환 — 창작마당 탭", Peek.Field<RadioButton>(form, "_kindWorkshop").Enabled, false);
                ShowWorkshop(form);
                Report.Is("눌러도 기본 맵 칸에 남나", Tiles(form).All(t => !t.Map.IsWorkshop), true);
            }
        }

        /// <summary>받아 둔 그림이 있으면 타일에 붙는다. 바깥 요청 없이 보려고 그림 파일을 미리 놓는다.</summary>
        private static void PictureShowsWhenSaved()
        {
            Report.Head("받아 둔 그림이 타일에 붙나 (창작마당·기본 맵)");

            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                      "CS2PracticeHost", "thumbs");
            // 실제 사용자 폴더라 이미 있으면 건드리지 않고, 없을 때만 놓았다가 치운다
            string[] files = { Path.Combine(dir, "3070596071.jpg"), Path.Combine(dir, "builtin", "de_dust2.png") };
            bool[] existed = files.Select(File.Exists).ToArray();
            for (int i = 0; i < files.Length; i++)
            {
                if (existed[i]) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(files[i]));
                using (var bmp = new Bitmap(16, 9)) bmp.Save(files[i]);
            }

            FieldInfo picture = typeof(MapTile).GetField("_picture", BindingFlags.Instance | BindingFlags.NonPublic);
            try
            {
                using (MainForm form = Open("casual", "de_dust2"))
                {
                    MapTile dust = Tiles(form).First(t => t.Map.Id == "de_dust2");
                    Report.Is("기본 맵 그림", picture.GetValue(dust) != null, true);

                    ShowWorkshop(form);
                    MapTile botz = Tiles(form).First(t => t.Map.WorkshopId == "3070596071");
                    Report.Is("창작마당 그림", picture.GetValue(botz) != null, true);
                }
            }
            finally
            {
                for (int i = 0; i < files.Length; i++)
                    if (!existed[i]) try { File.Delete(files[i]); } catch (Exception) { }
            }
        }
    }
}
