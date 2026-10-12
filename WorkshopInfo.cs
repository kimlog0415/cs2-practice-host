using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace CS2PracticeHost
{
    /// <summary>
    /// 받아 둔 창작마당 맵의 진짜 제목과 「어느 모드용인가」 태그. 제작자가 올릴 때 단 것이라
    /// 파일 이름으로 짐작하는 것보다 정확하다.
    ///
    /// 보내는 것은 구독 번호뿐이다 — 원래 공개된 값이고, 누가 켰는지는 보내지 않는다.
    /// 받아 온 것은 적어 두고 다음부터는 새로 구독한 번호만 물어본다.
    /// 인터넷이 없으면 적어 둔 것을 쓰고, 그것도 없으면 파일 이름으로 보여 준다.
    /// </summary>
    internal static class WorkshopInfo
    {
        private const string Api = "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/";

        /// <summary>한 요청에 묶어 보내는 수. 구독이 많아도 몇 번이면 끝난다.</summary>
        private const int BatchSize = 50;

        private static Dictionary<string, WorkshopItem> _known;

        public static WorkshopItem Of(string id)
        {
            Load();
            WorkshopItem item;
            return _known.TryGetValue(id, out item) ? item : null;
        }

        /// <summary>아직 모르는 번호만 Steam에 물어본다. 돌려주는 값은 「새로 알아낸 게 있나」.</summary>
        public static bool FetchMissing(IEnumerable<string> ids)
        {
            Load();
            string[] missing = ids.Distinct().Where(id => !_known.ContainsKey(id)).ToArray();
            if (missing.Length == 0) return false;

            bool gained = false;
            for (int at = 0; at < missing.Length; at += BatchSize)
            {
                string[] batch = missing.Skip(at).Take(BatchSize).ToArray();
                foreach (WorkshopItem item in Ask(batch))
                {
                    _known[item.Id] = item;
                    gained = true;
                }
            }

            if (gained) Save();
            return gained;
        }

        // ---------- Steam ----------

        private static IEnumerable<WorkshopItem> Ask(string[] ids)
        {
            string body = "itemcount=" + ids.Length;
            for (int i = 0; i < ids.Length; i++)
                body += "&publishedfileids[" + i + "]=" + Uri.EscapeDataString(ids[i]);

            SteamReply reply;
            try
            {
                // 기본값이 옛 프로토콜이라 거절당한다 (.NET Framework 4.8)
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                var request = (HttpWebRequest)WebRequest.Create(Api);
                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded";
                request.UserAgent = "CS2PracticeHost";
                request.Timeout = 8000;
                request.ReadWriteTimeout = 8000;

                byte[] bytes = Encoding.UTF8.GetBytes(body);
                request.ContentLength = bytes.Length;
                using (Stream send = request.GetRequestStream()) send.Write(bytes, 0, bytes.Length);

                using (WebResponse response = request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                    reply = (SteamReply)ReplySerializer.ReadObject(stream);
            }
            catch (Exception)
            {
                // 인터넷이 없거나 Steam이 답하지 않는 것은 앱을 못 쓸 이유가 아니다
                yield break;
            }

            if (reply == null || reply.Response == null || reply.Response.Details == null) yield break;

            foreach (SteamDetail d in reply.Response.Details)
            {
                if (d == null || d.Result != 1 || d.Id == null) continue;   // 1이 「찾음」이다
                yield return new WorkshopItem
                {
                    Id = d.Id,
                    Title = d.Title,
                    Tags = d.Tags == null ? new string[0] : d.Tags.Where(t => t != null && t.Tag != null)
                                                                  .Select(t => t.Tag).ToArray(),
                };
            }
        }

        // ---------- 적어 두기 ----------

        private static string FilePath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "CS2PracticeHost", "workshop.json");
            }
        }

        private static void Load()
        {
            if (_known != null) return;
            _known = new Dictionary<string, WorkshopItem>();
            try
            {
                using (var stream = File.OpenRead(FilePath))
                {
                    var saved = (WorkshopCache)CacheSerializer.ReadObject(stream);
                    if (saved != null && saved.Items != null)
                        foreach (WorkshopItem item in saved.Items.Where(i => i != null && i.Id != null))
                            _known[item.Id] = item;
                }
            }
            catch (Exception) { }   // 없거나 깨졌으면 빈 채로 시작한다
        }

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                using (var stream = File.Create(FilePath))
                    CacheSerializer.WriteObject(stream, new WorkshopCache { Items = _known.Values.ToArray() });
            }
            catch (Exception) { }
        }

        /// <summary>확인기에서 쓴다. 적어 둔 것을 비워 다음 읽기가 파일을 다시 보게 한다.</summary>
        internal static void Forget() { _known = null; }

        private static readonly DataContractJsonSerializer CacheSerializer =
            new DataContractJsonSerializer(typeof(WorkshopCache));

        private static readonly DataContractJsonSerializer ReplySerializer =
            new DataContractJsonSerializer(typeof(SteamReply));
    }

    [DataContract]
    internal sealed class WorkshopItem
    {
        [DataMember(Name = "id")] public string Id { get; set; }
        [DataMember(Name = "title")] public string Title { get; set; }
        [DataMember(Name = "tags")] public string[] Tags { get; set; }

        public bool HasTag(string tag)
        {
            return Tags != null && Tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase));
        }
    }

    [DataContract]
    internal sealed class WorkshopCache
    {
        [DataMember(Name = "items")] public WorkshopItem[] Items { get; set; }
    }

    // ---------- Steam이 돌려주는 모양 ----------

    [DataContract]
    internal sealed class SteamReply
    {
        [DataMember(Name = "response")] public SteamResponse Response { get; set; }
    }

    [DataContract]
    internal sealed class SteamResponse
    {
        [DataMember(Name = "publishedfiledetails")] public SteamDetail[] Details { get; set; }
    }

    [DataContract]
    internal sealed class SteamDetail
    {
        [DataMember(Name = "publishedfileid")] public string Id { get; set; }
        [DataMember(Name = "result")] public int Result { get; set; }
        [DataMember(Name = "title")] public string Title { get; set; }
        [DataMember(Name = "tags")] public SteamTag[] Tags { get; set; }
    }

    [DataContract]
    internal sealed class SteamTag
    {
        [DataMember(Name = "tag")] public string Tag { get; set; }
    }
}
