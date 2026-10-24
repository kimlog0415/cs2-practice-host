using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CS2PracticeHost
{
    internal sealed class ServerInfo
    {
        /// <summary>[A:1:1509803029:51595] 형태의 서버 주소.</summary>
        public string Address;
        public DateTime OpenedAt;

        /// <summary>콘솔에 직접 치는 형태.</summary>
        public string ConnectCommand { get { return "connect " + Address; } }

        /// <summary>
        /// 친구에게 보내는 링크. 누르면 중계 페이지가 열리고 거기서 게임으로 넘어간다.
        /// steam:// 주소를 바로 보내지 않는 이유는 메신저가 그걸 링크로 만들어 주지 않고,
        /// 주소 안의 공백에서 뒤를 잘라 버리기 때문이다. https는 어디서든 링크가 된다.
        /// </summary>
        public string JoinLink
        {
            get { return "https://cs2.logstone.net/j/?a=" + Address.Trim('[', ']'); }
        }
    }

    /// <summary>-condebug 가 남기는 console.log 에서 서버 상태를 읽는다.</summary>
    internal static class ConsoleLog
    {
        private static readonly Regex ServerIdLine = new Regex(@"ServerSteamID=(\[A:[^\]]+\])");
        private static readonly Regex ShutdownLine = new Regex(
            @"Server shutting down: NETWORK_DISCONNECT_(DISCONNECT_BY_USER|REQUEST_HOSTSTATE_IDLE)");
        private static readonly Regex GameTypeLine = new Regex(@"c_game_type int\( (\d+) ");
        private static readonly Regex GameModeLine = new Regex(@"c_game_mode int\( (\d+) ");

        /// <summary>
        /// 서버가 지금 도는 모드. 맵을 불러올 때마다 서버가 찍는 값이라, F10이 실제로 먹었는지는
        /// 앱 설정이 아니라 이걸로 안다. <paramref name="since"/> 이후 기록이 없으면 false.
        /// </summary>
        public static bool CurrentGameMode(DateTime since, out int type, out int mode)
        {
            type = mode = -1;
            foreach (string line in ReadLines())
            {
                Match t = GameTypeLine.Match(line);
                if (t.Success && ParseTime(line) >= since) type = int.Parse(t.Groups[1].Value);
                Match m = GameModeLine.Match(line);
                if (m.Success && ParseTime(line) >= since) mode = int.Parse(m.Groups[1].Value);
            }
            return type >= 0 && mode >= 0;
        }

        /// <summary>
        /// 지금 열려 있는 서버. 이미 닫혔거나 <paramref name="since"/> 이전 기록이면 null.
        /// 로그는 CS2를 껐다 켜도 남아 있어서, 시각을 안 보면 죽은 주소를 집어 든다.
        /// </summary>
        public static ServerInfo CurrentServer(DateTime since)
        {
            string last = null;
            foreach (string line in ReadLines())
            {
                if (ServerIdLine.IsMatch(line) || ShutdownLine.IsMatch(line)) last = line;
            }

            Match opened = last == null ? Match.Empty : ServerIdLine.Match(last);
            if (!opened.Success) return null;

            DateTime at = ParseTime(last);
            if (at < since) return null;

            return new ServerInfo { Address = opened.Groups[1].Value, OpenedAt = at };
        }

        /// <summary>로그 줄은 "MM/dd HH:mm:ss ..." 로 시작한다 (연도가 없어 올해로 읽는다).</summary>
        private static DateTime ParseTime(string line)
        {
            if (line.Length < 14) return DateTime.MinValue;

            string stamp = DateTime.Now.Year + "/" + line.Substring(0, 14);
            DateTime parsed;
            if (DateTime.TryParseExact(stamp, "yyyy/MM/dd HH:mm:ss",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
                return parsed;

            return DateTime.MinValue;
        }

        /// <summary>CS2가 쓰는 중인 파일이라 공유 모드로 연다.</summary>
        private static IEnumerable<string> ReadLines()
        {
            string path = Cs2Paths.ConsoleLog;
            if (!File.Exists(path)) yield break;

            StreamReader reader;
            try
            {
                reader = new StreamReader(
                    new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite),
                    Encoding.UTF8);
            }
            catch (IOException) { yield break; }

            using (reader)
            {
                string line;
                while ((line = reader.ReadLine()) != null) yield return line;
            }
        }
    }
}
