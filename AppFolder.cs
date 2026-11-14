using System;
using System.IO;

namespace CS2PracticeHost
{
    /// <summary>설정·창작마당 맵 정보·타일 그림·안내문을 두는 `%APPDATA%\CS2PracticeHost`.</summary>
    internal static class AppFolder
    {
        /// <summary>
        /// 확인기가 쓰는 사람의 실제 설정을 덮지 않게 자기 임시 폴더로 돌릴 때만 채운다.
        /// GetFolderPath는 셸에서 읽어 APPDATA 환경변수로는 못 옮긴다.
        /// </summary>
        internal static string Override;

        public static string Dir
        {
            get
            {
                return Override ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CS2PracticeHost");
            }
        }
    }
}
