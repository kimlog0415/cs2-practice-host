using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace CS2PracticeHost
{
    /// <summary>
    /// 「처음 보는 눈」용 화면 캡처(공통 규칙 4번, 2026-11-29). 확인이 아니라 사람이 볼 그림을 남긴다 —
    /// 메인 창(메모·찾기)·오른쪽 클릭 메뉴·메모 창을 언어마다. 확인기 전용 폴더의 가짜 설정으로 그린다.
    /// `Checks.exe shots <폴더>`
    /// </summary>
    internal static class Shots
    {
        public static void Run(string outDir)
        {
            outDir = outDir ?? Path.Combine(Path.GetTempPath(), "cs2host-shots");
            Directory.CreateDirectory(outDir);
            Fake.Install("de_debris");
            Fake.ClearWorkshopMaps();
            Fake.WorkshopMaps("3070596071=aim_botz", "555=de_big_wingman");
            Fake.WorkshopInfoCache("3070596071|Aim Botz|Classic", "555|Big Wingman Map|Wingman");

            foreach (string lang in new[] { "ko", "en" })
            {
                Strings.Init(lang);
                Fake.WriteSettings("{\"mode\":\"casual\",\"map\":\"de_dust2\",\"bots\":0,\"level\":\"1\",\"team\":\"any\"," +
                                   "\"installDir\":null,\"lang\":\"" + lang + "\"," +
                                   "\"mapNotes\":[\"de_dust2=" + (lang == "ko" ? "친구랑 5대5, B 연습" : "5v5 with friends, B site") + "\"," +
                                   "\"de_mirage=" + (lang == "ko" ? "아주 긴 메모가 들어가면 타일 위에서 어떻게 잘리는지 보려고 쓴 줄" : "a very long note to see how it gets cut on the tile") + "\"]," +
                                   "\"mapKinds\":[\"de_dust2=bomb2\"]}");
                using (var form = new MainForm(Settings.Load()))
                {
                    form.StartPosition = FormStartPosition.Manual;
                    form.Location = new Point(-4000, 0);
                    form.Show();
                    Application.DoEvents();
                    Save(form, Path.Combine(outDir, lang + "-1-main.png"));

                    Field<TextBox>(form, "_search").Text = lang == "ko" ? "연습" : "friends";
                    Application.DoEvents();
                    Save(form, Path.Combine(outDir, lang + "-2-search.png"));

                    Field<TextBox>(form, "_search").Text = "zzz";
                    Application.DoEvents();
                    Save(form, Path.Combine(outDir, lang + "-3-nomatch.png"));
                    Field<TextBox>(form, "_search").Text = "";

                    Field<ComboBox>(form, "_kindFilter").SelectedIndex = 2;
                    Application.DoEvents();
                    Save(form, Path.Combine(outDir, lang + "-4-kind.png"));
                    Field<ComboBox>(form, "_kindFilter").SelectedIndex = 0;
                    Application.DoEvents();

                    MapTile dust = Field<FlowLayoutPanel>(form, "_tileArea").Controls.OfType<MapTile>().First(t => t.Map.Id == "de_dust2");
                    Invoke(form, "ShowKindMenu", dust, new Point(10, 10));
                    Application.DoEvents();
                    var menu = Field<ContextMenuStrip>(form, "_kindMenu");
                    Save(menu, Path.Combine(outDir, lang + "-5-menu.png"));
                    menu.Close();

                    // 메모 창은 모달이라 열린 뒤 타이머로 찍고 닫는다
                    var timer = new Timer { Interval = 300 };
                    timer.Tick += (s, e) =>
                    {
                        timer.Stop();
                        Form dialog = Application.OpenForms.Cast<Form>().LastOrDefault(f => f != form);
                        if (dialog == null) return;
                        Save(dialog, Path.Combine(outDir, lang + "-6-note.png"));
                        dialog.Close();
                    };
                    timer.Start();
                    Invoke(form, "EditNote", "de_dust2", dust.Map.TileName);
                    timer.Dispose();
                }
            }
            Fake.ClearWorkshopMaps();
            Fake.ForgetWorkshopInfo();
            Console.WriteLine("캡처: " + outDir);
        }

        private static void Save(Control c, string path)
        {
            using (var bmp = new Bitmap(c.Width, c.Height))
            {
                c.DrawToBitmap(bmp, new Rectangle(0, 0, c.Width, c.Height));
                bmp.Save(path, ImageFormat.Png);
            }
        }

        private static T Field<T>(object o, string name)
        {
            return (T)o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);
        }

        private static void Invoke(object o, string name, params object[] args)
        {
            o.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(o, args);
        }
    }
}
