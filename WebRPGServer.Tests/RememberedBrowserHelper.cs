using System.Diagnostics;

namespace WebRPGServer.Tests;

// 啟動測試專用無頭瀏覽器並等待結果，不使用使用者原有設定檔。
internal static class RememberedBrowserHelper
{
    private const int VirtualTimeBudgetMilliseconds = 10000;

    public static async Task<string> RunAsync(string browser, string profile, Uri page, int timeoutSeconds, params string[] browserArguments)
    {
        ProcessStartInfo start = new(browser)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (string argument in new[] { "--headless=new", "--disable-gpu", "--no-first-run", "--no-default-browser-check",
            "--disable-background-networking", "--user-data-dir=" + profile, "--dump-dom",
            "--virtual-time-budget=" + VirtualTimeBudgetMilliseconds })
            start.ArgumentList.Add(argument);
        foreach (string argument in browserArguments) start.ArgumentList.Add(argument);
        start.ArgumentList.Add(page.AbsoluteUri);
        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> errors = process.StandardError.ReadToEndAsync();
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(timeoutSeconds));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch { if (!process.HasExited) process.Kill(true); throw; }
        string diagnostics = await errors;
        if (process.ExitCode != 0) throw new InvalidOperationException("Isolated browser exited with code " + process.ExitCode);
        string result = await output;
        if (string.IsNullOrWhiteSpace(result)) throw new InvalidOperationException("Isolated browser produced no DOM: " + diagnostics);
        return result;
    }
}
