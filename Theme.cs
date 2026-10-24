using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CS2PracticeHost
{
    /// <summary>
    /// 화면 색. 앱 아이콘의 색(남색 바탕, 금 = T 쪽, 파랑 = CT 쪽)을 CS2 메뉴처럼 어두운 화면에 깐다(2026-10-24 디렉터 OK).
    /// 금은 「지금 할 것」(고른 모드, ▶)에만, 파랑은 「고른 것」(타일·왼쪽 탭·링크 버튼)에만 쓴다 — 섞으면 눈이 갈 곳을 잃는다.
    /// </summary>
    internal static class Theme
    {
        public static readonly Color Top = Color.FromArgb(19, 38, 58);        // 바탕 위 (#13263A)
        public static readonly Color Bottom = Color.FromArgb(11, 21, 32);     // 바탕 아래 (#0B1520)
        public static readonly Color Panel = Color.FromArgb(20, 39, 58);      // 칸·콤보 (#14273A)
        public static readonly Color Well = Color.FromArgb(14, 28, 42);       // 타일 칸 (#0E1C2A)
        public static readonly Color Note = Color.FromArgb(16, 34, 51);       // 안내 상자 (#102233)
        public static readonly Color Line = Color.FromArgb(31, 58, 84);       // 구분선 (#1F3A54)
        public static readonly Color Edge = Color.FromArgb(42, 69, 96);       // 칸 테두리 (#2A4560)

        public static readonly Color Text = Color.FromArgb(232, 238, 244);    // #E8EEF4
        public static readonly Color SubText = Color.FromArgb(147, 168, 189); // #93A8BD — 바탕에서 4.5:1 넘는다
        public static readonly Color Dim = Color.FromArgb(111, 135, 158);     // #6F879E — 꺼진 버튼 글자

        public static readonly Color Gold = Color.FromArgb(242, 178, 51);     // #F2B233 — 아이콘의 금
        public static readonly Color OnGold = Color.FromArgb(26, 18, 5);      // 금 위 글자
        public static readonly Color Blue = Color.FromArgb(91, 155, 213);     // #5B9BD5 — 아이콘의 파랑
        public static readonly Color BlueSoft = Color.FromArgb(142, 190, 232);
        public static readonly Color BlueFill = Color.FromArgb(30, 61, 92);   // 고른 왼쪽 탭 (#1E3D5C)
        public static readonly Color LinkFill = Color.FromArgb(23, 50, 75);   // 링크 버튼 (#17324B)

        /// <summary>둥근 모서리 경로. 띠·버튼 모서리를 깎을 때 쓴다.</summary>
        public static GraphicsPath Rounded(Rectangle r, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>컨트롤 모서리를 둥글게 깎는다. 크기가 바뀌면 다시 불러야 한다.</summary>
        public static void Round(Control control, int radius)
        {
            using (GraphicsPath path = Rounded(new Rectangle(0, 0, control.Width, control.Height), radius))
                control.Region = new Region(path);
        }

        /// <summary>납작한 버튼 — 바탕·테두리·글자색만 정한다.</summary>
        public static void Flat(ButtonBase button, Color back, Color border, Color fore)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.BackColor = back;
            button.ForeColor = fore;
            button.FlatAppearance.BorderColor = border;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(back, 0.15f);
            button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(back, 0.05f);
            button.UseVisualStyleBackColor = false;
        }
    }
}
