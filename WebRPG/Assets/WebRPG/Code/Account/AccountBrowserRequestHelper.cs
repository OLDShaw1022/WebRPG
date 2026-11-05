using System;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace WebRPG
{
    // 讓 WebGL 帳號要求攜帶瀏覽器管理的 Cookie，其他平台沿用原生網路行為。
    public static class AccountBrowserRequestHelper
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        private const string BrowserLibrary = "__Internal";

        [DllImport(BrowserLibrary)]
        private static extern void WebRPG_RegisterAccountRequest(string url);
#endif

        public static void Configure(Uri address)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebRPG_RegisterAccountRequest(address.AbsoluteUri);
#endif
        }
    }
}
