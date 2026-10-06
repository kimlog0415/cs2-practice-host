using System;
using System.Threading;

namespace Cs2Dummy
{
    /// <summary>
    /// 이름만 cs2인 빈 프로세스. 게임을 켜지 않고 「켜져 있다」 상태를 만든다.
    /// 앱은 프로세스 이름과 명령줄만 보므로 이것으로 충분하다. 아무 일도 하지 않는다.
    /// </summary>
    internal static class Dummy
    {
        private static void Main(string[] args)
        {
            Console.WriteLine("cs2 흉내 — 받은 인자: " + string.Join(" ", args));
            Thread.Sleep(Timeout.Infinite);
        }
    }
}
