using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using WebRPGServer;

namespace WebRPGServer.Tests;

// 驗證資料庫未設定時帳號 API 不會退回不安全的臨時帳號。
public sealed class AccountEndpointTests
{
    [Test]
    public async Task UnconfiguredDatabaseReturnsUnavailableAndNeverCachesCredentials()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        AccountEndpoints.AddServices(builder.Services);
        await using WebApplication application = builder.Build();
        application.UseRouting();
        application.UseRateLimiter();
        AccountEndpoints.Map(application);
        await application.StartAsync();
        string address = application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using HttpClient client = new() { BaseAddress = new Uri(address) };
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/accounts/login", new AccountLogin("player@example.com", "long-enough-password"));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        Assert.That(response.Headers.CacheControl?.NoStore, Is.True);
        await application.StopAsync();
    }
}
