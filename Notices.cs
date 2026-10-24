using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace CS2PracticeHost
{
    /// <summary>
    /// 상태 칸에 붙는 안내문을 사이트(`site/notices.json`)에서 받아 온다.
    /// 안내 한 줄을 보태려고 빌드·릴리스를 다시 하지 않으려고 만들었다(2026-10-24, CS2 버그로 창작마당 맵 바꾸기 뒤 목소리가 끊길 때).
    /// 보내는 것은 없고 받는 것은 짧은 글뿐이다. 받지 못하면 전에 받아 둔 것을 쓰고, 그것도 없으면 아무것도 안 붙인다.
    /// </summary>
    internal static class Notices
    {
        private const string Url = "https://cs2.logstone.net/notices.json";

        // 상태 칸은 몇 줄밖에 없다 — 사이트가 실수로 길게 적어도 칸이 넘치지 않게 자른다
        private const int MaxShown = 2;
        private const int MaxLength = 160;

        private static Notice[] _all;

        /// <summary>
        /// 사이트에서 받아 와 적어 둔다. 전과 달라졌으면 true — 그때만 상태 칸을 다시 그린다.
        /// 네트워크를 타므로 화면 쪽에서는 뒤에서 부른다.
        /// </summary>
        public static bool Fetch()
        {
            string json = Download();
            if (json == null) return false;

            Notice[] parsed = Parse(json);
            if (parsed == null) return false;   // 깨진 파일로 받아 둔 것을 덮지 않는다

            string before = Saved();
            if (before == json) return false;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, json, Encoding.UTF8);
            }
            catch (Exception) { }   // 못 적어도 이번 실행에는 쓴다

            _all = parsed;
            return true;
        }

        /// <summary>
        /// 지금 고른 모드·맵에 맞는 안내문을 상태 칸 줄(「※ …」)로 이어 붙인다. 없으면 빈 문자열.
        /// `when`을 모르는 값으로 적은 안내는 띄우지 않는다 — 새 조건을 쓴 안내가 옛 판에서 엉뚱한 자리에 뜨지 않게.
        /// </summary>
        public static string For(string modeKey, bool workshop)
        {
            if (_all == null) _all = Parse(Saved()) ?? new Notice[0];

            DateTime today = DateTime.Today;
            var lines = _all
                .Where(n => n != null && Matches(n.When, modeKey, workshop) && Within(n, today))
                .Select(n => Clean(Strings.En ? n.En : n.Ko))
                .Where(text => text.Length > 0)
                .Take(MaxShown);

            return string.Concat(lines.Select(Strings.NoticeLine));
        }

        /// <summary>확인기가 받은 것 대신 쓸 글을 넣는다. null이면 적어 둔 것을 다시 읽는다.</summary>
        internal static void Use(string json)
        {
            _all = json == null ? null : (Parse(json) ?? new Notice[0]);
        }

        internal static Notice[] Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                    return (Notice[])Serializer.ReadObject(stream);
            }
            catch (Exception) { return null; }
        }

        private static bool Matches(string when, string modeKey, bool workshop)
        {
            switch ((when ?? "").Trim().ToLowerInvariant())
            {
                case "always": return true;
                case "workshop": return workshop;
                case "builtin": return !workshop;
                default:
                    // 모드 키 하나(wingman 등)도 받는다
                    return GameData.Modes.Any(m => m.Key == when) && when == modeKey;
            }
        }

        /// <summary>from·until은 「yyyy-MM-dd」, 둘 다 그날을 넣는다. 비어 있으면 그쪽 끝이 없다.</summary>
        private static bool Within(Notice n, DateTime today)
        {
            DateTime day;
            if (!string.IsNullOrEmpty(n.From))
            {
                if (!TryDay(n.From, out day)) return false;
                if (today < day) return false;
            }
            if (!string.IsNullOrEmpty(n.Until))
            {
                if (!TryDay(n.Until, out day)) return false;
                if (today > day) return false;
            }
            return true;
        }

        private static bool TryDay(string text, out DateTime day)
        {
            return DateTime.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out day);
        }

        /// <summary>한 줄로, 짧게. 글자 그대로 보여 주므로 꾸밈은 먹지 않는다.</summary>
        private static string Clean(string text)
        {
            if (text == null) return "";
            string one = string.Join(" ", text.Split(new[] { '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
            return one.Length > MaxLength ? one.Substring(0, MaxLength - 1) + "…" : one;
        }

        private static string Download()
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                var request = (HttpWebRequest)WebRequest.Create(Url);
                request.UserAgent = "CS2PracticeHost";
                request.Timeout = 5000;
                request.ReadWriteTimeout = 5000;
                using (WebResponse response = request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                    return reader.ReadToEnd();
            }
            catch (Exception)
            {
                // 인터넷이 없거나 사이트가 답하지 않는 것은 앱을 못 쓸 이유가 아니다
                return null;
            }
        }

        private static string Saved()
        {
            try { return File.Exists(FilePath) ? File.ReadAllText(FilePath, Encoding.UTF8) : null; }
            catch (Exception) { return null; }
        }

        private static string FilePath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "CS2PracticeHost", "notices.json");
            }
        }

        private static readonly DataContractJsonSerializer Serializer =
            new DataContractJsonSerializer(typeof(Notice[]));
    }

    [DataContract]
    internal sealed class Notice
    {
        [DataMember(Name = "id")] public string Id;

        /// <summary>always / workshop(창작마당 맵을 골랐을 때) / builtin / 모드 키(wingman 등)</summary>
        [DataMember(Name = "when")] public string When;

        [DataMember(Name = "from")] public string From;
        [DataMember(Name = "until")] public string Until;
        [DataMember(Name = "ko")] public string Ko;
        [DataMember(Name = "en")] public string En;
    }
}
