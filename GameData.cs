using System;
using System.Collections.Generic;
using System.Linq;

namespace CS2PracticeHost
{
    internal sealed class GameMap
    {
        /// <summary>`+map`·`changelevel`에 그대로 넘기는 값. 창작마당은 `workshop/번호/이름` 꼴이다.</summary>
        public string Id;
        public string Ko;
        public string En;

        /// <summary>창작마당 맵이면 그 vpk 파일 이름. 기본 맵은 null.</summary>
        public string WorkshopFile;

        public GameMap(string id, string ko, string en) { Id = id; Ko = ko; En = en; }

        public bool IsWorkshop { get { return WorkshopFile != null; } }

        public string Name
        {
            get { return IsWorkshop ? Strings.WorkshopMap(WorkshopFile) : (Strings.En ? En : Ko); }
        }

        /// <summary>
        /// 받아 둔 창작마당 맵. 제목은 Steam에 물어봐야 알 수 있어서 파일 이름을 그대로 보여 준다
        /// — 받은 사람은 그걸로 자기 맵을 알아본다.
        /// </summary>
        public static GameMap Workshop(string id, string fileName)
        {
            return new GameMap("workshop/" + id + "/" + fileName, null, null) { WorkshopFile = fileName };
        }
    }

    internal sealed class GameMode
    {
        /// <summary>설정에 남는 이름. 보이는 이름은 언어에 따라 바뀌므로 되살릴 때는 이걸 본다.</summary>
        public string Key;
        public string Ko;
        public string En;
        public int Type;
        public int Mode;
        /// <summary>맵 로딩 때 CS2가 읽는 gamemode_&lt;Cfg&gt;_server.cfg. 탈환은 봇을 모드 규칙이 정해서 없다.</summary>
        public string Cfg;
        public GameMap[] Maps;

        public string Name { get { return Strings.En ? En : Ko; } }
        public bool BotsConfigurable { get { return Cfg != null; } }
    }

    /// <summary>봇 난이도·팀처럼 「보이는 이름 + 게임에 넘기는 값」 짝.</summary>
    internal sealed class Choice<T>
    {
        public string Ko;
        public string En;
        public T Value;
        public Choice(string ko, string en, T value) { Ko = ko; En = en; Value = value; }

        public string Name { get { return Strings.En ? En : Ko; } }
    }

    /// <summary>게임 '연습' 탭의 이름·순서를 그대로 따른다.</summary>
    internal static class GameData
    {
        private static readonly GameMap[] ClassicMaps =
        {
            new GameMap("de_cache", "무기창고", "Cache"),
            new GameMap("de_anubis", "아누비스", "Anubis"),
            new GameMap("de_inferno", "인페르노", "Inferno"),
            new GameMap("de_mirage", "신기루", "Mirage"),
            new GameMap("de_dust2", "더스트2", "Dust II"),
            new GameMap("de_nuke", "뉴크", "Nuke"),
            new GameMap("de_ancient", "고대", "Ancient"),
            new GameMap("de_train", "열차", "Train"),
            new GameMap("de_vertigo", "버티고", "Vertigo"),
            new GameMap("de_overpass", "오버패스", "Overpass"),
            new GameMap("de_boulder", "볼더", "Boulder"),
            new GameMap("de_fachwerk", "파흐베르크", "Fachwerk"),
            new GameMap("cs_shelter", "쉘터", "Shelter"),
            new GameMap("cs_office", "사무실", "Office"),
            new GameMap("cs_italy", "이탈리아", "Italy"),
        };

        private static readonly GameMap[] RetakeMaps = ClassicMaps.Take(10).ToArray();

        /// <summary>
        /// 윙맨(2대2) 전용 맵. 밸브 맵 넷은 폭탄 자리를 하나로 줄인 판이라 파일은 기본 맵과 같고,
        /// 셋(Debris·El Dorado·Poseidon)은 윙맨에만 있다. 디렉터가 게임 화면에서 읽어 준 목록이다.
        /// </summary>
        private static readonly GameMap[] WingmanMaps =
        {
            new GameMap("de_debris", "잔해", "Debris"),
            new GameMap("de_eldorado", "엘도라도", "El Dorado"),
            new GameMap("de_poseidon", "포세이돈", "Poseidon"),
            new GameMap("de_overpass", "오버패스", "Overpass"),
            new GameMap("de_vertigo", "버티고", "Vertigo"),
            new GameMap("de_nuke", "뉴크", "Nuke"),
            new GameMap("de_inferno", "인페르노", "Inferno"),
        };

        /// <summary>러시(3대3)는 맵이 하나다. 그 안에 방 열아홉이 들어 있다.</summary>
        private static readonly GameMap[] RushMaps =
        {
            new GameMap("rush_001", "컴플렉스", "Complex"),
        };

        private static readonly GameMap[] ArmsMaps =
        {
            new GameMap("ar_baggage", "수하물", "Baggage"),
            new GameMap("ar_shoots", "재배소(낮)", "Shoots (Day)"),
            new GameMap("ar_shoots_night", "재배소(밤)", "Shoots (Night)"),
            new GameMap("ar_pool_day", "풀 데이", "Pool Day"),
        };

        public static readonly GameMode[] Modes =
        {
            new GameMode { Key = "casual",      Ko = "캐주얼",      En = "Casual",      Type = 0, Mode = 0, Cfg = "casual",         Maps = ClassicMaps },
            new GameMode { Key = "competitive", Ko = "경쟁",        En = "Competitive", Type = 0, Mode = 1, Cfg = "competitive",    Maps = ClassicMaps },
            new GameMode { Key = "wingman",     Ko = "윙맨",        En = "Wingman",     Type = 0, Mode = 2, Cfg = "competitive2v2", Maps = WingmanMaps },
            new GameMode { Key = "rush",        Ko = "러시",        En = "Rush",        Type = 0, Mode = 6, Cfg = null,             Maps = RushMaps },
            new GameMode { Key = "deathmatch",  Ko = "데스매치",    En = "Deathmatch",  Type = 1, Mode = 2, Cfg = "deathmatch",     Maps = ClassicMaps },
            new GameMode { Key = "armsrace",    Ko = "무기 레이스", En = "Arms Race",   Type = 1, Mode = 0, Cfg = "armsrace",       Maps = ArmsMaps },
            new GameMode { Key = "retake",      Ko = "탈환",        En = "Retakes",     Type = 0, Mode = 5, Cfg = null,             Maps = RetakeMaps },
        };

        /// <summary>
        /// 봇 설정을 넣을 gamemode_*_server.cfg 들. 고를 수 있는 모드마다 하나씩이고,
        /// 지금 안 쓰는 것도 남겨 둬야 옛 버전이 만든 파일이 지워진다.
        /// 윙맨은 파일 이름이 모드 이름과 달라서 competitive2v2 다.
        /// </summary>
        public static readonly string[] ServerCfgs =
        {
            "casual", "competitive", "competitive2v2", "deathmatch", "armsrace",
        };

        public static readonly Choice<int>[] BotLevels =
        {
            new Choice<int>("쉬움", "Easy", 0),
            new Choice<int>("보통", "Normal", 1),
            new Choice<int>("어려움", "Hard", 2),
            new Choice<int>("전문가", "Expert", 3),
        };

        public static readonly Choice<string>[] BotTeams =
        {
            new Choice<string>("양쪽에 섞기", "Mix both teams", "any"),
            new Choice<string>("테러리스트 팀만", "Terrorists only", "t"),
            new Choice<string>("대테러 팀만", "Counter-Terrorists only", "ct"),
        };

        public static IEnumerable<string> BotCounts
        {
            get
            {
                yield return Strings.BotCountDefault;
                for (int i = 0; i <= 10; i++) yield return Strings.BotCount(i);
            }
        }

        /// <summary>
        /// 1.0.2까지는 화면에 보이던 한국어 이름을 그대로 설정에 저장했다. 그것도 받아 줘야
        /// 쓰던 사람이 새 버전을 켤 때 골라 둔 값이 기본값으로 되돌아가지 않는다.
        /// </summary>
        public static int IndexOfMode(string saved)
        {
            if (saved == null) return -1;
            return Array.FindIndex(Modes, m => m.Key == saved || m.Ko == saved || m.En == saved);
        }

        public static int IndexOfLevel(string saved)
        {
            if (saved == null) return -1;
            return Array.FindIndex(BotLevels,
                l => l.Value.ToString() == saved || l.Ko == saved || l.En == saved);
        }

        public static int IndexOfTeam(string saved)
        {
            if (saved == null) return -1;
            return Array.FindIndex(BotTeams,
                t => t.Value == saved || t.Ko == saved || t.En == saved);
        }
    }
}
