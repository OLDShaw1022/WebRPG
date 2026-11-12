using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace WebRPGServer.Tests;

// 管理遠端測試專用帳號的登入與登出，不輸出帳密或憑證。
internal sealed class RemoteAccountSession : IAsyncDisposable
{
    private const int LogoutTimeoutSeconds = 10;
    private readonly HttpClient _client;
    public string AccessToken { get; }

    private RemoteAccountSession(HttpClient client, string accessToken)
    {
        _client = client;
        AccessToken = accessToken;
    }

    public static async Task<RemoteAccountSession> LoginAsync(Uri address, string account, CancellationToken token)
    {
        string email = Environment.GetEnvironmentVariable($"WEBRPG_TEST_{account}_EMAIL") ?? string.Empty;
        string password = Environment.GetEnvironmentVariable($"WEBRPG_TEST_{account}_PASSWORD") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
            throw new InvalidOperationException(
                $"Configure WEBRPG_TEST_{account}_EMAIL and WEBRPG_TEST_{account}_PASSWORD " +
                "for a dedicated test account.");
        if (address.Scheme != "wss")
            throw new InvalidOperationException("Remote account tests require WSS and HTTPS.");
        UriBuilder api = new(
            address) { Scheme = "https", Path = "/api/accounts/", Query = string.Empty, Fragment = string.Empty };
        HttpClient client = new(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = api.Uri };
        try
        {
            using HttpResponseMessage response = await client.PostAsJsonAsync("login", new { email, password }, token);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Remote test account {account} login failed: HTTP {(int)response.StatusCode}. " +
                    "Check account API proxy, database and test credentials.");
            JsonObject? session = await response.Content.ReadFromJsonAsync<JsonObject>(token);
            string? accessToken = session?["accessToken"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(accessToken))
                throw new InvalidOperationException("Remote login returned no access token.");
            return new RemoteAccountSession(client, accessToken);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        using (_client)
        using (CancellationTokenSource timeout = new(TimeSpan.FromSeconds(LogoutTimeoutSeconds)))
        using (HttpRequestMessage request = new(HttpMethod.Post, "logout"))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
            using HttpResponseMessage response = await _client.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Remote test logout failed: HTTP {(int)response.StatusCode}.");
        }
    }
}
