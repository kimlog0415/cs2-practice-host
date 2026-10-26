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
            StarPutsMapFirst();
            KindMarks();
            TabsHaveTips();
            BrokenSettingsKept();
            WindowGrows();

            Fake.ClearWorkshopMaps();
            Fake.ForgetWorkshopInfo();
        }


        /// <summary>
        /// 함께 써 본 사람이 최대화가 안 돼 답답하다고 해서 창을 키울 수 있게 열었다(2026-10-25).
        /// 여는 것과 큰 화면에서 쓸만한 것은 다른 일이라, 늘어난 자리를 타일이 먹는지까지 본다 —
        /// 안 먹으면 가운데만 차고 양쪽이 비어 보인다. 창을 띄우지 않고 크기만 바꿔 칸 자리를 읽는다.
        /// </summary>
        private static void WindowGrows()
        {
            Report.Head("창을 키울 수 있나, 키우면 타일이 늘어나나");

            using (MainForm form = Open("casual", "de_dust2"))
            {
                Report.Is("최대화 버튼이 있나", form.MaximizeBox, true);
                Report.Is("크기를 바꿀 수 있나", form.FormBorderStyle.ToString(), "Sizable");
                Report.Is("최소 크기가 쓰던 크기인가", form.MinimumSize == form.Size, true);

                var host = Peek.Field<Panel>(form, "_tileHost");
                var area = Peek.Field<FlowLayoutPanel>(form, "_tileArea");
                var status = Peek.Field<Label>(form, "_status");
                var launch = Peek.Field<Button>(form, "_launch");
                var copyLink = Peek.Field<Button>(form, "_copyLink");

                int hostW = host.Width, hostH = host.Height, areaW = area.Width;
                int perRow = PerRow(area);
                int starBefore = StarSize(area.Controls.OfType<MapTile>().First());
                Report.Note("키우기 전 — 타일 칸 " + hostW + "×" + hostH + ", 한 줄에 " + perRow + "개");

                form.ClientSize = new Size(1600, 1000);
                Relayout(form);

                Report.Is("타일 칸이 넓어졌나", host.Width > hostW, true);
                Report.Is("타일 칸이 높아졌나", host.Height > hostH, true);
                Report.Is("안쪽 칸도 같이 넓어졌나", area.Width > areaW, true);
                Report.Is("한 줄에 더 들어가나 (" + perRow + " → " + PerRow(area) + ")",
                          PerRow(area) > perRow, true);

                // 넓힌 만큼 타일도 커진다. 다만 그림 원본이 264 폭이라 그보다는 안 키운다 —
                // 거기 닿으면 칸을 하나 더 만드는 쪽으로 간다(디렉터가 2번으로 골랐다)
                MapTile grown = area.Controls.OfType<MapTile>().First();
                Report.Note("키운 뒤 타일 " + grown.Width + "×" + grown.Height);
                Report.Is("타일이 커졌나", grown.Width > MapTile.TileWidth, true);
                Report.Is("원본 폭을 안 넘나", grown.Width <= MapTile.MaxTileWidth, true);
                Report.Is("비율이 유지되나",
                          Math.Abs(grown.Height - grown.Width * MapTile.TileHeight / MapTile.TileWidth) <= 1, true);
                Report.Is("한 줄이 꽉 차나 (남는 폭 " + Slack(host, area, grown) + "px)",
                          Slack(host, area, grown) < grown.Width, true);

                // 타일만 커지고 글자·별·딱지가 그대로면 큰 타일에서 글자만 작아 보인다(2026-10-26 백로그)
                Report.Is("글자·별이 같이 커지나 (배율 " + Scale(grown).ToString("0.00") + ")",
                          Scale(grown) > 1.3f, true);
                Report.Is("별 상자도 커졌나 (" + starBefore + " → " + StarSize(grown) + ")",
                          StarSize(grown) > starBefore, true);
                Report.Is("별이 타일 안에 있나",
                          StarBoxOf(grown).Right <= grown.Width && StarBoxOf(grown).Top >= 0, true);

                // ⚠️ 아주 넓히는 것은 창 크기로 못 본다 — 안 띄운 창에 화면보다 큰 크기를 주면
                // ClientSize는 그 값을 돌려주는데 실제 자리는 화면에 맞춰 잘려 앵커가 어긋난다.
                // 그래서 타일 칸을 직접 넓히고 셈만 다시 돌린다
                int wideCols = PerRow(area);
                int hostWide = host.Width;
                host.Width = 2600;
                Refit(form);
                MapTile capped = area.Controls.OfType<MapTile>().First();
                Report.Note("타일 칸 2600 — 타일 " + capped.Width + ", 한 줄 " + PerRow(area) + "개");
                Report.Is("상한에 닿으면 안 더 커지나", capped.Width <= MapTile.MaxTileWidth, true);
                Report.Is("대신 칸이 늘어나나 (" + wideCols + " → " + PerRow(area) + ")",
                          PerRow(area) > wideCols, true);

                host.Width = hostWide;   // 손으로 넓힌 것은 되돌린다 — 안 그러면 앵커 기준이 틀어진다
                Refit(form);

                form.ClientSize = new Size(1600, 1000);
                Relayout(form);

                Report.Is("상태 칸이 바닥에 붙어 있나",
                          form.ClientSize.Height - (status.Bottom) < 120, true);
                Report.Is("버튼 줄이 바닥에 붙어 있나", launch.Bottom < form.ClientSize.Height, true);
                Report.Is("▶ 버튼이 복사 버튼까지 늘어났나", launch.Right + 10 <= copyLink.Left, true);
                Report.Is("가운데가 비지 않나 (▶ 오른끝 " + launch.Right + " / 복사 왼끝 " + copyLink.Left + ")",
                          copyLink.Left - launch.Right <= 12, true);

                // 덮는 판은 만들 때 한 번 센 가운데라 창이 커지면 왼쪽으로 쏠린다
                var title = Peek.Field<Label>(form, "_updateTitle");
                Report.Is("새 판 알림 글이 가운데인가",
                          Math.Abs((title.Left + title.Width / 2) - form.ClientSize.Width / 2) <= 1, true);

                // 작은 화면으로 되돌려도 쓰던 대로여야 한다
                form.ClientSize = new Size(980, 770);
                Relayout(form);
                Report.Is("되돌리면 한 줄 개수도 돌아오나", PerRow(area), perRow);
                MapTile back = area.Controls.OfType<MapTile>().First();
                Report.Is("되돌리면 타일도 쓰던 크기 가까이 (" + back.Width + ")",
                          back.Width >= MapTile.TileWidth && back.Width < MapTile.TileWidth + 12, true);
            }
        }





        /// <summary>안 띄운 창은 Resize가 미덥지 않아 배치를 직접 돌린다.</summary>
        private static void Relayout(MainForm form)
        {
            typeof(MainForm).GetMethod("LayoutOnResize", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(form, null);
        }

        /// <summary>타일 칸 폭을 손으로 바꾼 뒤 타일 크기 셈을 다시 돌린다.</summary>
        private static void Refit(MainForm form)
        {
            typeof(MainForm).GetMethod("FitTileSize", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(form, null);
        }

        /// <summary>타일이 기본 크기의 몇 배인가 — 글자·별도 이 배율로 커져야 한다.</summary>
        private static float Scale(MapTile tile)
        {
            return (float)typeof(MapTile)
                .GetProperty("Scale", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(tile, null);
        }

        private static Rectangle StarBoxOf(MapTile tile)
        {
            return (Rectangle)typeof(MapTile)
                .GetProperty("StarBox", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(tile, null);
        }

        private static int StarSize(MapTile tile) { return StarBoxOf(tile).Width; }

        /// <summary>한 줄에서 남는 폭 — 작을수록 꽉 찬 것이다.</summary>
        private static int Slack(Panel host, FlowLayoutPanel area, MapTile tile)
        {
            int usable = host.ClientSize.Width - area.Padding.Horizontal;
            int per = tile.Width + 6;
            return usable - (usable / per) * per;
        }

        /// <summary>타일 한 줄에 몇 개 들어가나 — 맨 윗줄에 있는 타일 수를 센다.</summary>
        private static int PerRow(FlowLayoutPanel area)
        {
            MapTile[] tiles = area.Controls.OfType<MapTile>().ToArray();
            if (tiles.Length == 0) return 0;
            int top = tiles.Min(t => t.Top);
            return tiles.Count(t => t.Top == top);
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
                Press(botz, new Point(20, 60), MouseButtons.Left);
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
        private static object Call(Form form, string method, params object[] args)
        {
            return typeof(MainForm).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, args);
        }

        /// <summary>
        /// 별은 타일 고르기와 따로 눌린다. 별 자리를 누르면 그 맵이 칸 맨 앞으로 가고 고른 맵은 안 바뀐다.
        /// 별 자리가 아닌 곳을 누르면 평소처럼 고른다.
        /// </summary>
        private static void StarPutsMapFirst()
        {
            Report.Head("별을 누르면 그 맵이 맨 앞으로 — 고르기와 따로");

            using (MainForm form = Open("casual", "de_dust2"))
            {
                MapTile nuke = Tiles(form).First(t => t.Map.Id == "de_nuke");
                Report.Is("처음엔 맨 앞이 아니다", Tiles(form)[0].Map.Id != "de_nuke", true);

                // 별 자리(오른쪽 위)를 누른 것처럼
                Press(nuke, new Point(nuke.Width - 15, 15), MouseButtons.Left);
                Click(nuke);

                Report.Is("맨 앞", Tiles(form)[0].Map.Id, "de_nuke");
                Report.Is("별이 찼나", Tiles(form)[0].Favorite, true);
                Report.Is("고른 맵은 그대로", Peek.Combo(form, "_map"), "더스트2");
                Report.Is("설정에 남았나", string.Join(",", Settings.Load().Favorites ?? new string[0]), "de_nuke");

                // 별이 아닌 곳은 고르기
                MapTile again = Tiles(form)[0];
                Press(again, new Point(20, 60), MouseButtons.Left);
                Click(again);
                Report.Is("가운데를 누르면 고른다", Peek.Combo(form, "_map"), "뉴크");

                Call(form, "ToggleFavorite", "de_nuke");
                Report.Is("한 번 더 누르면 풀린다", Settings.Load().Favorites.Length, 0);
            }

            RightClickOnlyOpensMenu();
        }

        private static void Press(MapTile tile, Point at, MouseButtons button)
        {
            typeof(MapTile).GetField("_pressedAt", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(tile, at);
            typeof(MapTile).GetField("_pressedButton", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(tile, button);
        }

        /// <summary>
        /// Control의 Click은 오른쪽 버튼에도 온다. 오른쪽 클릭이 별 칸에 닿으면 별이 토글되고 타일을 다시 그려,
        /// 이어서 뜨는 종류 메뉴가 지워진 타일에 뜨다 앱이 죽었다(ObjectDisposedException, 2026-10-24 B 실기).
        /// </summary>
        private static void RightClickOnlyOpensMenu()
        {
            Report.Head("오른쪽 클릭은 별도 고르기도 안 건드리나");

            using (MainForm form = Open("casual", "de_dust2"))
            {
                MapTile nuke = Tiles(form).First(t => t.Map.Id == "de_nuke");
                Press(nuke, new Point(nuke.Width - 15, 15), MouseButtons.Right);
                Click(nuke);
                Report.Is("별 칸 오른쪽 클릭 — 별 그대로", Settings.Load().Favorites == null || Settings.Load().Favorites.Length == 0, true);
                Report.Is("타일이 안 지워졌나", nuke.IsDisposed, false);

                Press(nuke, new Point(20, 60), MouseButtons.Right);
                Click(nuke);
                Report.Is("가운데 오른쪽 클릭 — 고른 맵 그대로", Peek.Combo(form, "_map"), "더스트2");
            }
        }

        /// <summary>
        /// 껐다 켜니 종류 표시가 다 사라졌다(2026-10-24). 깨진 설정은 기본값으로 시작하되,
        /// 다음 저장이 덮기 전에 따로 떼어 둬야 사람이 쌓은 것을 되살릴 수 있다.
        /// </summary>
        private static void BrokenSettingsKept()
        {
            Report.Head("깨진 설정 파일은 따로 남기고, 저장은 바꿔치기로");

            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                       "CS2PracticeHost", "settings.json");
            string dir = Path.GetDirectoryName(path);
            foreach (string old in Directory.GetFiles(dir, "settings.broken-*.json")) File.Delete(old);

            File.WriteAllText(path, "{\"mode\":\"casual\",\"mapKinds\":[\"de_dust2=bomb2\"");   // 쓰다 끊긴 꼴
            Settings loaded = Settings.Load();
            Report.Is("기본값으로 시작하나", loaded.KindOf("de_dust2") == null, true);
            string[] kept = Directory.GetFiles(dir, "settings.broken-*.json");
            Report.Is("깨진 파일을 떼어 뒀나", kept.Length, 1);
            Report.Is("떼어 둔 파일에 표시가 남았나", kept.Length == 1 && File.ReadAllText(kept[0]).Contains("de_dust2=bomb2"), true);
            foreach (string old in kept) File.Delete(old);

            loaded.SetKind("de_dust2", "bomb1");
            loaded.Save();
            Report.Is("저장 뒤 다시 읽히나", Settings.Load().KindOf("de_dust2"), "bomb1");
            Report.Is("임시 파일이 안 남았나", File.Exists(path + ".tmp"), false);
        }

        /// <summary>오른쪽 클릭으로 단 종류는 진하게, 안 단 맵은 이름 앞부분으로 짐작해 흐리게.</summary>
        private static void KindMarks()
        {
            Report.Head("맵 종류 딱지 — 직접 단 것과 짐작한 것");

            using (MainForm form = Open("casual", "de_dust2"))
            {
                MapTile dust = Tiles(form).First(t => t.Map.Id == "de_dust2");
                Report.Is("de_ → 폭탄 짐작", dust.Kind + "/" + dust.KindGuessed, "bomb/True");

                Call(form, "SetMapKind", "de_dust2", MapKind.Bomb2);
                dust = Tiles(form).First(t => t.Map.Id == "de_dust2");
                Report.Is("직접 단 것", dust.Kind + "/" + dust.KindGuessed, "bomb2/False");
                Report.Is("설정에 남았나", Settings.Load().KindOf("de_dust2"), "bomb2");

                Call(form, "SetMapKind", "de_dust2", null);
                Report.Is("지우면 짐작으로", Tiles(form).First(t => t.Map.Id == "de_dust2").Kind, "bomb");

                ShowWorkshop(form);
                MapTile botz = Tiles(form).First(t => t.Map.WorkshopId == "3070596071");
                Report.Is("앞부분 없는 창작마당 맵(aim_botz)은 딱지 없음", botz.Kind == null, true);
            }
        }

        private static void TabsHaveTips()
        {
            Report.Head("모드 탭에 설명이 붙나");

            using (MainForm form = Open("casual", "de_dust2"))
            {
                var tips = Peek.Field<ToolTip>(form, "_tips");
                foreach (RadioButton tab in Tabs(form))
                    if (string.IsNullOrEmpty(tips.GetToolTip(tab))) Report.Fail("설명 없는 탭: " + tab.Text);
                Report.Is("러시", tips.GetToolTip(Tabs(form).First(t => t.Text == "러시")), "3대3 짧은 경기. 빈자리는 봇이 채운다");
            }
        }

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
