using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;

namespace WebRPGServer.Tests;

// 管理整合測試專用伺服器的啟動、輸出與關閉。
internal sealed class LocalLobbyServer : IAsyncDisposable
{
    private readonly Process _process;
    private readonly Task<string> _output;
    private readonly Task<string> _errors;
    public Uri Address { get; }

    private LocalLobbyServer(Process process, int port)
    {
        _process = process;
        _output = process.StandardOutput.ReadToEndAsync();
        _errors = process.StandardError.ReadToEndAsync();
        Address = new Uri($"ws://127.0.0.1:{port}/ws?channel=lobby");
    }

    public static async Task<LocalLobbyServer> StartAsync()
    {
        TcpListener reservation = new(IPAddress.Loopback, 0);
        reservation.Start();
        int port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();
        ProcessStartInfo start = new("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = TestContext.CurrentContext.TestDirectory
        };
        start.ArgumentList.Add(Path.Combine(TestContext.CurrentContext.TestDirectory, "WebRPGServer.dll"));
        start.Environment["PORT"] = port.ToString();
        start.Environment["ConnectionStrings__GameDatabase"] = string.Empty;
        LocalLobbyServer server = new(Process.Start(start)!, port);
        try
        {
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(15));
            while (true)
            {
                if (server._process.HasExited) throw new InvalidOperationException("Test server exited before becoming ready.");
                try
                {
                    using TcpClient probe = new();
                    await probe.ConnectAsync(IPAddress.Loopback, port, timeout.Token);
                    return server;
                }
                catch (SocketException) { await Task.Delay(50, timeout.Token); }
            }
        }
        catch
        {
            await server.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!_process.HasExited) _process.Kill(true);
            await _process.WaitForExitAsync();
            TestContext.Progress.WriteLine(await _output);
            TestContext.Progress.WriteLine(await _errors);
        }
        finally { _process.Dispose(); }
    }
}
