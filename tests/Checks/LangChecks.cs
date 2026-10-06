using System.Windows.Forms;

namespace CS2PracticeHost
{
    /// <summary>
    /// 한국어·영어 전환. 고른 값이 남는지가 핵심이다 — 보이는 이름으로 되살리면
    /// 언어를 바꿀 때마다 기본값으로 떨어진다.
    /// </summary>
    internal static class LangChecks
    {
        public static void Run()
        {
            Fake.Install();
            LegacySettingsCarryOver();
            ToggleKeepsSelection();
            SavedValuesAreLanguageNeutral();
            StartsInEnglishWhenSettingSaysSo();
            EasyStaysEasy();
        }

        /// <summary>1.0.2까지는 화면에 보이던 한국어 이름을 저장했다. 그 설정도 되살아나야 한다.</summary>
        private static void LegacySettingsCarryOver()
        {
            Report.Head("1.0.2 형식 설정이 이어받아지는지");

            Fake.WriteSettings("{\"mode\":\"데스매치\",\"map\":\"de_nuke\",\"bots\":5," +
                               "\"level\":\"어려움\",\"team\":\"테러리스트 팀만\",\"installDir\":null,\"lang\":\"ko\"}");
            Strings.Init("ko");
            using (var form = new MainForm(Settings.Load()))
            {
                Report.Is("모드", Peek.Combo(form, "_mode"), "데스매치");
                Report.Is("맵", Peek.Combo(form, "_map"), "뉴크");
                Report.Is("봇 수(저장값은 인덱스라 5 = 4명)", Peek.Combo(form, "_bots"), "4 명");
                Report.Is("봇 난이도", Peek.Combo(form, "_level"), "어려움");
                Report.Is("봇 팀", Peek.Combo(form, "_team"), "테러리스트 팀만");
            }
        }

        /// <summary>언어를 바꿔도 고른 다섯 개가 그대로 남아야 한다.</summary>
        private static void ToggleKeepsSelection()
        {
            Report.Head("언어를 바꿔도 고른 값이 남는지");

            Fake.WriteSettings("{\"mode\":\"armsrace\",\"map\":\"ar_baggage\",\"bots\":3," +
                               "\"level\":\"2\",\"team\":\"ct\",\"installDir\":null,\"lang\":\"ko\"}");
            Strings.Init("ko");
            using (var form = new MainForm(Settings.Load()))
            {
                Report.Is("시작 모드", Peek.Combo(form, "_mode"), "무기 레이스");
                Report.Is("시작 맵", Peek.Combo(form, "_map"), "수하물");
                Report.Is("시작 난이도", Peek.Combo(form, "_level"), "어려움");
                Report.Is("시작 봇 팀", Peek.Combo(form, "_team"), "대테러 팀만");
                Report.Is("줄 이름", Peek.RowLabel(form, 0), "모드");

                Peek.Click(form, "EN");

                Report.Is("영어 모드", Peek.Combo(form, "_mode"), "Arms Race");
                Report.Is("영어 맵", Peek.Combo(form, "_map"), "Baggage");
                Report.Is("영어 봇 수", Peek.Combo(form, "_bots"), "2 bots");
                Report.Is("영어 난이도", Peek.Combo(form, "_level"), "Hard");
                Report.Is("영어 봇 팀", Peek.Combo(form, "_team"), "Counter-Terrorists only");
                Report.Is("영어 줄 이름", Peek.RowLabel(form, 0), "Mode");
                Report.Is("언어 버튼", Peek.Button(form, 0).Text, "한");

                Peek.Click(form, "한");

                Report.Is("되돌린 모드", Peek.Combo(form, "_mode"), "무기 레이스");
                Report.Is("되돌린 맵", Peek.Combo(form, "_map"), "수하물");
                Report.Is("되돌린 봇 수", Peek.Combo(form, "_bots"), "2 명");
                Report.Is("되돌린 난이도", Peek.Combo(form, "_level"), "어려움");
                Report.Is("되돌린 봇 팀", Peek.Combo(form, "_team"), "대테러 팀만");
                Report.Is("되돌린 줄 이름", Peek.RowLabel(form, 0), "모드");
            }
        }

        /// <summary>저장값에 보이는 이름이 들어가면 다음 전환에서 또 깨진다.</summary>
        private static void SavedValuesAreLanguageNeutral()
        {
            Report.Head("저장된 값이 언어와 무관한지");

            Fake.WriteSettings("{\"mode\":\"데스매치\",\"map\":\"de_dust2\",\"bots\":2," +
                               "\"level\":\"쉬움\",\"team\":\"양쪽에 섞기\",\"installDir\":null,\"lang\":\"ko\"}");
            Strings.Init("ko");
            using (var form = new MainForm(Settings.Load())) { Peek.Click(form, "EN"); }

            string json = Fake.ReadSettings();
            Report.Note("저장된 내용: " + json);
            Report.Has("모드", json, "\"mode\":\"deathmatch\"");
            Report.Has("난이도", json, "\"level\":\"0\"");
            Report.Has("봇 팀", json, "\"team\":\"any\"");
            Report.Has("언어", json, "\"lang\":\"en\"");
            Report.HasNot("한국어 이름이 안 남았는지", json, "데스매치");
            Report.HasNot("한국어 난이도가 안 남았는지", json, "쉬움");
        }

        private static void StartsInEnglishWhenSettingSaysSo()
        {
            Report.Head("설정이 en이면 처음부터 영어인지");

            Fake.WriteSettings("{\"mode\":\"casual\",\"map\":\"de_mirage\",\"bots\":0," +
                               "\"level\":\"1\",\"team\":\"any\",\"installDir\":null,\"lang\":\"en\"}");
            Strings.Init(Settings.Load().Lang);
            using (var form = new MainForm(Settings.Load()))
            {
                Report.Is("모드", Peek.Combo(form, "_mode"), "Casual");
                Report.Is("맵", Peek.Combo(form, "_map"), "Mirage");
                Report.Is("봇 수", Peek.Combo(form, "_bots"), "Game default");
                Report.Is("줄 이름", Peek.RowLabel(form, 0), "Mode");
                Report.Is("언어 버튼", Peek.Button(form, 0).Text, "한");
                Report.Is("켜기 버튼", Peek.Button(form, 2).Text, "▶  Launch CS2 and open server");
            }
        }

        /// <summary>
        /// 1.0.0부터 있던 버그 자리. 난이도만 기본값이 첫 칸이 아니라서,
        /// 「없음(-1)」을 큰 쪽 고르기로 받으면 제대로 찾은 0(쉬움)까지 1(보통)로 끌려 올라갔다.
        /// </summary>
        private static void EasyStaysEasy()
        {
            Report.Head("쉬움을 골라 두면 다시 켤 때도 쉬움인지 (1.1.1에서 고친 자리)");

            Fake.WriteSettings("{\"mode\":\"casual\",\"map\":\"de_dust2\",\"bots\":0," +
                               "\"level\":\"0\",\"team\":\"any\",\"installDir\":null,\"lang\":\"ko\"}");
            Strings.Init("ko");
            using (var form = new MainForm(Settings.Load()))
                Report.Is("난이도", Peek.Combo(form, "_level"), "쉬움");
        }
    }
}
