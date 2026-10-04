using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;

namespace CS2PracticeHost
{
    /// <summary>
    /// 켤 때 새 버전이 있는지만 본다. 받는 것은 사용자가 직접 한다.
    /// 보내는 정보는 없고 받는 것은 최신 버전 번호 하나다.
    /// </summary>
    internal static class UpdateCheck
    {
        private const string LatestApi = "https://api.github.com/repos/kimlog0415/cs2-practice-host/releases/latest";

        /// <summary>새 버전이 있으면 그 번호, 없거나 확인하지 못하면 null.</summary>
        public static string NewerVersion()
        {
            Version latest = Fetch();
            if (latest == null) return null;

            Version mine = Assembly.GetExecutingAssembly().GetName().Version;
            return latest > mine ? latest.ToString(3) : null;
        }

        private static Version Fetch()
        {
            try
            {
                // 기본값이 옛 프로토콜이라 GitHub이 거부한다 (.NET Framework 4.8)
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                var request = (HttpWebRequest)WebRequest.Create(LatestApi);
                request.UserAgent = "CS2PracticeHost";   // GitHub은 이게 없으면 거절한다
                request.Timeout = 5000;
                request.ReadWriteTimeout = 5000;

                string body;
                using (WebResponse response = request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream()))
                    body = reader.ReadToEnd();

                Match tag = Regex.Match(body, "\"tag_name\"\\s*:\\s*\"v?([0-9]+(?:\\.[0-9]+)*)\"");
                if (!tag.Success) return null;

                Version found;
                return Version.TryParse(tag.Groups[1].Value, out found) ? found : null;
            }
            catch (Exception)
            {
                // 인터넷이 없거나 GitHub이 답하지 않는 것은 앱을 못 쓸 이유가 아니다
                return null;
            }
        }
    }
}
