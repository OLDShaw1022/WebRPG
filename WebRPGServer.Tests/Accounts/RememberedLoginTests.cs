using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Npgsql;
using NUnit.Framework;
using WebRPGServer;

namespace WebRPGServer.Tests;

// 驗證長效登入 Cookie、憑證輪替、撤銷與跨來源防護。
[TestFixture]
[NonParallelizable]
[Category("Integration")]
public sealed class RememberedLoginTests
{
    private LocalAccountDatabase _database = null!;

    [OneTimeSetUp]
    public async Task SetUpAsync()
    {
        string? binaries = Environment.GetEnvironmentVariable("WEBRPG_TEST_POSTGRES_BIN");
        if (string.IsNullOrEmpty(binaries) || !File.Exists(Path.Combine(binaries, "initdb.exe")))
            Assert.Ignore("Set WEBRPG_TEST_POSTGRES_BIN to isolated local PostgreSQL binaries.");
        _database = await LocalAccountDatabase.StartAsync(binaries!);
    }

    [OneTimeTearDown]
    public async Task TearDownAsync()
    {
        if (_database is not null) await _database.DisposeAsync();
    }

    [Test]
    public async Task RememberedLoginRotatesCookieAndLogoutRevokesBothTokens()
    {
        await using LocalAccountServer server = await LocalAccountServer.StartAsync(_database);
        using HttpClient client = new(new HttpClientHandler { UseCookies = false }) { BaseAddress = server.Address };
        client.DefaultRequestHeaders.Add("X-WebRPG-Request", "1");
        string email = $"remember-{Guid.NewGuid():N}@example.invalid";
        using HttpResponseMessage registration = await client.PostAsJsonAsync("api/accounts/register", new AccountRegistration(email, "long-enough-password", "玩家"));
        registration.EnsureSuccessStatusCode();
        using HttpResponseMessage login = await client.PostAsJsonAsync("api/accounts/login", new { email, password = "long-enough-password", rememberMe = true });
        login.EnsureSuccessStatusCode();
        Assert.That(login.Headers.TryGetValues("Set-Cookie", out var values), Is.True, "Remembered login must set an HttpOnly cookie");
        string header = values!.Single();
        Assert.That(header.ToLowerInvariant(), Does.Contain("httponly").And.Contain("secure").And.Contain("samesite=strict").And.Contain("path=/api/accounts").And.Contain("expires="));
        Assert.That(header.ToLowerInvariant(), Does.Not.Contain("domain="));
        string cookie = header.Split(';')[0];
        JsonObject first = (await login.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.That(first.ContainsKey("refreshToken"), Is.False);
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        using HttpResponseMessage refreshed = await client.PostAsJsonAsync("api/accounts/refresh", new { });
        refreshed.EnsureSuccessStatusCode();
        JsonObject second = (await refreshed.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.That(second["accountId"]!.GetValue<string>(), Is.EqualTo(first["accountId"]!.GetValue<string>()));
        Assert.That(second["accessToken"]!.GetValue<string>(), Is.Not.EqualTo(first["accessToken"]!.GetValue<string>()));
        string rotated = refreshed.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
        Assert.That(rotated, Is.Not.EqualTo(cookie));
        await using NpgsqlDataSource source = NpgsqlDataSource.Create(_database.ConnectionString);
        PostgresAccountStore store = new(source);
        Assert.That(await store.AuthenticateAsync(first["accessToken"]!.GetValue<string>(), CancellationToken.None), Is.Null);
        using HttpResponseMessage replay = await client.PostAsJsonAsync("api/accounts/refresh", new { });
        Assert.That(replay.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", rotated);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", second["accessToken"]!.GetValue<string>());
        using HttpResponseMessage logout = await client.PostAsJsonAsync("api/accounts/logout", new { });
        Assert.That(logout.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That(logout.Headers.GetValues("Set-Cookie").Single(), Does.Contain("expires=Thu, 01 Jan 1970"));
        using HttpResponseMessage afterLogout = await client.PostAsJsonAsync("api/accounts/refresh", new { });
        Assert.That(afterLogout.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(await store.AuthenticateAsync(second["accessToken"]!.GetValue<string>(), CancellationToken.None), Is.Null);
    }

    [Test]
    public async Task RememberedLoginSurvivesRestartAndOnlyHashesAreStored()
    {
        string cookie;
        string accountId;
        await using (LocalAccountServer server = await LocalAccountServer.StartAsync(_database))
        {
            using HttpClient client = new(new HttpClientHandler { UseCookies = false }) { BaseAddress = server.Address };
            string email = $"persist-{Guid.NewGuid():N}@example.invalid";
            using HttpResponseMessage registration = await client.PostAsJsonAsync("api/accounts/register", new AccountRegistration(email, "long-enough-password", "玩家"));
            registration.EnsureSuccessStatusCode();
            using HttpResponseMessage login = await client.PostAsJsonAsync("api/accounts/login", new { email, password = "long-enough-password", rememberMe = true });
            login.EnsureSuccessStatusCode();
            Assert.That(login.Headers.TryGetValues("Set-Cookie", out var values), Is.True);
            cookie = values!.Single().Split(';')[0];
            JsonObject session = (await login.Content.ReadFromJsonAsync<JsonObject>())!;
            accountId = session["accountId"]!.GetValue<string>();
            await using NpgsqlDataSource source = NpgsqlDataSource.Create(_database.ConnectionString);
            await using NpgsqlCommand query = source.CreateCommand("SELECT token_hash FROM game_account_remembered_sessions WHERE account_id = $1;");
            query.Parameters.AddWithValue(Guid.Parse(accountId));
            string stored = (string)(await query.ExecuteScalarAsync())!;
            Assert.That(stored, Is.Not.EqualTo(cookie.Split('=')[1]));
        }
        await _database.RestartAsync();
        await using LocalAccountServer restarted = await LocalAccountServer.StartAsync(_database);
        using HttpClient restoredClient = new(new HttpClientHandler { UseCookies = false }) { BaseAddress = restarted.Address };
        restoredClient.DefaultRequestHeaders.Add("X-WebRPG-Request", "1");
        restoredClient.DefaultRequestHeaders.Add("Cookie", cookie);
        using HttpResponseMessage response = await restoredClient.PostAsJsonAsync("api/accounts/refresh", new { });
        response.EnsureSuccessStatusCode();
        Assert.That((await response.Content.ReadFromJsonAsync<JsonObject>())!["accountId"]!.GetValue<string>(), Is.EqualTo(accountId));
    }

    [Test]
    public async Task CrossOriginRefreshAndMissingRequestHeaderAreRejected()
    {
        await using LocalAccountServer server = await LocalAccountServer.StartAsync(_database);
        using HttpClient client = new() { BaseAddress = server.Address };
        using HttpResponseMessage missingHeader = await client.PostAsJsonAsync("api/accounts/refresh", new { });
        Assert.That(missingHeader.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        client.DefaultRequestHeaders.Add("X-WebRPG-Request", "1");
        client.DefaultRequestHeaders.Add("Origin", "https://evil.example");
        using HttpResponseMessage foreign = await client.PostAsJsonAsync("api/accounts/refresh", new { });
        Assert.That(foreign.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task ConcurrentRefreshOnlySucceedsOnceAndBearerLogoutRevokesFamily()
    {
        await using NpgsqlDataSource source = NpgsqlDataSource.Create(_database.ConnectionString);
        PostgresAccountStore store = new(source);
        await store.InitializeAsync(CancellationToken.None);
        string email = $"parallel-{Guid.NewGuid():N}@example.invalid";
        await store.RegisterAsync(new(email, "long-enough-password", "玩家"), CancellationToken.None);
        AccountSession session = (await store.LoginAsync(new(email, "long-enough-password"), CancellationToken.None))!;
        RememberedAccountSession remembered = await store.RememberAsync(session, CancellationToken.None);
        RememberedAccountSession?[] results = await Task.WhenAll(
            store.RefreshAsync(remembered.RefreshToken, CancellationToken.None),
            store.RefreshAsync(remembered.RefreshToken, CancellationToken.None));
        Assert.That(results.Count(value => value is not null), Is.EqualTo(1));
        RememberedAccountSession rotated = results.Single(value => value is not null)!;
        Assert.That(rotated.RefreshExpiresAt, Is.EqualTo(remembered.RefreshExpiresAt).Within(TimeSpan.FromMilliseconds(1)));
        await store.LogoutAsync(rotated.Session.AccessToken, CancellationToken.None);
        Assert.That(await store.RefreshAsync(rotated.RefreshToken, CancellationToken.None), Is.Null);
        Assert.That(await store.AuthenticateAsync(rotated.Session.AccessToken, CancellationToken.None), Is.Null);
    }

    [Test]
    public async Task ExpiredOrMalformedRefreshTokensCannotRestoreSession()
    {
        await using NpgsqlDataSource source = NpgsqlDataSource.Create(_database.ConnectionString);
        PostgresAccountStore store = new(source);
        await store.InitializeAsync(CancellationToken.None);
        string email = $"expired-{Guid.NewGuid():N}@example.invalid";
        await store.RegisterAsync(new(email, "long-enough-password", "玩家"), CancellationToken.None);
        AccountSession session = (await store.LoginAsync(new(email, "long-enough-password"), CancellationToken.None))!;
        RememberedAccountSession remembered = await store.RememberAsync(session, CancellationToken.None);
        await using NpgsqlCommand expire = source.CreateCommand("UPDATE game_account_remembered_sessions SET expires_at = CURRENT_TIMESTAMP WHERE account_id = $1;");
        expire.Parameters.AddWithValue(Guid.Parse(session.AccountId));
        await expire.ExecuteNonQueryAsync();
        Assert.That(await store.RefreshAsync(remembered.RefreshToken, CancellationToken.None), Is.Null);
        Assert.That(await store.RefreshAsync(null, CancellationToken.None), Is.Null);
        Assert.That(await store.RefreshAsync(new string('x', 129), CancellationToken.None), Is.Null);
        Assert.That(await store.RefreshAsync("invalid-token", CancellationToken.None), Is.Null);
    }

    [Test]
    public async Task ChoosingOrdinaryLoginRevokesPreviousRememberedAccount()
    {
        await using LocalAccountServer server = await LocalAccountServer.StartAsync(_database);
        using HttpClient client = new(new HttpClientHandler { UseCookies = false }) { BaseAddress = server.Address };
        client.DefaultRequestHeaders.Add("X-WebRPG-Request", "1");
        string email = $"replace-{Guid.NewGuid():N}@example.invalid";
        using HttpResponseMessage registration = await client.PostAsJsonAsync("api/accounts/register", new AccountRegistration(email, "long-enough-password", "玩家"));
        registration.EnsureSuccessStatusCode();
        using HttpResponseMessage remembered = await client.PostAsJsonAsync("api/accounts/login", new { email, password = "long-enough-password", rememberMe = true });
        remembered.EnsureSuccessStatusCode();
        string cookie = remembered.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        using HttpResponseMessage ordinary = await client.PostAsJsonAsync("api/accounts/login", new AccountLogin(email, "long-enough-password"));
        ordinary.EnsureSuccessStatusCode();
        Assert.That(ordinary.Headers.GetValues("Set-Cookie").Single(), Does.Contain("1970"));
        using HttpResponseMessage oldCookie = await client.PostAsJsonAsync("api/accounts/refresh", new { });
        Assert.That(oldCookie.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        await using NpgsqlDataSource source = NpgsqlDataSource.Create(_database.ConnectionString);
        PostgresAccountStore store = new(source);
        JsonObject previousSession = (await remembered.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.That(await store.AuthenticateAsync(previousSession["accessToken"]!.GetValue<string>(), CancellationToken.None), Is.Null);
    }

    [TestCase("https://oldshaw-webrpg.com", true)]
    [TestCase("https://evil.example", false)]
    [TestCase("https://oldshaw-web-rpg.web.app", false)]
    public async Task CorsPreflightAllowsCredentialsOnlyForConfiguredGameOrigin(string origin, bool allowed)
    {
        await using LocalAccountServer server = await LocalAccountServer.StartAsync(_database);
        using HttpClient client = new() { BaseAddress = server.Address };
        using HttpRequestMessage request = new(HttpMethod.Options, "api/accounts/refresh");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type,x-webrpg-request,authorization");
        using HttpResponseMessage response = await client.SendAsync(request);
        bool credentials = response.Headers.TryGetValues("Access-Control-Allow-Credentials", out var values) && values.Single() == "true";
        Assert.That(credentials, Is.EqualTo(allowed));
        string? allowedOrigin = response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins) ? origins.Single() : null;
        Assert.That(allowedOrigin, Is.EqualTo(allowed ? origin : null));
    }

    [Test]
    public async Task LoginWithoutRememberMeDoesNotIssuePersistentCookie()
    {
        await using LocalAccountServer server = await LocalAccountServer.StartAsync(_database);
        using HttpClient client = new() { BaseAddress = server.Address };
        string email = $"ordinary-{Guid.NewGuid():N}@example.invalid";
        using HttpResponseMessage registration = await client.PostAsJsonAsync("api/accounts/register", new AccountRegistration(email, "long-enough-password", "玩家"));
        registration.EnsureSuccessStatusCode();
        using HttpResponseMessage login = await client.PostAsJsonAsync("api/accounts/login", new AccountLogin(email, "long-enough-password"));
        login.EnsureSuccessStatusCode();
        Assert.That(login.Headers.TryGetValues("Set-Cookie", out var values) ? values.Any(value => value.Contains("expires=") && !value.Contains("1970")) : false, Is.False);
    }
}
