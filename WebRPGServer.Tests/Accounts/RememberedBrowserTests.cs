using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using NUnit.Framework;
using WebRPGServer;

namespace WebRPGServer.Tests;

// 使用獨立無頭瀏覽器設定檔驗證 HttpOnly Cookie 跨程序保存與登出撤銷。
[TestFixture]
[NonParallelizable]
[Category("Integration")]
public sealed class RememberedBrowserTests
{
    private const int BrowserTimeoutSeconds = 45;
    private const string BrowserPage = """
        <!doctype html><html><body><output id="result">waiting</output><script>
        (async () => {
          const result = document.getElementById('result');
          const headers = {'Content-Type':'application/json','X-WebRPG-Request':'1'};
          try {
            if (new URL(location.href).searchParams.get('stage') === 'login') {
              const response = await fetch('/api/accounts/login', {
                method:'POST', headers, body:JSON.stringify({email:EMAIL,password:'long-enough-password',rememberMe:true})
              });
              if (!response.ok) throw new Error('login status '+response.status);
              if (document.cookie.includes('__Secure-WebRPGRemember')) throw new Error('Cookie exposed to JavaScript');
              result.textContent = 'remembered-and-http-only';
            } else {
              const response = await fetch('/api/accounts/refresh', {method:'POST',headers});
              if (!response.ok) throw new Error('refresh status '+response.status);
              const session = await response.json();
              if (!session.accessToken) throw new Error('missing session');
              if (document.cookie.includes('__Secure-WebRPGRemember')) throw new Error('Cookie exposed to JavaScript');
              const logout = await fetch('/api/accounts/logout', {method:'POST',headers:{...headers,Authorization:'Bearer '+session.accessToken}});
              if (logout.status !== 204) throw new Error('logout status '+logout.status);
              const afterLogout = await fetch('/api/accounts/refresh', {method:'POST',headers});
              if (afterLogout.status !== 401) throw new Error('logout did not revoke cookie');
              result.textContent = 'restored-after-restart-and-revoked';
            }
          } catch (error) { result.textContent = 'FAILED: '+error.message; }
        })();
        </script></body></html>
        """;

    [Test]
    public async Task CookiePersistsAcrossBrowserProcessesAndLogoutPreventsRestoration()
    {
        string? browser = Environment.GetEnvironmentVariable("WEBRPG_TEST_BROWSER_BIN");
        string? binaries = Environment.GetEnvironmentVariable("WEBRPG_TEST_POSTGRES_BIN");
        if (string.IsNullOrEmpty(browser) || !File.Exists(browser) || string.IsNullOrEmpty(binaries))
            Assert.Ignore("Set WEBRPG_TEST_BROWSER_BIN and WEBRPG_TEST_POSTGRES_BIN for isolated browser validation.");
        await using LocalAccountDatabase database = await LocalAccountDatabase.StartAsync(binaries!);
        await using NpgsqlDataSource source = NpgsqlDataSource.Create(database.ConnectionString);
        PostgresAccountStore store = new(source);
        await store.InitializeAsync(CancellationToken.None);
        string email = $"browser-{Guid.NewGuid():N}@example.invalid";
        await store.RegisterAsync(new(email, "long-enough-password", "玩家"), CancellationToken.None);
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(source);
        builder.Services.AddSingleton(store);
        AccountEndpoints.AddServices(builder.Services);
        await using WebApplication application = builder.Build();
        application.UseRouting();
        application.UseRateLimiter();
        AccountEndpoints.Map(application);
        application.MapGet("/api/accounts/browser-test", () => Microsoft.AspNetCore.Http.Results.Content(
            BrowserPage.Replace("EMAIL", System.Text.Json.JsonSerializer.Serialize(email)), "text/html"));
        await application.StartAsync();
        string address = application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        UriBuilder page = new(address) { Host = "localhost", Path = "/api/accounts/browser-test" };
        string profile = Path.Combine(Path.GetTempPath(), "WebRPGRememberBrowser-" + Guid.NewGuid().ToString("N"));
        page.Query = "stage=login";
        string first = await RememberedBrowserHelper.RunAsync(browser!, profile, page.Uri, BrowserTimeoutSeconds);
        Assert.That(first, Does.Contain("<output id=\"result\">remembered-and-http-only</output>"));
        page.Query = "stage=restore";
        string second = await RememberedBrowserHelper.RunAsync(browser!, profile, page.Uri, BrowserTimeoutSeconds);
        Assert.That(second, Does.Contain("<output id=\"result\">restored-after-restart-and-revoked</output>"));
        await application.StopAsync();
    }
}
