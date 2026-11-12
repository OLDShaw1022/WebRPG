#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace WebRPG
{
    // 使用目前 Windows 使用者的認證管理員保存記住我 Cookie。
    internal static class AccountWindowsCredentialStore
    {
        private const string CredentialLibrary = "advapi32.dll";
        private const uint GenericCredential = 1;
        private const uint PersistOnThisComputer = 2;
        private const int CredentialNotFound = 1168;
        private const string WriteFailedMessage =
            "Could not save the WebRPG remembered login in Windows Credential Manager.";
        private const string DeleteFailedMessage =
            "Could not clear the WebRPG remembered login in Windows Credential Manager.";

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct Credential
        {
            public uint Flags;
            public uint Type;
            public string TargetName;
            public IntPtr Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public uint BlobSize;
            public IntPtr Blob;
            public uint Persist;
            public uint AttributeCount;
            public IntPtr Attributes;
            public IntPtr TargetAlias;
            public IntPtr UserName;
        }

        [DllImport(CredentialLibrary, CharSet = CharSet.Unicode, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CredReadW(string target, uint type, uint flags, out IntPtr credential);

        [DllImport(CredentialLibrary, CharSet = CharSet.Unicode, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CredWriteW(ref Credential credential, uint flags);

        [DllImport(CredentialLibrary, CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CredDeleteW(string target, uint type, uint flags);

        [DllImport(CredentialLibrary, ExactSpelling = true)]
        private static extern void CredFree(IntPtr credential);

        internal static string Read(string key)
        {
            if (!CredReadW(key, GenericCredential, 0, out IntPtr pointer)) return string.Empty;

            try
            {
                Credential credential = Marshal.PtrToStructure<Credential>(pointer);
                return Marshal.PtrToStringUni(credential.Blob, (int)credential.BlobSize / sizeof(char));
            }
            finally
            {
                CredFree(pointer);
            }
        }

        internal static void Write(string key, string value)
        {
            IntPtr blob = Marshal.StringToCoTaskMemUni(value);
            try
            {
                Credential credential = new Credential
                {
                    Type = GenericCredential,
                    TargetName = key,
                    BlobSize = (uint)(value.Length * sizeof(char)),
                    Blob = blob,
                    Persist = PersistOnThisComputer
                };
                if (!CredWriteW(ref credential, 0)) Debug.LogWarning(WriteFailedMessage);
            }
            finally
            {
                Marshal.ZeroFreeCoTaskMemUnicode(blob);
            }
        }

        internal static void Delete(string key)
        {
            if (!CredDeleteW(key, GenericCredential, 0) && Marshal.GetLastWin32Error() != CredentialNotFound)
                Debug.LogWarning(DeleteFailedMessage);
        }
    }
}
#endif
