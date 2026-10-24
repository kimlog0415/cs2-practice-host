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
        public const int TileWidth = 140;
        public const int TileHeight = 103;
        private const int PictureHeight = 79;

        public readonly GameMap Map;
        private Image _picture;
        private bool _selected;
        private bool _hover;

        public MapTile(GameMap map)
        {
            Map = map;
            Size = new Size(TileWidth, TileHeight);
            Margin = new Padding(3);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
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
            var box = new Rectangle(0, 0, Width - 1, Height - 1);

            if (_picture != null) PaintPicture(g, box);
            else PaintColor(g, box);

            // 고른 칸은 굵은 테두리. 색만으로 가르면 색을 못 가리는 사람이 모른다
            if (_selected)
                using (var pen = new Pen(Color.FromArgb(25, 118, 210), 4)) g.DrawRectangle(pen, 2, 2, Width - 5, Height - 5);
            else if (_hover)
                using (var pen = new Pen(Color.FromArgb(120, 25, 118, 210), 2)) g.DrawRectangle(pen, box);
            else
                using (var pen = new Pen(Color.FromArgb(40, 0, 0, 0))) g.DrawRectangle(pen, box);
        }

        /// <summary>그림이 아직 없다(받는 중, 인터넷 없음, 게임 파일에 없는 맵) — 색으로 채우고 이름을 가운데.</summary>
        private void PaintColor(Graphics g, Rectangle box)
        {
            using (var back = new SolidBrush(ColorOf(Map.Id))) g.FillRectangle(back, box);
            DrawName(g, box, Color.White, FontStyle.Bold);
        }

        private void PaintPicture(Graphics g, Rectangle box)
        {
            g.FillRectangle(Brushes.White, box);
            var picture = new Rectangle(0, 0, Width, PictureHeight);
            g.SetClip(picture);
            g.DrawImage(_picture, Fit(_picture.Size, picture));
            g.ResetClip();

            DrawName(g, new Rectangle(4, PictureHeight, Width - 8, Height - PictureHeight), Color.FromArgb(33, 37, 41), FontStyle.Regular);
        }

        private void DrawName(Graphics g, Rectangle area, Color color, FontStyle style)
        {
            using (var font = new Font(Strings.UiFont, 9, style))
                TextRenderer.DrawText(g, Map.TileName, font, area, color,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
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
