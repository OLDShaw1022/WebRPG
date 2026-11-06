#if UNITY_EDITOR || UNITY_STANDALONE_WIN
using System;
using System.Net;
using UnityEngine.Networking;
#if UNITY_EDITOR
using UnityEditor;
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

        internal static void Configure(UnityWebRequest request)
        {
            Uri address = request.uri;
            if (address.Scheme != Uri.UriSchemeHttps && !address.IsLoopback) return;

            request.redirectLimit = 0;
            UnityWebRequest.ClearCookieCache(address);
            string key = SessionKeyPrefix + address.GetLeftPart(UriPartial.Authority);
#if UNITY_EDITOR
            string header = SessionState.GetString(key, string.Empty);
#else
            string header = AccountWindowsCredentialStore.Read(key);
#endif
            Cookie cookie = ReadCookie(address, header);
            if (cookie != null) request.SetRequestHeader(CookieHeader, CookieName + "=" + cookie.Value);
        }

        internal static void SaveResponse(UnityWebRequest request)
        {
            Uri address = request.uri;
            if (address.Scheme != Uri.UriSchemeHttps && !address.IsLoopback) return;

            string header = request.GetResponseHeader(SetCookieHeader);
            if (string.IsNullOrEmpty(header)) return;

            string key = SessionKeyPrefix + address.GetLeftPart(UriPartial.Authority);
            bool remember = ReadCookie(address, header) != null;
#if UNITY_EDITOR
            if (remember) SessionState.SetString(key, header);
            else SessionState.EraseString(key);
#else
            if (remember) AccountWindowsCredentialStore.Write(key, header);
            else AccountWindowsCredentialStore.Delete(key);
#endif
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
