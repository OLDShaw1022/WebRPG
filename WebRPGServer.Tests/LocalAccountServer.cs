using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;

namespace WebRPGServer.Tests;

// 以獨立本機資料庫啟動正式伺服器，供 HTTP 與 WebSocket 整合驗證。
internal sealed class LocalAccountServer : IAsyncDisposable
{
    private const int StartupTimeoutSeconds = 20;
    private const int RetryDelayMilliseconds = 50;
    private readonly Process _process;
    private readonly Task<string> _output;
    private readonly Task<string> _errors;
    public Uri Address { get; }

    private LocalAccountServer(Process process, int port)
    {
        _process = process;
        _output = process.StandardOutput.ReadToEndAsync();
        _errors = process.StandardError.ReadToEndAsync();
        Address = new Uri($"http://127.0.0.1:{port}/");
    }

    public static async Task<LocalAccountServer> StartAsync(LocalAccountDatabase database)
    {
        TcpListener reservation = new(IPAddress.Loopback, 0);
        reservation.Start();
        int port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();
        ProcessStartInfo start = new("dotnet")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = TestContext.CurrentContext.TestDirectory
        };
        start.ArgumentList.Add(Path.Combine(TestContext.CurrentContext.TestDirectory, "WebRPGServer.dll"));
        start.Environment["PORT"] = port.ToString();
        start.Environment["ConnectionStrings__GameDatabase"] = database.ConnectionString;
        LocalAccountServer server = new(Process.Start(start)!, port);
        try
        {
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(StartupTimeoutSeconds));
            while (true)
            {
                if (server._process.HasExited)
                    throw new InvalidOperationException($"Local account server exited: {await server._output}\n{await server._errors}");
                try
                {
                    using TcpClient probe = new();
                    await probe.ConnectAsync(IPAddress.Loopback, port, timeout.Token);
                    return server;
                }
                catch (SocketException) { await Task.Delay(RetryDelayMilliseconds, timeout.Token); }
            }
        }
        catch { await server.DisposeAsync(); throw; }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_process.HasExited) _process.Kill(true);
        await _process.WaitForExitAsync();
        await Task.WhenAll(_output, _errors);
        _process.Dispose();
    }
}
