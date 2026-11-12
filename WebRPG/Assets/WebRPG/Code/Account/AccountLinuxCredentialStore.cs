#if UNITY_EDITOR_LINUX || (UNITY_STANDALONE_LINUX && !UNITY_EDITOR)
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace WebRPG
{
    // 透過 secret-tool 將 Cookie 寫入 Linux 桌面環境的 Secret Service。
    internal static class AccountLinuxCredentialStore
    {
        private const string SecretTool = "secret-tool";
        private const string Lookup = "lookup";
        private const string Store = "store --label=\"WebRPG remembered login\"";
        private const string Clear = "clear";
        private const string KeyArgument = " webrpg-account ";
        private const string UnavailableMessage = "WebRPG could not access Linux Secret Service. Check that " +
            "secret-tool and a desktop keyring are available.";
        private const int TimeoutMilliseconds = 10000;

        internal static string Read(string key) => Run(Lookup, key, null);

        internal static void Write(string key, string value) => Run(Store, key, value);

        internal static void Delete(string key) => Run(Clear, key, null);

        private static string Run(string command, string key, string value)
        {
            ProcessStartInfo start = new ProcessStartInfo(SecretTool, command + KeyArgument + key)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            try
            {
                using Process process = Process.Start(start);
                if (process == null) return string.Empty;
                Task<string> output = process.StandardOutput.ReadToEndAsync();
                Task<string> errors = process.StandardError.ReadToEndAsync();
                if (value != null) process.StandardInput.Write(value);
                process.StandardInput.Close();

                if (!process.WaitForExit(TimeoutMilliseconds))
                {
                    try { process.Kill(); }
                    catch (InvalidOperationException) { }
                    UnityEngine.Debug.LogWarning(UnavailableMessage);
                    return string.Empty;
                }

                if (!Task.WaitAll(new Task[] { output, errors }, TimeoutMilliseconds) ||
                    (process.ExitCode != 0 && (command == Store || !string.IsNullOrEmpty(errors.Result))))
                {
                    UnityEngine.Debug.LogWarning(UnavailableMessage);
                    return string.Empty;
                }

                return process.ExitCode == 0 ? output.Result.TrimEnd('\r', '\n') : string.Empty;
            }
            catch (Exception exception) when (
                exception is Win32Exception || exception is IOException || exception is AggregateException)
            {
                UnityEngine.Debug.LogWarning(UnavailableMessage);
                return string.Empty;
            }
        }
    }
}
#endif
