#if UNITY_EDITOR || UNITY_STANDALONE
using System;
using System.Net;
using UnityEngine;
using UnityEngine.Networking;

#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
using DesktopCredentialStore = WebRPG.AccountWindowsCredentialStore;
#elif UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !UNITY_EDITOR)
using DesktopCredentialStore = WebRPG.AccountMacCredentialStore;
#elif UNITY_EDITOR_LINUX || (UNITY_STANDALONE_LINUX && !UNITY_EDITOR)
using DesktopCredentialStore = WebRPG.AccountLinuxCredentialStore;
#endif

namespace WebRPG
{
    // 依伺服器保留輪替後的記住我 Cookie，讓下次啟動可恢復登入。
    internal static class AccountNativeCookieHelper
    {
        private const string SessionKeyPrefix = "WebRPG.Account.RememberedCookie:";
        private const string CookieName = "__Secure-WebRPGRemember";
        private const string CookieHeader = "Cookie";
        private const string SetCookieHeader = "Set-Cookie";

        internal static async Awaitable ConfigureAsync(UnityWebRequest request)
        {
            Uri address = request.uri;
            if (address.Scheme != Uri.UriSchemeHttps && !address.IsLoopback) return;

            request.redirectLimit = 0;
            UnityWebRequest.ClearCookieCache(address);
            string key = SessionKeyPrefix + address.GetLeftPart(UriPartial.Authority);
            string header;
            await Awaitable.BackgroundThreadAsync();
            try { header = DesktopCredentialStore.Read(key); }
            finally { await Awaitable.MainThreadAsync(); }

            Cookie cookie = ReadCookie(address, header);
            if (cookie != null) request.SetRequestHeader(CookieHeader, CookieName + "=" + cookie.Value);
        }

        internal static async Awaitable SaveResponseAsync(UnityWebRequest request)
        {
            Uri address = request.uri;
            if (address.Scheme != Uri.UriSchemeHttps && !address.IsLoopback) return;

            string header = request.GetResponseHeader(SetCookieHeader);
            if (string.IsNullOrEmpty(header)) return;

            string key = SessionKeyPrefix + address.GetLeftPart(UriPartial.Authority);
            bool remember = ReadCookie(address, header) != null;
            await Awaitable.BackgroundThreadAsync();
            try
            {
                if (remember) DesktopCredentialStore.Write(key, header);
                else DesktopCredentialStore.Delete(key);
            }
            finally { await Awaitable.MainThreadAsync(); }
        }

        private static Cookie ReadCookie(Uri address, string header)
        {
            if (string.IsNullOrEmpty(header)) return null;

            try
            {
                CookieContainer cookies = new CookieContainer();
                cookies.SetCookies(address, header);
                Cookie cookie = cookies.GetCookies(address)[CookieName];
                return cookie != null && !string.IsNullOrEmpty(cookie.Value) ? cookie : null;
            }
            catch (CookieException)
            {
                return null;
            }
        }
    }
}
#endif
