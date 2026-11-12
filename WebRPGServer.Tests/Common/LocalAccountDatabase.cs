using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Npgsql;

namespace WebRPGServer.Tests;

// 為帳號整合測試建立獨立 PostgreSQL cluster，只監聽本機且不註冊服務。
internal sealed class LocalAccountDatabase : IAsyncDisposable
{
    private const int StartupTimeoutSeconds = 30;
    private readonly string _binaryDirectory;
    public string DataDirectory { get; }
    public int Port { get; }
    public string ConnectionString { get; }

    private LocalAccountDatabase(string binaryDirectory, string dataDirectory, int port)
    {
        _binaryDirectory = binaryDirectory;
        DataDirectory = dataDirectory;
        Port = port;
        ConnectionString = new NpgsqlConnectionStringBuilder
        {
            Host = "127.0.0.1", Port = port, Username = "webrpg_test", Database = "webrpg_accounts",
            Pooling = false, IncludeErrorDetail = false
        }.ConnectionString;
    }

    public static async Task<LocalAccountDatabase> StartAsync(string binaryDirectory)
    {
        string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WebRPGTest", "Runs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        TcpListener reservation = new(IPAddress.Loopback, 0);
        reservation.Start();
        int port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();
        LocalAccountDatabase database = new(binaryDirectory, Path.Combine(directory, "data"), port);
        await database.RunAsync(
            "initdb.exe", "-D", database.DataDirectory, "-U", "webrpg_test", "-A", "trust", "--no-locale", "-E",
            "UTF8");
        try
        {
            await database.StartProcessAsync();
            NpgsqlConnectionStringBuilder admin = new(database.ConnectionString) { Database = "postgres" };
            await using NpgsqlDataSource source = NpgsqlDataSource.Create(admin.ConnectionString);
            await using NpgsqlCommand create = source.CreateCommand("CREATE DATABASE webrpg_accounts;");
            await create.ExecuteNonQueryAsync();
            return database;
        }
        catch { await database.DisposeAsync(); throw; }
    }

    public async Task RestartAsync()
    {
        await RunAsync("pg_ctl.exe", "-D", DataDirectory, "-m", "fast", "-w", "stop");
        await StartProcessAsync();
    }

    private async Task StartProcessAsync()
    {
        await RunAsync(
            "pg_ctl.exe", "-D", DataDirectory, "-l",
            Path.Combine(Path.GetDirectoryName(DataDirectory)!, "postgres.log"),
            "-o", $"-h 127.0.0.1 -p {Port}", "-w", "start");
    }

    private async Task RunAsync(string executable, params string[] arguments)
    {
        bool captureOutput = executable != "pg_ctl.exe";
        ProcessStartInfo start = new(Path.Combine(_binaryDirectory, executable))
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = captureOutput, RedirectStandardError = captureOutput
        };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using Process process = Process.Start(start)!;
        Task<string> output = captureOutput ? process.StandardOutput.ReadToEndAsync() : Task.FromResult(string.Empty);
        Task<string> errors = captureOutput ? process.StandardError.ReadToEndAsync() : Task.FromResult(string.Empty);
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(StartupTimeoutSeconds));
        await process.WaitForExitAsync(timeout.Token);
        if (process.ExitCode != 0) throw new InvalidOperationException(
            $"{executable} failed: {await output}\n{await errors}");
        await Task.WhenAll(output, errors);
    }

    public async ValueTask DisposeAsync()
    {
        if (File.Exists(Path.Combine(DataDirectory, "postmaster.pid")))
            await RunAsync("pg_ctl.exe", "-D", DataDirectory, "-m", "fast", "-w", "stop");
    }
}
