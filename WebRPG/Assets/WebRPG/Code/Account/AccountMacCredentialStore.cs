#if UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !UNITY_EDITOR)
using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace WebRPG
{
    // 以目前 macOS 使用者的 Keychain 保存記住我 Cookie。
    internal static class AccountMacCredentialStore
    {
        private const string Security = "/System/Library/Frameworks/Security.framework/Security";
        private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
        private const int Success = 0;
        private const int ItemNotFound = -25300;
        private const string WriteFailedMessage = "Could not save the WebRPG remembered login in macOS Keychain.";
        private const string DeleteFailedMessage = "Could not clear the WebRPG remembered login in macOS Keychain.";
        private static readonly byte[] Service = Encoding.UTF8.GetBytes("WebRPG");

        [DllImport(Security)]
        private static extern int SecKeychainFindGenericPassword(IntPtr keychain, uint serviceLength, byte[] service,
            uint accountLength, byte[] account, out uint passwordLength, out IntPtr password, IntPtr item);

        [DllImport(Security, EntryPoint = nameof(SecKeychainFindGenericPassword))]
        private static extern int FindItem(IntPtr keychain, uint serviceLength, byte[] service,
            uint accountLength, byte[] account, IntPtr passwordLength, IntPtr password, out IntPtr item);

        [DllImport(Security)]
        private static extern int SecKeychainAddGenericPassword(IntPtr keychain, uint serviceLength, byte[] service,
            uint accountLength, byte[] account, uint passwordLength, byte[] password, IntPtr item);

        [DllImport(Security)]
        private static extern int SecKeychainItemModifyAttributesAndData(IntPtr item, IntPtr attributes, uint length, byte[] password);

        [DllImport(Security)]
        private static extern int SecKeychainItemDelete(IntPtr item);

        [DllImport(Security)]
        private static extern int SecKeychainItemFreeContent(IntPtr attributes, IntPtr data);

        [DllImport(CoreFoundation)]
        private static extern void CFRelease(IntPtr item);

        internal static string Read(string key)
        {
            byte[] account = Encoding.UTF8.GetBytes(key);
            int status = SecKeychainFindGenericPassword(IntPtr.Zero, (uint)Service.Length, Service, (uint)account.Length,
                account, out uint length, out IntPtr data, IntPtr.Zero);
            if (status != Success || data == IntPtr.Zero) return string.Empty;

            try
            {
                byte[] bytes = new byte[length];
                try
                {
                    Marshal.Copy(data, bytes, 0, bytes.Length);
                    return Encoding.UTF8.GetString(bytes);
                }
                finally { Array.Clear(bytes, 0, bytes.Length); }
            }
            finally { SecKeychainItemFreeContent(IntPtr.Zero, data); }
        }

        internal static void Write(string key, string value)
        {
            byte[] account = Encoding.UTF8.GetBytes(key);
            byte[] password = Encoding.UTF8.GetBytes(value);
            int status = FindItem(IntPtr.Zero, (uint)Service.Length, Service, (uint)account.Length, account,
                IntPtr.Zero, IntPtr.Zero, out IntPtr item);
            try
            {
                if (status == Success)
                    status = SecKeychainItemModifyAttributesAndData(item, IntPtr.Zero, (uint)password.Length, password);
                else if (status == ItemNotFound)
                    status = SecKeychainAddGenericPassword(IntPtr.Zero, (uint)Service.Length, Service,
                        (uint)account.Length, account, (uint)password.Length, password, IntPtr.Zero);

                if (status != Success) Debug.LogWarning(WriteFailedMessage);
            }
            finally
            {
                if (item != IntPtr.Zero) CFRelease(item);
                Array.Clear(password, 0, password.Length);
            }
        }

        internal static void Delete(string key)
        {
            byte[] account = Encoding.UTF8.GetBytes(key);
            int status = FindItem(IntPtr.Zero, (uint)Service.Length, Service, (uint)account.Length, account,
                IntPtr.Zero, IntPtr.Zero, out IntPtr item);
            try
            {
                if (status == Success) status = SecKeychainItemDelete(item);
                if (status != Success && status != ItemNotFound) Debug.LogWarning(DeleteFailedMessage);
            }
            finally
            {
                if (item != IntPtr.Zero) CFRelease(item);
            }
        }
    }
}
#endif
