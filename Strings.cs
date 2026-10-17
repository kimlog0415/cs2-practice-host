using System.Globalization;

namespace CS2PracticeHost
{
    /// <summary>
    /// 화면에 나가는 문구를 한곳에 모은다. 주석·로그·cfg 표시는 우리 것이라 번역하지 않는다.
    /// </summary>
    internal static class Strings
    {
        public static bool En;

        /// <summary>설정에 저장된 값이 없으면 윈도우 표시 언어를 따른다 (한국어가 아니면 영어).</summary>
        public static void Init(string saved)
        {
            if (saved == "ko") { En = false; return; }
            if (saved == "en") { En = true; return; }
            En = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName != "ko";
        }

        public static string Code { get { return En ? "en" : "ko"; } }

        private static string T(string ko, string en) { return En ? en : ko; }

        // ---------- 글꼴 ----------

        /// <summary>맑은 고딕은 한글용이라 영문에서는 자막처럼 떠 보인다.</summary>
        public static string UiFont { get { return T("맑은 고딕", "Segoe UI"); } }

        // ---------- 머리 ----------

        public static string LangToggle { get { return T("EN", "한"); } }
        public static string LangToggleTip { get { return T("English", "한국어"); } }
        public static string HelpTip { get { return T("사용법 보기", "Open the guide"); } }

        // ---------- 줄 이름 ----------

        public static string RowMode { get { return T("모드", "Mode"); } }
        public static string RowMap { get { return T("맵", "Map"); } }
        public static string RowBots { get { return T("봇 수", "Bots"); } }
        public static string RowLevel { get { return T("봇 난이도", "Difficulty"); } }
        public static string RowTeam { get { return T("봇 팀", "Bot team"); } }

        // ---------- 고르는 값 ----------

        public static string BotCountDefault { get { return T("게임 기본값", "Game default"); } }

        /// <summary>
        /// 받아 둔 창작마당 맵. 기본 맵과 섞이지 않게 앞에 표시를 붙인다.
        /// 제작자가 붙인 제목은 꽤 길 때가 있어(괄호 설명·이모지까지) 칸을 넘치지 않게 자른다.
        /// </summary>
        public static string WorkshopMap(string shown)
        {
            const int Limit = 34;
            if (shown != null && shown.Length > Limit) shown = shown.Substring(0, Limit).TrimEnd() + "…";
            return T("창작마당 · " + shown, "Workshop · " + shown);
        }

        public static string BotCount(int n)
        {
            if (!En) return n + " 명";
            return n == 1 ? "1 bot" : n + " bots";
        }

        // ---------- 버튼 ----------

        public static string Launch { get { return T("▶  CS2 켜고 서버 열기", "▶  Launch CS2 and open server"); } }
        public static string Relaunch { get { return T("▶  CS2 다시 켜고 서버 열기", "▶  Relaunch CS2 and open server"); } }
        public static string CopyLink { get { return T("참가 링크 복사 (친구에게 보내기)", "Copy join link (send to a friend)"); } }
        public static string CopyAddress { get { return T("콘솔용 주소 복사", "Copy console address"); } }

        public static string UpdateFound(string version)
        {
            return T("새 버전 " + version + " 이 나왔어요 — 받으러 가기",
                     "Version " + version + " is out — get it");
        }

        // ---------- 상태 띠 ----------

        public static string BannerOff { get { return T("○  CS2 꺼져 있음 · 아래 ▶ 버튼으로 시작", "○  CS2 not running · press ▶ below"); } }
        public static string BannerNoLog { get { return T("●  CS2 실행 중 · 주소를 읽으려면 ▶로 다시 켜기", "●  CS2 running · relaunch with ▶ to read address"); } }
        public static string BannerNoServer { get { return T("●  CS2 실행 중 · 서버 없음 (메인 화면)", "●  CS2 running · no server (main menu)"); } }

        public static string BannerServerOpen(string time)
        {
            return T("●  서버 열림 (" + time + ") · 맵 안에서 F10",
                     "●  Server open (" + time + ") · F10 in game");
        }

        // ---------- 가운데 안내 ----------

        public static string Saved(string mode, string map, string note)
        {
            return T(
                "저장됐어요!  (" + mode + " / " + map + ")" + note + "\n\n" +
                "▶ 버튼 : CS2 켜고 서버 열기 (항상 이걸로 켜세요)\n" +
                "F10 : (맵 안에서) 맵·모드 바꾸기 — 친구들 그대로\n\n" +
                "※ 봇 설정은 맵을 새로 열 때 반영돼요\n" +
                "※ 방장이 메뉴로 나가면 서버가 닫혀요",

                "Saved!  (" + mode + " / " + map + ")" + note + "\n\n" +
                "▶ button : launch CS2 and open the server (always use this)\n" +
                "F10 : change map/mode while in a map — friends stay in\n\n" +
                "* Bot settings apply when a map loads\n" +
                "* The server closes if the host returns to the menu");
        }

        /// <summary>모드가 봇을 직접 정해 설정이 안 먹는 경우의 안내.</summary>
        public static string ModeBotsNote(string modeKey)
        {
            if (modeKey == "rush")
                return T("\n※ 러시는 봇을 게임이 정해요 (3대3, 빈자리를 채워요)",
                         "\n* Rush picks its own bots (3v3, it fills the empty slots)");
            return RetakeBotsNote;
        }

        /// <summary>
        /// 창작마당 맵도 F10으로 간다. 그 맵이 있는 친구는 로딩 한 번으로 따라오지만, 없는 친구는
        /// 받겠냐는 창에서 받기를 누르면 서버를 떠나 받고 저절로 다시 붙지 않는다(실기).
        /// 주소는 그대로라 같은 링크로 다시 들어오면 된다.
        /// </summary>
        public static string WorkshopF10Note
        {
            get
            {
                return T("\n※ 창작마당 맵이 없는 친구는 받느라 한 번 나가요 — 다 받은 뒤 같은 링크로 다시 들어오면 돼요",
                         "\n* Friends without this workshop map leave once to download it — then rejoin with the same link");
            }
        }

        public static string RetakeBotsNote
        {
            get
            {
                return T("\n※ 탈환은 봇을 게임이 정해요 (수비 테러리스트 봇)",
                         "\n* Retakes picks its own bots (defending Terrorist bots)");
            }
        }

        public static string OneSidedBotNote
        {
            get
            {
                return T(
                    "\n※ 한 팀으로 몰면 봇은 「사람 수 + 2」까지만 나와요\n" +
                    "   (다 채우려면 봇 팀을 「양쪽에 섞기」로)",

                    "\n* On one team, bots stop at \"humans + 2\"\n" +
                    "   (to fill every slot, set bot team to \"Mix both teams\")");
            }
        }

        public static string Launching(string mode, string map)
        {
            return T(
                "CS2 켜는 중...  (" + mode + " / " + map + ")\n\n" +
                "서버가 열리면 친구에게 보낼 참가 링크가\n자동으로 복사돼요. (1분 정도 걸려요)",

                "Launching CS2...  (" + mode + " / " + map + ")\n\n" +
                "Once the server is up, the join link is copied\nfor you automatically. (takes about a minute)");
        }

        public static string Cs2ClosedBeforeServer
        {
            get
            {
                return T("CS2가 꺼졌어요.\n다시 ▶ 버튼으로 열어주세요.",
                         "CS2 was closed.\nUse the ▶ button to open it again.");
            }
        }

        public static string ServerNotFound
        {
            get
            {
                return T("서버를 찾지 못했어요.\nCS2가 켜졌는지 확인하고 다시 눌러주세요.",
                         "Could not find the server.\nCheck that CS2 started, then press again.");
            }
        }

        public static string ServerOpened(bool copied, string link)
        {
            return T(
                (copied
                    ? "서버가 열렸어요! 참가 링크가 복사됐어요.\n카톡/디스코드에 Ctrl+V 로 보내세요.\n"
                    : "서버가 열렸어요!\n복사가 막혀서 아래 [참가 링크 복사]를 눌러주세요.\n") +
                "친구가 누르면 안내 페이지를 거쳐 바로 들어와요.\n\n" + link,

                (copied
                    ? "Server is open! The join link is copied.\nPaste it in your chat app with Ctrl+V.\n"
                    : "Server is open!\nCopying was blocked — press [Copy join link] below.\n") +
                "Your friend clicks it and walks straight in.\n\n" + link);
        }

        // ---------- 알림창 ----------

        public static string SteamNotFound { get { return T("Steam을 찾지 못했어요.", "Could not find Steam."); } }

        public static string NoMaps
        {
            get
            {
                return T("고를 수 있는 맵이 없어요.\nCS2 설치 폴더를 제대로 찾았는지 확인해 주세요.",
                         "No maps to choose from.\nCheck that the CS2 install folder was found correctly.");
            }
        }

        public static string RelaunchWarning
        {
            get
            {
                return T(
                    "지금 서버가 열려 있어요.\n\n" +
                    "새로 켜면 들어와 있는 친구들이 전부 끊겨요.\n" +
                    "맵·모드만 바꿀 거라면 [아니요]를 누르고 맵 안에서 F10을 쓰세요.\n\n" +
                    "그래도 CS2를 껐다가 새로 켤까요?",

                    "A server is open right now.\n\n" +
                    "Relaunching drops every friend who is already in.\n" +
                    "To change only the map or mode, press [No] and use F10 in game.\n\n" +
                    "Close CS2 and launch it again anyway?");
            }
        }

        public static string LinkCopied(bool copied, string link)
        {
            return T(
                (copied
                     ? "참가 링크가 복사됐어요!\n카톡/디스코드에 Ctrl+V 로 붙여넣으세요.\n\n"
                     : "복사가 막혔어요. 아래 주소를 직접 옮겨 주세요.\n\n") +
                link + "\n\n" +
                "친구가 링크를 누르면 안내 페이지가 열리고,\n" +
                "거기서 [게임 접속하기]를 누르면 바로 들어와요.\n" +
                "친구 쪽에 설치할 것도 콘솔 설정도 없어요.\n\n" + SharingWarning,

                (copied
                     ? "Join link copied!\nPaste it in your chat app with Ctrl+V.\n\n"
                     : "Copying was blocked. Please carry the link below over by hand.\n\n") +
                link + "\n\n" +
                "Your friend clicks the link, a short page opens,\n" +
                "and [Join the game] takes them straight in.\n" +
                "Nothing to install, no console setting on their side.\n\n" + SharingWarning);
        }

        public static string AddressCopied(bool copied, string command)
        {
            return T(
                (copied
                     ? "콘솔용 주소가 복사됐어요.\n\n"
                     : "복사가 막혔어요. 아래 주소를 직접 옮겨 주세요.\n\n") +
                command + "\n\n" +
                "친구가 게임 콘솔(~ 키)에 붙여넣는 방식이에요.\n" +
                "보통은 [참가 링크 복사] 쪽이 더 편해요.\n\n" + SharingWarning,

                (copied
                     ? "Console address copied.\n\n"
                     : "Copying was blocked. Please carry the address below over by hand.\n\n") +
                command + "\n\n" +
                "Your friend pastes this into the game console (~ key).\n" +
                "[Copy join link] is usually easier.\n\n" + SharingWarning);
        }

        public static string SharingWarning
        {
            get
            {
                return T(
                    "※ 주소를 아는 사람은 누구나 들어올 수 있어요.\n" +
                    "   공개된 곳에 올리지 말고 친구에게만 보내세요.",

                    "* Anyone who knows the address can join.\n" +
                    "   Send it to friends only, not somewhere public.");
            }
        }

        public static string Cs2Off
        {
            get
            {
                return T("CS2가 꺼져 있어요.\n▶ 버튼으로 서버를 열어주세요.",
                         "CS2 is not running.\nUse the ▶ button to open a server.");
            }
        }

        public static string Cs2NotOurs
        {
            get
            {
                return T("이 CS2는 앱으로 켠 게 아니라 주소를 읽을 수 없어요.\n\n▶ 버튼으로 다시 켜주세요.",
                         "This CS2 was not started by the app, so the address cannot be read.\n\n" +
                         "Please relaunch it with the ▶ button.");
            }
        }

        public static string NoServerOpen
        {
            get
            {
                return T("열려 있는 서버가 없어요.\n\n▶ 버튼으로 서버를 열어주세요.",
                         "No server is open.\n\nUse the ▶ button to open one.");
            }
        }

        public static string CannotOpenPage(string url)
        {
            return T("페이지를 열지 못했어요.\n주소를 복사했으니 브라우저에 붙여넣어 주세요.\n\n" + url,
                     "Could not open the page.\nThe address is copied — paste it in your browser.\n\n" + url);
        }

        public static string CannotOpenGuide(string url)
        {
            return T("사용법 페이지를 열지 못했어요.\n주소를 복사했으니 브라우저에 붙여넣어 주세요.\n\n" + url,
                     "Could not open the guide.\nThe address is copied — paste it in your browser.\n\n" + url);
        }

        // ---------- 시작할 때 ----------

        public static string SteamMissing
        {
            get
            {
                return T("Steam을 찾지 못했어요.\n\nSteam이 설치돼 있는지 확인해 주세요.",
                         "Could not find Steam.\n\nPlease check that Steam is installed.");
            }
        }

        public static string InstallDirMissing
        {
            get
            {
                return T(
                    "CS2 설치 폴더를 찾지 못했어요.\n\n" +
                    "다음 창에서 CS2가 설치된 폴더를 골라 주세요.\n" +
                    "(이름이 'Counter-Strike Global Offensive' 인 폴더예요)",

                    "Could not find the CS2 install folder.\n\n" +
                    "Please pick it in the next window.\n" +
                    "(the folder named 'Counter-Strike Global Offensive')");
            }
        }

        public static string PickInstallDir
        {
            get { return T("CS2 설치 폴더를 선택하세요", "Select the CS2 install folder"); }
        }

        public static string WrongInstallDir
        {
            get
            {
                return T(
                    "그 폴더에서는 CS2를 찾지 못했어요.\n\n" +
                    "Steam 라이브러리에서 CS2를 우클릭 → 관리 → 로컬 파일 보기 로\n" +
                    "열리는 폴더를 골라 주세요.",

                    "CS2 is not in that folder.\n\n" +
                    "In your Steam library, right-click CS2 → Manage → Browse local files,\n" +
                    "and pick the folder that opens.");
            }
        }

        public static string Crash(string type, string message)
        {
            return T(
                "문제가 생겨 앱을 열지 못했어요.\n\n" + type + "\n" + message + "\n\n" +
                "자세한 내용이 클립보드에 복사됐어요.\n만든 사람에게 붙여넣어 보내 주세요.",

                "Something went wrong and the app could not start.\n\n" + type + "\n" + message + "\n\n" +
                "The details are copied to your clipboard.\nPlease paste them to the developer.");
        }
    }
}
