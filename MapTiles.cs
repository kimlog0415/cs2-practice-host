using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows.Forms;

namespace CS2PracticeHost
{
    /// <summary>
    /// 맵 하나를 그린 칸. 누르면 그 맵을 고른 것이다. 위에 그림, 아래에 이름.
    /// 기본 맵 그림은 사용자 PC에 깔린 게임 파일에서 꺼내 온 것이고, 창작마당 그림은 제작자가 올린 대표 그림이다.
    /// 그림이 아직 없으면 맵마다 정해진 색으로 채운다.
    /// </summary>
    internal sealed class MapTile : Control
    {
        public const int TileWidth = 156;
        public const int TileHeight = 120;

        /// <summary>
        /// 타일을 이보다 넓게 늘리면 그림이 흐려진다 — 썸네일을 264×148로 받아 두기 때문이다
        /// (기본 맵은 게임 파일에서 그 크기로 줄여 저장, 창작마당은 Steam에 그 크기로 요청).
        /// 창이 더 넓어지면 타일을 키우는 대신 한 줄에 하나 더 넣는다.
        /// </summary>
        public const int MaxTileWidth = 264;

        /// <summary>가장 작은 창에서 한 줄에 들어가는 수. 거기서는 지금 보던 모양 그대로여야 한다.</summary>
        public const int BaseColumns = 5;

        public readonly GameMap Map;
        private Image _picture;
        private bool _selected;
        private bool _hover;
        private bool _favorite;
        private string _kind;
        private bool _kindGuessed;
        private Point _pressedAt;
        private MouseButtons _pressedButton;

        /// <summary>오른쪽 위 별을 눌렀다. 별은 타일 고르기와 따로 눌린다 — 별을 눌러도 그 맵이 골라지지 않는다.</summary>
        public event EventHandler StarClicked;

        private Rectangle StarBox { get { return new Rectangle(Width - 30, 4, 26, 26); } }

        public bool Favorite
        {
            get { return _favorite; }
            set { if (_favorite != value) { _favorite = value; Invalidate(); } }
        }

        /// <summary>
        /// 맵 종류 딱지. kind는 MapKind 값(없으면 null). guessed면 사용자가 단 게 아니라 이름 앞부분으로 짐작한 것 — 흐리게 그린다.
        /// </summary>
        public void SetKind(string kind, bool guessed)
        {
            _kind = kind;
            _kindGuessed = guessed;
            Invalidate();
        }

        public string Kind { get { return _kind; } }
        public bool KindGuessed { get { return _kindGuessed; } }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _pressedAt = e.Location;
            _pressedButton = e.Button;
            base.OnMouseDown(e);
        }

        protected override void OnClick(EventArgs e)
        {
            // Control의 Click은 오른쪽 버튼에도 온다. 오른쪽 클릭이 별 칸에 닿으면 별이 토글되고, 그 바람에
            // 타일을 다시 그려 오른쪽 클릭 메뉴가 지워진 타일에 뜨다 앱이 죽었다(B 실기). 오른쪽은 메뉴만 쓴다
            if (_pressedButton != MouseButtons.Left) return;

            if (StarBox.Contains(_pressedAt))
            {
                if (StarClicked != null) StarClicked(this, EventArgs.Empty);
                return;   // 별만 누른 것 — 고르기(Click)로 넘기지 않는다
            }
            base.OnClick(e);
        }

        public MapTile(GameMap map)
        {
            Map = map;
            Size = new Size(TileWidth, TileHeight);
            Margin = new Padding(3);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Theme.Round(this, 6);
            _picture = Thumbs.Of(map);
        }

        public bool Selected
        {
            get { return _selected; }
            set { if (_selected != value) { _selected = value; Invalidate(); } }
        }

        /// <summary>그림을 늦게 받았을 때 그 자리에서 갈아 끼운다.</summary>
        public void ReloadPicture()
        {
            _picture = Thumbs.Of(Map);
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            var box = new Rectangle(0, 0, Width, Height);

            // 그림을 칸 가득 깔고 아래를 어둡게 덮어 이름을 그 위에 쓴다. 그림이 아직 없으면 맵마다 정한 색
            if (_picture != null) g.DrawImage(_picture, Fit(_picture.Size, box));
            else using (var back = new SolidBrush(ColorOf(Map.Id))) g.FillRectangle(back, box);

            var shade = new Rectangle(0, Height - 46, Width, 46);
            using (var fade = new LinearGradientBrush(shade, Color.FromArgb(0, Theme.Bottom), Color.FromArgb(235, Theme.Bottom),
                                                      LinearGradientMode.Vertical))
                g.FillRectangle(fade, shade);

            // 왼쪽 아래 종류 딱지, 그 뒤에 이름
            int nameX = 8;
            if (_kind != null) nameX += DrawKind(g, new Point(8, Height - 24)) + 5;
            using (var font = new Font(Strings.UiFont, 9, _selected ? FontStyle.Bold : FontStyle.Regular))
                TextRenderer.DrawText(g, Map.TileName, font, new Rectangle(nameX, Height - 26, Width - nameX - 8, 22), Theme.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                    TextFormatFlags.SingleLine);

            DrawStar(g);

            // 고른 칸은 굵은 파란 테두리(오른쪽 위는 별 자리라 체크는 없다). 굵기가 달라 색을 못 가려도 보인다
            if (_selected)
                using (var ring = new Pen(Theme.Blue, 3)) g.DrawRectangle(ring, 1.5f, 1.5f, Width - 3, Height - 3);
            else
            {
                using (var edge = new Pen(_hover ? Theme.BlueSoft : Theme.Line, _hover ? 2 : 1))
                    g.DrawRectangle(edge, 0, 0, Width - 1, Height - 1);
            }
        }

        /// <summary>
        /// 별. 빈 별은 마우스를 올린 타일에만 또렷하게, 평소엔 흐리게 — 그림을 가리지 않게.
        /// 그림이 밝아도 보이게 어두운 동그라미를 깔고 그린다.
        /// </summary>
        private void DrawStar(Graphics g)
        {
            Rectangle box = StarBox;
            if (!_favorite && !_hover) { DrawStarShape(g, box, Color.FromArgb(150, Theme.Text), false, 90); return; }
            DrawStarShape(g, box, _favorite ? Theme.Gold : Theme.Text, _favorite, 170);
        }

        private static void DrawStarShape(Graphics g, Rectangle box, Color color, bool filled, int backAlpha)
        {
            using (var back = new SolidBrush(Color.FromArgb(backAlpha, Theme.Bottom))) g.FillEllipse(back, box);
            float cx = box.X + box.Width / 2f, cy = box.Y + box.Height / 2f + 0.5f, outer = 8.5f, inner = 3.6f;
            var points = new PointF[10];
            for (int i = 0; i < 10; i++)
            {
                double angle = -Math.PI / 2 + i * Math.PI / 5;
                float r = i % 2 == 0 ? outer : inner;
                points[i] = new PointF(cx + (float)(Math.Cos(angle) * r), cy + (float)(Math.Sin(angle) * r));
            }
            if (filled) using (var fill = new SolidBrush(color)) g.FillPolygon(fill, points);
            else using (var line = new Pen(color, 1.4f)) g.DrawPolygon(line, points);
        }

        /// <summary>
        /// 종류 딱지 — 폭탄은 폭탄 모양 + 지점 수(1·2, 짐작이면 숫자 없음), 인질은 사람 모양, 기타는 점 셋.
        /// 짐작한 것은 흐리게. Valve 그림은 쓰지 않고 직접 그린다. 돌려주는 값은 그린 폭.
        /// </summary>
        private int DrawKind(Graphics g, Point at)
        {
            int alpha = _kindGuessed ? 120 : 255;
            Color ink = Color.FromArgb(alpha, _kind == MapKind.Hostage ? Theme.BlueSoft : Theme.Gold);
            var icon = new Rectangle(at.X, at.Y, 16, 16);
            using (var pen = new Pen(ink, 1.6f))
            using (var fill = new SolidBrush(ink))
            {
                if (MapKind.IsBomb(_kind))
                {
                    g.FillEllipse(fill, icon.X + 1, icon.Y + 4, 11, 11);                       // 몸통
                    g.DrawLine(pen, icon.X + 9, icon.Y + 5, icon.X + 12, icon.Y + 1);          // 심지
                    g.FillEllipse(fill, icon.X + 11, icon.Y - 1, 4, 4);                        // 불꽃
                    string sites = _kind == MapKind.Bomb1 ? "1" : _kind == MapKind.Bomb2 ? "2" : null;
                    if (sites == null) return 16;
                    using (var font = new Font(Strings.UiFont, 8, FontStyle.Bold))
                        TextRenderer.DrawText(g, sites, font, new Point(icon.Right, icon.Y + 1), ink);
                    return 24;
                }
                if (_kind == MapKind.Hostage)
                {
                    g.FillEllipse(fill, icon.X + 5, icon.Y + 1, 6, 6);                         // 머리
                    g.FillPie(fill, icon.X + 2, icon.Y + 8, 12, 14, 180, 180);                 // 어깨
                    return 16;
                }
                for (int i = 0; i < 3; i++) g.FillEllipse(fill, icon.X + 1 + i * 5, icon.Y + 7, 3, 3);   // 기타
                return 16;
            }
        }

        /// <summary>그림 비율을 지킨 채 칸을 채운다(넘치는 쪽은 잘린다). 늘이면 맵이 찌그러져 보인다.</summary>
        private static Rectangle Fit(Size image, Rectangle area)
        {
            float scale = Math.Max((float)area.Width / image.Width, (float)area.Height / image.Height);
            int w = (int)(image.Width * scale), h = (int)(image.Height * scale);
            return new Rectangle(area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2, w, h);
        }

        private static readonly Color[] Palette =
        {
            Color.FromArgb(94, 108, 132), Color.FromArgb(121, 99, 77), Color.FromArgb(70, 120, 110),
            Color.FromArgb(128, 86, 110), Color.FromArgb(84, 104, 150), Color.FromArgb(138, 112, 64),
            Color.FromArgb(96, 124, 82), Color.FromArgb(118, 82, 82),
        };

        /// <summary>맵마다 늘 같은 색. 켤 때마다 바뀌면 색으로 맵을 기억할 수가 없다.</summary>
        private static Color ColorOf(string id)
        {
            int sum = 0;
            foreach (char c in id) sum = (sum * 31 + c) & 0x7FFFFFFF;
            return Palette[sum % Palette.Length];
        }
    }

    /// <summary>
    /// 맵 종류 딱지의 값. 설정 파일에 이 글자 그대로 남는다 — 바꾸면 사용자가 단 표시가 사라진다.
    /// Steam 정보엔 이런 칸이 없고 맵 파일 안의 폭탄 지점도 압축돼 못 읽어서, 사람이 단다(2026-10-24 디렉터 결정).
    /// </summary>
    internal static class MapKind
    {
        public const string Bomb1 = "bomb1";
        public const string Bomb2 = "bomb2";
        public const string Hostage = "hostage";
        public const string Other = "other";

        /// <summary>짐작으로만 쓰는 값 — 폭탄 맵인 건 알지만 지점 수는 모른다.</summary>
        public const string Bomb = "bomb";

        public static bool IsBomb(string kind) { return kind == Bomb || kind == Bomb1 || kind == Bomb2; }

        /// <summary>
        /// 표시를 안 단 맵은 이름 앞부분으로 짐작한다 — `de_`는 폭탄, `cs_`는 인질. 관례일 뿐이라
        /// 앞부분이 없는 창작마당 맵(`panama`·`prestige` 같은)은 짐작하지 않는다.
        /// </summary>
        public static string Guess(GameMap map)
        {
            string name = (map.IsWorkshop ? map.WorkshopMapName : map.Id) ?? "";
            if (name.StartsWith("de_", StringComparison.OrdinalIgnoreCase)) return Bomb;
            if (name.StartsWith("cs_", StringComparison.OrdinalIgnoreCase)) return Hostage;
            return null;
        }
    }

    /// <summary>
    /// 타일 그림을 `%APPDATA%\CS2PracticeHost\thumbs`에 모아 둔다. 한 번 만들면 다시 안 만든다.
    /// - 창작마당: Steam이 그림을 두는 주소에서 받는다. 보내는 것은 없다(주소에 든 것은 그림 번호다)
    /// - 기본 맵: 사용자 PC에 깔린 CS2의 `pak01`에서 메뉴용 맵 그림을 꺼내 작게 줄여 둔다.
    ///   앱에 Valve 그림을 넣어 배포하지 않는다 — 그 PC의 게임 파일을 그 PC에서 읽기만 한다
    /// </summary>
    internal static class Thumbs
    {
        private static readonly Dictionary<string, Image> Loaded = new Dictionary<string, Image>();

        private static string Dir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "CS2PracticeHost", "thumbs");
            }
        }

        private static string PathOf(string id) { return Path.Combine(Dir, id + ".jpg"); }
        private static string BuiltInPathOf(string mapId) { return Path.Combine(Dir, "builtin", mapId + ".png"); }

        /// <summary>이 맵의 타일 그림. 없으면 null — 타일은 색과 이름만 보여 준다.</summary>
        public static Image Of(GameMap map)
        {
            return map.IsWorkshop ? Load(PathOf(map.WorkshopId)) : Load(BuiltInPathOf(map.Id));
        }

        private static Image Load(string path)
        {
            Image image;
            if (Loaded.TryGetValue(path, out image)) return image;

            if (!File.Exists(path)) return null;
            try
            {
                // 파일을 연 채로 두지 않게 메모리로 읽어 들인다
                using (var stream = new MemoryStream(File.ReadAllBytes(path)))
                using (var read = Image.FromStream(stream))
                    image = new Bitmap(read);
            }
            catch (Exception) { return null; }   // 깨진 파일이면 이름만 보인다

            Loaded[path] = image;
            return image;
        }

        /// <summary>아직 없는 그림만 받는다. 돌려주는 값은 「새로 받은 게 있나」. 뒤에서 부른다.</summary>
        public static bool FetchMissing(IEnumerable<string> ids)
        {
            bool gained = false;
            foreach (string id in ids.Distinct())
            {
                if (File.Exists(PathOf(id))) continue;
                WorkshopItem item = WorkshopInfo.Of(id);
                if (item == null || !IsSteamImage(item.PreviewUrl)) continue;

                try
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    Directory.CreateDirectory(Dir);
                    var request = (HttpWebRequest)WebRequest.Create(Sized(item.PreviewUrl));
                    request.UserAgent = "CS2PracticeHost";
                    request.Timeout = 8000;
                    request.ReadWriteTimeout = 8000;

                    string temp = PathOf(id) + ".part";
                    using (WebResponse response = request.GetResponse())
                    using (Stream from = response.GetResponseStream())
                    using (FileStream to = File.Create(temp))
                        from.CopyTo(to);

                    // 받다가 끊긴 파일이 그림으로 남지 않게, 다 받은 뒤에야 제 이름을 붙인다
                    File.Move(temp, PathOf(id));
                    gained = true;
                }
                catch (Exception) { }   // 인터넷이 없으면 이름만 보인다. 다음에 켤 때 다시 받는다
            }
            return gained;
        }

        /// <summary>메뉴용 맵 그림이 든 자리. 게임이 맵 고르는 화면에 쓰는 360p 판이다.</summary>
        private const string ScreenshotDir = "panorama/images/map_icons/screenshots/360p";

        /// <summary>
        /// 기본 맵 그림 중 아직 없는 것만 게임 파일에서 꺼낸다. 돌려주는 값은 「새로 만든 게 있나」. 뒤에서 부른다.
        /// `pak01_dir.vpk` 목차가 수 MB라 켤 때마다 읽지 않는다 — 다 있으면 목차를 열지도 않는다.
        /// </summary>
        public static bool FetchBuiltInMissing(IEnumerable<string> mapIds)
        {
            string[] missing = mapIds.Distinct().Where(id => !File.Exists(BuiltInPathOf(id))).ToArray();
            if (missing.Length == 0 || Cs2Paths.InstallDir == null) return false;

            string pak = Path.Combine(Cs2Paths.CsgoDir, "pak01_dir.vpk");
            Dictionary<string, VpkEntry> found;
            try { found = FindScreenshots(pak, missing); }
            catch (Exception) { return false; }   // 게임 업데이트 중이거나 형식이 바뀌었으면 색으로 남는다

            bool gained = false;
            foreach (KeyValuePair<string, VpkEntry> hit in found)
            {
                try
                {
                    byte[] png = PngInside(ReadEntry(pak, hit.Value));
                    if (png == null) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(BuiltInPathOf(hit.Key)));
                    using (var stream = new MemoryStream(png))
                    using (var full = Image.FromStream(stream))
                    using (var small = new Bitmap(264, 148))
                    {
                        using (Graphics g = Graphics.FromImage(small))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.DrawImage(full, 0, 0, small.Width, small.Height);
                        }
                        small.Save(BuiltInPathOf(hit.Key), System.Drawing.Imaging.ImageFormat.Png);
                    }
                    gained = true;
                }
                catch (Exception) { }
            }
            return gained;
        }

        private struct VpkEntry { public ushort Archive; public uint Offset; public uint Length; public byte[] Preload; public long TreeEnd; }

        /// <summary>목차에서 「맵 이름_png.vtex_c」만 골라 낸다. 목차 꼴은 WorkshopMaps가 읽는 것과 같다.</summary>
        private static Dictionary<string, VpkEntry> FindScreenshots(string pak, string[] mapIds)
        {
            var wanted = new HashSet<string>(mapIds.Select(id => id + "_png"));
            var found = new Dictionary<string, VpkEntry>();
            using (var r = new BinaryReader(new BufferedStream(File.OpenRead(pak), 1 << 16)))
            {
                if (r.ReadUInt32() != 0x55AA1234 || r.ReadUInt32() != 2) return found;
                uint treeSize = r.ReadUInt32();
                r.ReadBytes(16);
                long treeEnd = 28 + treeSize;

                string ext;
                while ((ext = ReadText(r)).Length > 0)
                {
                    string path;
                    while ((path = ReadText(r)).Length > 0)
                    {
                        bool here = ext == "vtex_c" && path == ScreenshotDir;
                        string file;
                        while ((file = ReadText(r)).Length > 0)
                        {
                            r.ReadUInt32();                                  // CRC
                            ushort preload = r.ReadUInt16();
                            var entry = new VpkEntry
                            {
                                Archive = r.ReadUInt16(), Offset = r.ReadUInt32(), Length = r.ReadUInt32(), TreeEnd = treeEnd,
                            };
                            r.ReadUInt16();                                  // 끝 표시
                            entry.Preload = r.ReadBytes(preload);
                            if (here && wanted.Contains(file))
                                found[file.Substring(0, file.Length - "_png".Length)] = entry;
                        }
                    }
                }
            }
            return found;
        }

        private static byte[] ReadEntry(string pak, VpkEntry e)
        {
            // 0x7FFF는 「목차 파일 안, 목차 뒤에 있다」, 나머지는 `pak01_<번호>.vpk` 조각
            string file = e.Archive == 0x7FFF ? pak : pak.Replace("_dir.vpk", "_" + e.Archive.ToString("000") + ".vpk");
            long at = e.Archive == 0x7FFF ? e.TreeEnd + e.Offset : e.Offset;
            var data = new byte[e.Preload.Length + e.Length];
            Buffer.BlockCopy(e.Preload, 0, data, 0, e.Preload.Length);
            using (var f = File.OpenRead(file))
            {
                f.Seek(at, SeekOrigin.Begin);
                int read = 0;
                while (read < e.Length)
                {
                    int n = f.Read(data, e.Preload.Length + read, (int)e.Length - read);
                    if (n <= 0) throw new EndOfStreamException();
                    read += n;
                }
            }
            return data;
        }

        /// <summary>메뉴용 그림은 vtex_c 안에 PNG가 통째로 들어 있다(텍스처 머리 뒤). 그 PNG만 잘라 낸다.</summary>
        private static byte[] PngInside(byte[] vtex)
        {
            byte[] signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            for (int i = 0; i + signature.Length <= vtex.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < signature.Length && match; j++) match = vtex[i + j] == signature[j];
                if (!match) continue;
                var png = new byte[vtex.Length - i];
                Buffer.BlockCopy(vtex, i, png, 0, png.Length);
                return png;   // 뒤에 남는 바이트가 있어도 Image.FromStream은 IEND에서 멈춘다
            }
            return null;
        }

        private static string ReadText(BinaryReader r)
        {
            var bytes = new List<byte>();
            byte b;
            while ((b = r.ReadByte()) != 0) bytes.Add(b);
            return System.Text.Encoding.UTF8.GetString(bytes.ToArray());
        }

        /// <summary>Steam이 그림을 두는 곳만 받는다. 캐시에 엉뚱한 주소가 들어 있어도 따라가지 않게.</summary>
        private static bool IsSteamImage(string url)
        {
            Uri uri;
            return url != null && Uri.TryCreate(url, UriKind.Absolute, out uri) && uri.Scheme == Uri.UriSchemeHttps &&
                   (uri.Host == "images.steamusercontent.com" || uri.Host.EndsWith(".steamusercontent.com"));
        }

        /// <summary>원본은 몇 MB씩 된다. Steam 그림 서버에 타일에 맞는 크기로 줄여 달라고 한다.</summary>
        private static string Sized(string url)
        {
            return url + (url.Contains("?") ? "&" : "?") + "imw=264&imh=148&ima=fit&impolicy=Letterbox";
        }
    }
}
