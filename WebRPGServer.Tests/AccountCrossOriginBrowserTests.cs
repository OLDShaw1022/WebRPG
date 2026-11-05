using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using NUnit.Framework;
using WebRPGServer;

namespace WebRPGServer.Tests;

// 以同站但不同來源的本機 HTTPS 網域驗證實際 WebGL 插件與 Cookie 恢復登入。
[TestFixture]
[NonParallelizable]
[Category("Integration")]
public sealed class AccountCrossOriginBrowserTests
{
    private const int CertificateKeyBits = 2048;
    private const int CertificateLifetimeDays = 1;
    private const int BrowserTimeoutSeconds = 45;
    private const string GameHost = "webrpg.test";
    private const string ApiHost = "api.webrpg.test";
    private const string PluginRelativePath = "../../../../WebRPG/Assets/Plugins/WebRPGAccountCredentials.jslib";
    private const string TestPage = """
        <!doctype html><html><body><output id="result">waiting</output><script>
        var LibraryManager = {library:{}};
        function mergeInto(target, values) { Object.assign(target, values); }
        function UTF8ToString(value) { return value; }
        var Module = {
          fetchWithProgress: function(resource, options) { return fetch(resource, options); },
          cachedFetch: function(resource, options) { return fetch(resource, options); }
        };
        PLUGIN_SOURCE
        var WebRPGAccountCredentials = LibraryManager.library.$WebRPGAccountCredentials;
        var api = API_ORIGIN + '/api/accounts/';
        for (var endpoint of ['login','refresh','logout']) LibraryManager.library.WebRPG_RegisterAccountRequest(api + endpoint);
        (async () => {
          const result = document.getElementById('result');
          const headers = {'Content-Type':'application/json','X-WebRPG-Request':'1'};
          try {
            if (new URL(location.href).searchParams.get('stage') === 'login') {
              const response = await Module.fetchWithProgress(api+'login', {
                method:'POST', headers, body:JSON.stringify({email:EMAIL,password:'long-enough-password',rememberMe:true})
              });
              if (!response.ok) throw new Error('login status '+response.status);
              result.textContent = 'cross-origin-cookie-saved';
            } else {
              const omitted = await fetch(api+'refresh', {method:'POST',headers,credentials:'omit'});
              if (omitted.status !== 401) throw new Error('control request unexpectedly sent cookies');
              const response = await Module.cachedFetch(api+'refresh', {method:'POST',headers});
              if (!response.ok) throw new Error('restore status '+response.status);
              const session = await response.json();
              if (!session.accessToken) throw new Error('missing session');
              const logout = await Module.fetchWithProgress(api+'logout', {method:'POST',headers:{...headers,Authorization:'Bearer '+session.accessToken}});
              if (logout.status !== 204) throw new Error('logout status '+logout.status);
              const afterLogout = await Module.cachedFetch(api+'refresh', {method:'POST',headers});
              if (afterLogout.status !== 401) throw new Error('cookie remained valid after logout');
              result.textContent = 'cross-origin-restored-after-restart-and-revoked';
            }
          } catch(error) { result.textContent = 'FAILED: '+error.message; }
        })();
        </script></body></html>
        """;

    [Test]
    public async Task SameSiteSubdomainCookieWorksWithTheActualWebGlPluginAcrossBrowserRestarts()
    {
        string? browser = Environment.GetEnvironmentVariable("WEBRPG_TEST_BROWSER_BIN");
        string? binaries = Environment.GetEnvironmentVariable("WEBRPG_TEST_POSTGRES_BIN");
        if (string.IsNullOrEmpty(browser) || !File.Exists(browser) || string.IsNullOrEmpty(binaries))
            Assert.Ignore("Set browser and PostgreSQL test binaries for isolated HTTPS browser validation.");
        await using LocalAccountDatabase database = await LocalAccountDatabase.StartAsync(binaries!);
        await using NpgsqlDataSource source = NpgsqlDataSource.Create(database.ConnectionString);
        PostgresAccountStore store = new(source);
        await store.InitializeAsync(CancellationToken.None);
        string email = $"subdomain-{Guid.NewGuid():N}@example.invalid";
        await store.RegisterAsync(new(email, "long-enough-password", "玩家"), CancellationToken.None);
        using RSA key = RSA.Create(CertificateKeyBits);
        CertificateRequest request = new("CN=" + GameHost, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        SubjectAlternativeNameBuilder names = new();
        names.AddDnsName(GameHost);
        names.AddDnsName(ApiHost);
        request.CertificateExtensions.Add(names.Build());
        using X509Certificate2 generatedCertificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-CertificateLifetimeDays),
            DateTimeOffset.UtcNow.AddDays(CertificateLifetimeDays));
        // Windows Schannel 不支援純記憶體私鑰；暫時載入使用者金鑰容器，不安裝憑證或信任根。
        byte[] certificateBytes = generatedCertificate.Export(X509ContentType.Pfx);
        using X509Certificate2 certificate = X509CertificateLoader.LoadPkcs12(certificateBytes, null, X509KeyStorageFlags.UserKeySet);
        CryptographicOperations.ZeroMemory(certificateBytes);
        TcpListener reservation = new(IPAddress.Loopback, 0);
        reservation.Start();
        int port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();
        string gameOrigin = $"https://{GameHost}:{port}";
        string apiOrigin = $"https://{ApiHost}:{port}";
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, port, listener => listener.UseHttps(certificate)));
        builder.Configuration["Accounts:AllowedOrigins:0"] = gameOrigin;
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(source);
        builder.Services.AddSingleton(store);
        AccountEndpoints.AddServices(builder.Services);
        AccountCorsHelper.AddServices(builder.Services, builder.Configuration);
        await using WebApplication application = builder.Build();
        application.UseRouting();
        application.UseCors();
        application.UseRateLimiter();
        AccountEndpoints.Map(application);
        string plugin = await File.ReadAllTextAsync(Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, PluginRelativePath)));
        string html = TestPage.Replace("PLUGIN_SOURCE", plugin)
            .Replace("API_ORIGIN", System.Text.Json.JsonSerializer.Serialize(apiOrigin))
            .Replace("EMAIL", System.Text.Json.JsonSerializer.Serialize(email));
        application.MapGet("/browser-test", () => Microsoft.AspNetCore.Http.Results.Content(html, "text/html"));
        await application.StartAsync();
        string profile = Path.Combine(Path.GetTempPath(), "WebRPGSubdomainBrowser-" + Guid.NewGuid().ToString("N"));
        string[] arguments = { "--ignore-certificate-errors", "--no-proxy-server",
            $"--host-resolver-rules=MAP {GameHost} 127.0.0.1, MAP {ApiHost} 127.0.0.1" };
        string login = await RememberedBrowserHelper.RunAsync(browser!, profile,
            new Uri(gameOrigin + "/browser-test?stage=login"), BrowserTimeoutSeconds, arguments);
        Assert.That(login, Does.Contain("<output id=\"result\">cross-origin-cookie-saved</output>"));
        string restore = await RememberedBrowserHelper.RunAsync(browser!, profile,
            new Uri(gameOrigin + "/browser-test?stage=restore"), BrowserTimeoutSeconds, arguments);
        Assert.That(restore, Does.Contain("<output id=\"result\">cross-origin-restored-after-restart-and-revoked</output>"));
        await application.StopAsync();
    }
}
