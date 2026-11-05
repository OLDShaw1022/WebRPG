using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using Npgsql;
using NUnit.Framework;
using WebRPGServer;

namespace WebRPGServer.Tests;

// 使用獨立真實 PostgreSQL 驗證帳號儲存、正式 API、認證與重啟保存。
[TestFixture]
[NonParallelizable]
[Category("Integration")]
public sealed class AccountPostgresTests
{
    private const int WebSocketBufferBytes = 8192;
    private const int WebSocketTimeoutSeconds = 10;
    private LocalAccountDatabase _database = null!;

    [OneTimeSetUp]
    public async Task SetUpAsync()
    {
        string? binaries = Environment.GetEnvironmentVariable("WEBRPG_TEST_POSTGRES_BIN");
        if (string.IsNullOrEmpty(binaries) || !File.Exists(Path.Combine(binaries, "initdb.exe")))
            Assert.Ignore("Set WEBRPG_TEST_POSTGRES_BIN to local PostgreSQL binaries to run isolated database tests.");
        _database = await LocalAccountDatabase.StartAsync(binaries!);
        TestContext.Progress.WriteLine("Isolated PostgreSQL test data: " + _database.DataDirectory);
    }

    [OneTimeTearDown]
    public async Task TearDownAsync()
    {
        if (_database is not null) await _database.DisposeAsync();
    }

    [Test]
    public async Task StoreEnforcesEmailUniquenessAndStoresOnlyHashes()
    {
        await using NpgsqlDataSource source = NpgsqlDataSource.Create(_database.ConnectionString);
        PostgresAccountStore store = new(source);
        await store.InitializeAsync(CancellationToken.None);
        string email = $"store-{Guid.NewGuid():N}@example.invalid";
        await store.RegisterAsync(new(email, "long-enough-password", "同名玩家"), CancellationToken.None);
        Assert.ThrowsAsync<ArgumentException>(() => store.RegisterAsync(new(email.ToUpperInvariant(), "long-enough-password", "另一名稱"), CancellationToken.None));
        Assert.That(await store.LoginAsync(new(email, "incorrect-password"), CancellationToken.None), Is.Null);
        AccountSession first = (await store.LoginAsync(new(email, "long-enough-password"), CancellationToken.None))!;
        AccountSession second = (await store.LoginAsync(new(email.ToUpperInvariant(), "long-enough-password"), CancellationToken.None))!;
        Assert.That(first.AccountId, Is.EqualTo(second.AccountId));
        Assert.That(first.AccessToken, Is.Not.EqualTo(second.AccessToken));
        await using NpgsqlCommand query = source.CreateCommand("""
            SELECT a.password_hash, s.token_hash FROM game_accounts a
            JOIN game_account_sessions s ON a.account_id = s.account_id WHERE a.account_id = $1;
            """);
        query.Parameters.AddWithValue(Guid.Parse(first.AccountId));
        await using (NpgsqlDataReader reader = await query.ExecuteReaderAsync())
        {
            Assert.That(await reader.ReadAsync(), Is.True);
            Assert.That(reader.GetString(0), Is.Not.EqualTo("long-enough-password"));
            Assert.That(AccountPasswordHelper.Verify(reader.GetString(0), "long-enough-password"), Is.True);
            Assert.That(reader.GetString(1), Is.Not.EqualTo(first.AccessToken).And.Not.EqualTo(second.AccessToken));
        }
        await store.LogoutAsync(first.AccessToken, CancellationToken.None);
        Assert.That(await store.AuthenticateAsync(first.AccessToken, CancellationToken.None), Is.Null);
        Assert.That(await store.AuthenticateAsync(second.AccessToken, CancellationToken.None), Is.Not.Null);
        await using NpgsqlCommand expire = source.CreateCommand("UPDATE game_account_sessions SET expires_at = CURRENT_TIMESTAMP WHERE account_id = $1;");
        expire.Parameters.AddWithValue(Guid.Parse(first.AccountId));
        await expire.ExecuteNonQueryAsync();
        Assert.That(await store.AuthenticateAsync(second.AccessToken, CancellationToken.None), Is.Null);
    }

    [Test]
    public async Task RealApiSupportsRegistrationLoginLobbyLogoutAndDatabaseRestart()
    {
        string email = $"api-{Guid.NewGuid():N}@example.invalid";
        string otherEmail = $"api-{Guid.NewGuid():N}@example.invalid";
        string accountId;
        await using (LocalAccountServer server = await LocalAccountServer.StartAsync(_database))
        {
            using HttpClient client = new() { BaseAddress = server.Address };
            using HttpResponseMessage registration = await client.PostAsJsonAsync("api/accounts/register", new AccountRegistration(email, "long-enough-password", "同名玩家"));
            Assert.That(registration.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            using HttpResponseMessage duplicate = await client.PostAsJsonAsync("api/accounts/register", new AccountRegistration(email.ToUpperInvariant(), "long-enough-password", "同名玩家"));
            Assert.That(duplicate.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            using HttpResponseMessage other = await client.PostAsJsonAsync("api/accounts/register", new AccountRegistration(otherEmail, "long-enough-password", "同名玩家"));
            Assert.That(other.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            using HttpResponseMessage wrong = await client.PostAsJsonAsync("api/accounts/login", new AccountLogin(email, "incorrect-password"));
            Assert.That(wrong.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            using HttpResponseMessage login = await client.PostAsJsonAsync("api/accounts/login", new AccountLogin(email, "long-enough-password"));
            login.EnsureSuccessStatusCode();
            JsonObject session = (await login.Content.ReadFromJsonAsync<JsonObject>())!;
            accountId = session["accountId"]!.GetValue<string>();
            string accessToken = session["accessToken"]!.GetValue<string>();
            using HttpResponseMessage otherLogin = await client.PostAsJsonAsync("api/accounts/login", new AccountLogin(otherEmail, "long-enough-password"));
            JsonObject otherSession = (await otherLogin.Content.ReadFromJsonAsync<JsonObject>())!;
            Assert.That(otherSession["accountId"]!.GetValue<string>(), Is.Not.EqualTo(accountId));
            Assert.That(otherSession["username"]!.GetValue<string>(), Is.EqualTo("同名玩家"));
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(WebSocketTimeoutSeconds));
            using ClientWebSocket socket = new();
            UriBuilder websocket = new(server.Address) { Scheme = "ws", Path = "/ws", Query = "channel=lobby" };
            await socket.ConnectAsync(websocket.Uri, timeout.Token);
            string hello = new JsonObject { ["Type"] = "Hello", ["Username"] = "Spoofed", ["AccessToken"] = accessToken }.ToJsonString();
            await socket.SendAsync(Encoding.UTF8.GetBytes(hello), WebSocketMessageType.Text, true, timeout.Token);
            byte[] buffer = new byte[WebSocketBufferBytes];
            WebSocketReceiveResult welcome = await socket.ReceiveAsync(buffer, timeout.Token);
            Assert.That(JsonNode.Parse(buffer.AsSpan(0, welcome.Count))!["PlayerId"]!.GetValue<string>(), Is.EqualTo(accountId));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using HttpResponseMessage logout = await client.PostAsync("api/accounts/logout", null);
            Assert.That(logout.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            await socket.SendAsync(Encoding.UTF8.GetBytes("{\"Type\":\"ListRooms\"}"), WebSocketMessageType.Text, true, timeout.Token);
            WebSocketReceiveResult rejected = await socket.ReceiveAsync(buffer, timeout.Token);
            Assert.That(rejected.CloseStatus, Is.EqualTo(WebSocketCloseStatus.PolicyViolation));
        }
        await _database.RestartAsync();
        await using LocalAccountServer restarted = await LocalAccountServer.StartAsync(_database);
        using HttpClient restartedClient = new() { BaseAddress = restarted.Address };
        using HttpResponseMessage relogin = await restartedClient.PostAsJsonAsync("api/accounts/login", new AccountLogin(email, "long-enough-password"));
        relogin.EnsureSuccessStatusCode();
        JsonObject persisted = (await relogin.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.That(persisted["accountId"]!.GetValue<string>(), Is.EqualTo(accountId));
    }
}
