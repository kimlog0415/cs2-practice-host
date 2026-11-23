using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CS2PracticeHost
{
    /// <summary>
    /// 기본 맵이 게임과 맞춰지나 — 게임 폴더에서 찾기, 번역 파일에서 이름, 사이트 목록.
    /// 네트워크는 안 탄다 — 받은 목록 대신 <see cref="BuiltInMaps.Use"/>로 넣는다.
    /// </summary>
    internal static class BuiltInMapChecks
    {
        public static void Run()
        {
            BuiltInMaps.Use(null);
            Fake.Install("de_newmap", "de_newmap_vanity", "de_ancient_night", "ar_newarms", "de_bigmap_000", "cs_newhostage");

            FoundInGameFolder();
            NameFromTranslation();
            SiteListApplies();
            BrokenSiteListIgnored();
            SiteFileMatchesCode();

            BuiltInMaps.Use(null);
            // 다른 확인이 같은 가짜 설치 폴더를 쓴다 — 더한 맵 파일을 치운다
            foreach (string f in new[] { "de_newmap", "de_newmap_vanity", "de_ancient_night", "ar_newarms", "de_bigmap_000", "cs_newhostage" })
                try { File.Delete(Path.Combine(Cs2Paths.MapsDir, f + ".vpk")); } catch (Exception) { }
        }

        private static string[] Ids(string key)
        {
            return BuiltInMaps.For(GameData.Modes.First(m => m.Key == key)).Select(m => m.Id).ToArray();
        }

        private static void FoundInGameFolder()
        {
            Report.Head("목록에 없는 맵 파일이 깔려 있으면 붙나");

            foreach (string key in new[] { "casual", "competitive", "deathmatch" })
            {
                string[] ids = Ids(key);
                Report.Is(key + "에 새 폭탄 맵", ids.Contains("de_newmap"), true);
                Report.Is(key + "에 새 인질 맵", ids.Contains("cs_newhostage"), true);
                Report.Is(key + "에 무기 레이스 맵은 안 붙나", ids.Contains("ar_newarms"), false);
            }
            foreach (string key in new[] { "wingman", "retake", "rush" })
                Report.Is(key + "에는 안 붙나", Ids(key).Contains("de_newmap"), false);

            Report.Is("무기 레이스에 새 맵", Ids("armsrace").Contains("ar_newarms"), true);

            string[] casual = Ids("casual");
            Report.Is("메뉴 배경(_vanity)은 안 세나", casual.Contains("de_newmap_vanity"), false);
            Report.Is("쪼갠 조각(_000)은 안 세나", casual.Any(id => id.StartsWith("de_bigmap")), false);
            Report.Is("고대 밤판은 안 붙나", casual.Contains("de_ancient_night"), false);
            Report.Is("윙맨 전용 맵이 캐주얼로 새지 않나", casual.Contains("de_debris"), false);
            Report.Is("붙은 자리는 목록 끝", casual.Last() == "de_newmap" || casual.Last() == "cs_newhostage", true);
            Report.Is("이름을 모르면 짐작", BuiltInMaps.For(GameData.Modes[0]).First(m => m.Id == "de_newmap").Ko, "Newmap");
        }

        private static void NameFromTranslation()
        {
            Report.Head("번역 파일에서 이름을 읽나");

            string ko = "\"lang\" { \"Tokens\" {\r\n\t\t\"SFUI_Map_de_cache\"\t\t\t\"무기창고\"\r\n\t\t\"SFUI_Map_de_cbble\"\t\t\t\"코블\"\r\n\t\t\"SFUI_Map_de_cbble_desc\"\t\"설명\"\r\n";
            Report.Is("코블", BuiltInMaps.Lookup(ko, "de_cbble"), "코블");
            Report.Is("없는 맵", BuiltInMaps.Lookup(ko, "de_newmap"), null);
            Report.Is("앞부분이 같은 다른 줄과 안 헷갈리나", BuiltInMaps.Lookup(ko, "de_cbb"), null);
        }

        private static void SiteListApplies()
        {
            Report.Head("사이트 목록으로 바뀌나");

            bool ok = BuiltInMaps.Use(
                "{\"modes\":[" +
                "{\"key\":\"wingman\",\"maps\":[\"de_newmap\",\"de_inferno\",\"bad id!\"]}," +
                "{\"key\":\"casual\",\"maps\":[]}," +
                "{\"key\":\"nomode\",\"maps\":[\"de_dust2\"]}]," +
                "\"names\":[{\"id\":\"de_newmap\",\"ko\":\"새맵\",\"en\":\"New Map\"}]," +
                "\"ignore\":[]}");
            Report.Is("읽혔나", ok, true);

            GameMap[] wingman = GameData.Modes.First(m => m.Key == "wingman").Maps;
            Report.Is("윙맨 순서", string.Join(",", wingman.Select(m => m.Id)), "de_newmap,de_inferno");
            Report.Is("사이트가 준 이름", wingman[0].Ko + "/" + wingman[0].En, "새맵/New Map");
            Report.Is("이름이 없으면 코드에 박힌 이름", wingman[1].Ko, "인페르노");
            Report.Is("빈 목록은 건너뛰나", GameData.Modes.First(m => m.Key == "casual").Maps.Any(m => m.Id == "de_dust2"), true);
            Report.Is("윙맨에 든 맵은 캐주얼 찾기에서 빠지나", Ids("casual").Contains("de_newmap"), false);
            Report.Is("ignore를 비우면 고대 밤판도 붙나", Ids("casual").Contains("de_ancient_night"), true);

            BuiltInMaps.Use(null);
            Report.Is("되돌리면 윙맨 맵 수", GameData.Modes.First(m => m.Key == "wingman").Maps.Length, 8);
        }

        private static void BrokenSiteListIgnored()
        {
            Report.Head("깨진 사이트 목록은 안 먹나");

            Report.Is("깨진 글", BuiltInMaps.Use("{\"modes\":[{\"key\":"), false);
            Report.Is("코드에 박힌 목록 그대로", GameData.Modes.First(m => m.Key == "casual").Maps.Any(m => m.Id == "de_cbble"), true);
            BuiltInMaps.Use(null);
        }

        /// <summary>사이트에 올라가는 목록이 코드에 박힌 것과 같아야 push해도 아무것도 안 바뀐다 — 다르면 둘 중 하나를 고친다.</summary>
        private static void SiteFileMatchesCode([CallerFilePath] string here = "")
        {
            Report.Head("site/maps.json이 코드 목록과 같나");

            string path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here), @"..\..\site\maps.json"));
            Catalog site = BuiltInMaps.Parse(File.Exists(path) ? File.ReadAllText(path) : null);
            Report.Is("읽혔나", site != null, true);
            if (site == null) return;

            foreach (GameMode mode in GameData.Modes)
            {
                ModeMaps entry = site.Modes.FirstOrDefault(m => m.Key == mode.Key);
                Report.Is(mode.Key, entry == null ? "(없음)" : string.Join(",", entry.Maps),
                          string.Join(",", mode.Maps.Select(m => m.Id)));
            }
            foreach (GameMap map in GameData.Modes.SelectMany(m => m.Maps).GroupBy(m => m.Id).Select(g => g.First()))
            {
                MapName n = site.Names.FirstOrDefault(x => x.Id == map.Id);
                Report.Is(map.Id + " 이름", n == null ? "(없음)" : n.Ko + "/" + n.En, map.Ko + "/" + map.En);
            }
        }
    }
}
