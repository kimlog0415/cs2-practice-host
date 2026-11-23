using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace CS2PracticeHost
{
    /// <summary>
    /// 기본 맵 목록을 게임과 맞춘다 (2026-11-23 디렉터 — 「기본이든 창작마당이든 우리 앱이랑 싱크가 되어야 한다」).
    /// 계기는 CS2 11-17 업데이트로 코블이 공식 맵이 됐는데 앱 목록이 코드에 박혀 있어 새 판 전까지 안 떴던 것.
    ///
    /// 두 겹이다.
    /// 1. 사이트 목록 — `cs2.logstone.net/maps.json`. 모드마다 어떤 맵이 어떤 순서로 뜨나(윙맨·탈환처럼
    ///    파일만으로는 모르는 것)를 우리가 적어 둔다. 새 판 없이 push만 하면 모든 사용자에게 간다.
    ///    못 받으면 전에 받아 둔 것을, 그것도 없으면 코드에 박힌 목록(GameData)을 쓴다.
    /// 2. 게임 폴더에서 찾기 — 목록에 없는 `de_`·`cs_` 맵 파일이 깔려 있으면 캐주얼·경쟁·데스매치 끝에,
    ///    `ar_`이면 무기 레이스 끝에 붙인다. 이름은 게임 번역 파일(`resource/csgo_koreana.txt` 등)에서 읽는다.
    ///    사이트를 고치기 전에, 인터넷이 없어도, 게임이 맵을 더하면 바로 뜬다.
    /// 빠진 맵은 원래대로 맵 파일이 없으면 안 보인다.
    /// </summary>
    internal static class BuiltInMaps
    {
        private const string Url = "https://cs2.logstone.net/maps.json";

        /// <summary>찾기로 붙이는 모드. 윙맨·탈환·러시는 전용 배치가 있어야 해서 찾기로는 안 붙인다.</summary>
        private static readonly string[] ClassicKeys = { "casual", "competitive", "deathmatch" };

        /// <summary>
        /// 파일은 있지만 연습 탭에 따로 안 뜨는 맵. 사이트 목록에 `ignore`가 있으면 그걸로 바꾼다.
        /// 고대 밤판은 메뉴에 그림·이름이 있는 변형판이라 기본 고대와 겹친다 — 띄울지는 사이트에서 바꿀 수 있다.
        /// </summary>
        private static readonly string[] DefaultIgnore = { "de_ancient_night" };

        private static Dictionary<string, GameMap[]> _defaults;
        private static string[] _ignore = DefaultIgnore;
        private static Dictionary<string, MapName> _names;

        /// <summary>이 모드에 띄울 기본 맵 전부(설치 여부는 부르는 쪽이 본다). 목록 + 찾은 것.</summary>
        public static GameMap[] For(GameMode mode)
        {
            string prefix = ClassicKeys.Contains(mode.Key) ? null : mode.Key == "armsrace" ? "ar_" : "";
            if (prefix == "") return mode.Maps;

            HashSet<string> known = new HashSet<string>(
                GameData.Modes.SelectMany(m => m.Maps).Select(m => m.Id), StringComparer.OrdinalIgnoreCase);

            var found = Installed()
                .Where(id => prefix == null ? id.StartsWith("de_") || id.StartsWith("cs_") : id.StartsWith(prefix))
                .Where(id => !known.Contains(id) && !_ignore.Contains(id, StringComparer.OrdinalIgnoreCase))
                .Select(Discovered);
            return mode.Maps.Concat(found).ToArray();
        }

        /// <summary>모든 모드에서 보일 수 있는 기본 맵 아이디 — 그림을 꺼낼 때 쓴다.</summary>
        public static string[] AllIds()
        {
            return GameData.Modes.SelectMany(For).Select(m => m.Id).Distinct().ToArray();
        }

        /// <summary>
        /// 게임 폴더의 맵 파일 이름들. `_vanity`(메뉴 배경용)나 쪼갠 조각(`_000`)은 맵이 아니다.
        /// </summary>
        internal static string[] Installed()
        {
            string dir = Cs2Paths.MapsDir;
            if (dir == null || !Directory.Exists(dir)) return new string[0];
            try
            {
                return Directory.GetFiles(dir, "*.vpk")
                    .Select(Path.GetFileNameWithoutExtension)
                    .Select(n => n.ToLowerInvariant())
                    .Where(n => !n.EndsWith("_vanity") && !Regex.IsMatch(n, @"_(dir|\d{3})$"))
                    .OrderBy(n => n, StringComparer.Ordinal)
                    .ToArray();
            }
            catch (Exception) { return new string[0]; }
        }

        private static GameMap Discovered(string id)
        {
            MapName known = NameOf(id);
            if (known != null) return new GameMap(id, known.Ko ?? known.En, known.En ?? known.Ko);

            // 번역 파일을 아직 못 읽었으면 이름 앞부분을 떼고 첫 글자만 키운다(de_cbble → Cbble)
            string bare = id.Substring(id.IndexOf('_') + 1);
            string guess = bare.Length == 0 ? id : char.ToUpperInvariant(bare[0]) + bare.Substring(1);
            return new GameMap(id, guess, guess);
        }

        // ---------- 게임 번역 파일에서 이름 ----------

        private static MapName NameOf(string id)
        {
            if (_names == null) _names = LoadNames();
            MapName name;
            return _names.TryGetValue(id, out name) ? name : null;
        }

        /// <summary>
        /// 목록에 없는 맵의 이름을 아직 모르면 게임 번역 파일에서 읽어 적어 둔다. 돌려주는 값은 「새로 안 게 있나」.
        /// 번역 파일이 한 언어에 5MB쯤이라 뒤에서 부르고, 모르는 맵이 없으면 열지도 않는다.
        /// </summary>
        public static bool LearnNames()
        {
            string[] unknown = GameData.Modes.Where(m => ClassicKeys.Contains(m.Key) || m.Key == "armsrace")
                .SelectMany(For).Select(m => m.Id)
                .Where(id => NameOf(id) == null && !GameData.Modes.Any(m => m.Maps.Any(x => x.Id == id)))
                .Distinct().ToArray();
            if (unknown.Length == 0 || Cs2Paths.InstallDir == null) return false;

            string pak = Path.Combine(Cs2Paths.CsgoDir, "pak01_dir.vpk");
            string ko, en;
            try
            {
                ko = Vpk.ReadText(pak, "resource/csgo_koreana.txt");
                en = Vpk.ReadText(pak, "resource/csgo_english.txt");
            }
            catch (Exception) { return false; }   // 게임 업데이트 중이거나 형식이 바뀌었으면 짐작한 이름으로 남는다

            bool gained = false;
            foreach (string id in unknown)
            {
                string k = Lookup(ko, id), e = Lookup(en, id);
                if (k == null && e == null) continue;
                _names[id] = new MapName { Id = id, Ko = k, En = e };
                gained = true;
            }
            if (gained) SaveNames();
            return gained;
        }

        /// <summary>번역 파일 한 줄 꼴 — `"SFUI_Map_de_cbble"   "코블"`.</summary>
        internal static string Lookup(string text, string id)
        {
            if (text == null) return null;
            Match m = Regex.Match(text, "\"SFUI_Map_" + Regex.Escape(id) + "\"\\s+\"([^\"\\r\\n]+)\"", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }

        private static string NamesPath { get { return Path.Combine(AppFolder.Dir, "map-names.json"); } }

        private static Dictionary<string, MapName> LoadNames()
        {
            var names = new Dictionary<string, MapName>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!File.Exists(NamesPath)) return names;
                using (var stream = new MemoryStream(File.ReadAllBytes(NamesPath)))
                    foreach (MapName n in (MapName[])NamesSerializer.ReadObject(stream))
                        if (n != null && !string.IsNullOrEmpty(n.Id)) names[n.Id] = n;
            }
            catch (Exception) { }   // 깨졌으면 다시 읽으면 된다
            return names;
        }

        private static void SaveNames()
        {
            try
            {
                Directory.CreateDirectory(AppFolder.Dir);
                using (var stream = new MemoryStream())
                {
                    NamesSerializer.WriteObject(stream, _names.Values.ToArray());
                    File.WriteAllBytes(NamesPath, stream.ToArray());
                }
            }
            catch (Exception) { }
        }

        // ---------- 사이트 목록 ----------

        /// <summary>켤 때 한 번 — 받아 둔 목록이 있으면 먼저 그걸로 맞춘다. 네트워크는 안 탄다.</summary>
        public static void ApplySaved()
        {
            Catalog saved = Parse(Saved());
            if (saved != null) Apply(saved);
        }

        /// <summary>
        /// 사이트에서 받아 와 적어 두고 맞춘다. 전과 달라졌으면 true — 그때만 화면을 다시 그린다. 뒤에서 부른다.
        /// </summary>
        public static bool Fetch()
        {
            string json = Download();
            Catalog parsed = Parse(json);
            if (parsed == null) return false;   // 못 받았거나 깨진 파일로 받아 둔 것을 덮지 않는다
            if (json == Saved()) return false;

            try
            {
                Directory.CreateDirectory(AppFolder.Dir);
                File.WriteAllText(FilePath, json, Encoding.UTF8);
            }
            catch (Exception) { }   // 못 적어도 이번 실행에는 쓴다

            Apply(parsed);
            return true;
        }

        /// <summary>확인기가 받은 것 대신 쓸 목록을 넣는다. null이면 코드에 박힌 목록으로 되돌린다.</summary>
        internal static bool Use(string json)
        {
            Restore();
            if (json == null) return true;
            Catalog parsed = Parse(json);
            if (parsed == null) return false;
            Apply(parsed);
            return true;
        }

        /// <summary>
        /// 모드 키가 맞는 것만 바꾼다. 모르는 모드·빈 목록은 건너뛴다 — 사이트를 잘못 적어도 맵이 다 사라지지 않게.
        /// 이름은 사이트 `names` → 코드에 박힌 이름 → 게임 번역 파일 → 짐작 순으로 찾는다.
        /// </summary>
        private static void Apply(Catalog catalog)
        {
            Restore();

            var builtIn = new Dictionary<string, GameMap>(StringComparer.OrdinalIgnoreCase);
            foreach (GameMap m in _defaults.Values.SelectMany(x => x)) builtIn[m.Id] = m;
            var given = new Dictionary<string, MapName>(StringComparer.OrdinalIgnoreCase);
            foreach (MapName n in catalog.Names ?? new MapName[0])
                if (n != null && !string.IsNullOrEmpty(n.Id)) given[n.Id] = n;

            foreach (ModeMaps entry in catalog.Modes ?? new ModeMaps[0])
            {
                if (entry == null || entry.Maps == null) continue;
                GameMode mode = GameData.Modes.FirstOrDefault(x => x.Key == entry.Key);
                string[] ids = entry.Maps.Where(id => !string.IsNullOrWhiteSpace(id) && Regex.IsMatch(id, @"^[a-z0-9_]+$"))
                    .Distinct().ToArray();
                if (mode == null || ids.Length == 0) continue;

                mode.Maps = ids.Select(id =>
                {
                    MapName n;
                    if (given.TryGetValue(id, out n) && (n.Ko != null || n.En != null))
                        return new GameMap(id, n.Ko ?? n.En, n.En ?? n.Ko);
                    GameMap b;
                    if (builtIn.TryGetValue(id, out b)) return new GameMap(id, b.Ko, b.En);
                    return Discovered(id);
                }).ToArray();
            }

            if (catalog.Ignore != null) _ignore = catalog.Ignore.Where(x => x != null).ToArray();
        }

        /// <summary>코드에 박힌 목록으로 되돌린다. 처음 부를 때 그 목록을 떠 둔다.</summary>
        private static void Restore()
        {
            if (_defaults == null) _defaults = GameData.Modes.ToDictionary(m => m.Key, m => m.Maps);
            foreach (GameMode m in GameData.Modes) m.Maps = _defaults[m.Key];
            _ignore = DefaultIgnore;
        }

        internal static Catalog Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                    return (Catalog)CatalogSerializer.ReadObject(stream);
            }
            catch (Exception) { return null; }
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
            catch (Exception) { return null; }   // 인터넷이 없어도 앱은 쓴다
        }

        private static string Saved()
        {
            try { return File.Exists(FilePath) ? File.ReadAllText(FilePath, Encoding.UTF8) : null; }
            catch (Exception) { return null; }
        }

        private static string FilePath { get { return Path.Combine(AppFolder.Dir, "maps.json"); } }

        private static readonly DataContractJsonSerializer CatalogSerializer = new DataContractJsonSerializer(typeof(Catalog));
        private static readonly DataContractJsonSerializer NamesSerializer = new DataContractJsonSerializer(typeof(MapName[]));
    }

    [DataContract]
    internal sealed class Catalog
    {
        [DataMember(Name = "modes")] public ModeMaps[] Modes;
        [DataMember(Name = "names")] public MapName[] Names;

        /// <summary>파일은 있어도 찾기로 붙이지 않을 맵. 없으면 코드에 박힌 것을 쓴다.</summary>
        [DataMember(Name = "ignore")] public string[] Ignore;
    }

    [DataContract]
    internal sealed class ModeMaps
    {
        [DataMember(Name = "key")] public string Key;
        [DataMember(Name = "maps")] public string[] Maps;
    }

    [DataContract]
    internal sealed class MapName
    {
        [DataMember(Name = "id")] public string Id;
        [DataMember(Name = "ko")] public string Ko;
        [DataMember(Name = "en")] public string En;
    }

    /// <summary>게임 데이터 묶음(`pak01_dir.vpk`)에서 파일 하나를 꺼낸다. 목차 꼴은 Thumbs가 읽는 것과 같다.</summary>
    internal static class Vpk
    {
        public static string ReadText(string pak, string path)
        {
            string dir = Path.GetDirectoryName(path).Replace('\\', '/');
            string name = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path).TrimStart('.');

            using (var r = new BinaryReader(new BufferedStream(File.OpenRead(pak), 1 << 16)))
            {
                if (r.ReadUInt32() != 0x55AA1234 || r.ReadUInt32() != 2) return null;
                uint treeSize = r.ReadUInt32();
                r.ReadBytes(16);
                long treeEnd = 28 + treeSize;

                string e;
                while ((e = Text(r)).Length > 0)
                {
                    string p;
                    while ((p = Text(r)).Length > 0)
                    {
                        string f;
                        while ((f = Text(r)).Length > 0)
                        {
                            r.ReadUInt32();                                  // CRC
                            ushort preload = r.ReadUInt16();
                            ushort archive = r.ReadUInt16();
                            uint offset = r.ReadUInt32(), length = r.ReadUInt32();
                            r.ReadUInt16();                                  // 끝 표시
                            byte[] pre = r.ReadBytes(preload);
                            if (e == ext && p == dir && f == name)
                                return Decode(Read(pak, archive, offset, length, pre, treeEnd));
                        }
                    }
                }
            }
            return null;
        }

        private static byte[] Read(string pak, ushort archive, uint offset, uint length, byte[] pre, long treeEnd)
        {
            string file = archive == 0x7FFF ? pak : pak.Replace("_dir.vpk", "_" + archive.ToString("000") + ".vpk");
            long at = archive == 0x7FFF ? treeEnd + offset : offset;
            var data = new byte[pre.Length + length];
            Buffer.BlockCopy(pre, 0, data, 0, pre.Length);
            using (var f = File.OpenRead(file))
            {
                f.Seek(at, SeekOrigin.Begin);
                int read = 0;
                while (read < length)
                {
                    int n = f.Read(data, pre.Length + read, (int)length - read);
                    if (n <= 0) throw new EndOfStreamException();
                    read += n;
                }
            }
            return data;
        }

        /// <summary>번역 파일은 UTF-8이거나 UTF-16(BOM 붙음)이다.</summary>
        private static string Decode(byte[] data)
        {
            if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE) return Encoding.Unicode.GetString(data, 2, data.Length - 2);
            return Encoding.UTF8.GetString(data);
        }

        private static string Text(BinaryReader r)
        {
            var bytes = new List<byte>();
            byte b;
            while ((b = r.ReadByte()) != 0) bytes.Add(b);
            return Encoding.UTF8.GetString(bytes.ToArray());
        }
    }
}
