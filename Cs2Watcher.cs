using System;
using System.Diagnostics;
using System.Linq;

namespace CS2PracticeHost
{
    internal enum Cs2State
    {
        /// <summary>CS2가 꺼져 있다.</summary>
        Off,

        /// <summary>앱이 켠 CS2가 아니라 로그가 없다. 서버가 열려 있어도 주소를 읽을 수 없다.</summary>
        NoLog,

        /// <summary>CS2는 돌지만 열린 서버가 없다 (메인 화면이거나 방장이 나갔다).</summary>
        NoServer,

        ServerOpen,
    }

    internal sealed class Cs2Status
    {
        public Cs2State State;

        /// <summary><see cref="Cs2State.ServerOpen"/> 일 때만 채워진다.</summary>
        public ServerInfo Server;
    }

    /// <summary>
    /// 지금 CS2가 어떤 상태인지 한곳에서 판단한다.
    /// 상태 표시줄과 주소 복사가 같은 순서로 따져야 해서, 그 순서를 두 군데 두지 않는다.
    /// </summary>
    internal static class Cs2Watcher
    {
        public static Cs2Status Read()
        {
            Process cs2 = Cs2Process.Find();
            if (cs2 == null) return new Cs2Status { State = Cs2State.Off };
            if (!Cs2Process.HasCondebug(cs2)) return new Cs2Status { State = Cs2State.NoLog };

            ServerInfo server = ServerSince(cs2);
            return server == null
                ? new Cs2Status { State = Cs2State.NoServer }
                : new Cs2Status { State = Cs2State.ServerOpen, Server = server };
        }

        /// <summary>
        /// 서버가 지금 윙맨인가. 윙맨에 들어간 직후의 첫 changelevel은 모드를 못 바꾸고 맵만 바꾼다
        /// (두 번째부터 바뀐다, 2026-10-24 실기). 그 자리에서만 「F10 두 번」을 안내하려고 본다.
        /// </summary>
        public static bool ServerIsInWingman()
        {
            Process cs2 = Cs2Process.Find();
            if (cs2 == null || !Cs2Process.HasCondebug(cs2)) return false;

            GameMode wingman = GameData.Modes.First(m => m.Key == "wingman");
            int type, mode;
            try
            {
                return ConsoleLog.CurrentGameMode(cs2.StartTime.AddSeconds(-5), out type, out mode)
                    && type == wingman.Type && mode == wingman.Mode;
            }
            catch (Exception) { return false; }
        }

        /// <summary>이번 CS2 세션에서 열린 서버만 인정한다. 로그는 껐다 켜도 남아 죽은 주소를 집게 된다.</summary>
        private static ServerInfo ServerSince(Process cs2)
        {
            try { return ConsoleLog.CurrentServer(cs2.StartTime.AddSeconds(-5)); }
            catch (Exception) { return null; }
        }
    }
}
