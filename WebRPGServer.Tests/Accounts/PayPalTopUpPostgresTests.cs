using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Npgsql;
using NUnit.Framework;
using WebRPGServer;

namespace WebRPGServer.Tests;

// 用隔離的 PostgreSQL 與 PayPal HTTP 替身驗證付款及錢包的完整交易。
[TestFixture]
[NonParallelizable]
[Category("Integration")]
public sealed class PayPalTopUpPostgresTests
{
    private static readonly CancellationToken TestCancellation = CancellationToken.None;
    private LocalAccountDatabase _database = null!;

    [OneTimeSetUp]
    public async Task StartDatabaseAsync()
    {
        string? binaries = Environment.GetEnvironmentVariable("WEBRPG_TEST_POSTGRES_BIN");
        if (string.IsNullOrEmpty(binaries) || !File.Exists(Path.Combine(binaries, "initdb.exe")))
            Assert.Ignore("Set WEBRPG_TEST_POSTGRES_BIN to local PostgreSQL binaries.");
        _database = await LocalAccountDatabase.StartAsync(binaries!);
    }

    [OneTimeTearDown]
    public async Task StopDatabaseAsync()
    {
        if (_database is not null) await _database.DisposeAsync();
    }

    [Test]
    public async Task VerifiedPaymentCreditsOnceAndRefundReversesMixedExchangeWithDebt()
    {
        await using NpgsqlDataSource source = NpgsqlDataSource.Create(_database.ConnectionString);
        PostgresAccountStore accounts = new(source);
        PostgresShopStore wallet = new(source);
        PostgresTopUpStore store = new(source);
        await accounts.InitializeAsync(TestCancellation);
        await wallet.InitializeAsync(TestCancellation);
        string email = $"paypal-{Guid.NewGuid():N}@example.invalid";
        await accounts.RegisterAsync(new(email, "long-enough-password", "測試玩家"), TestCancellation);
        Guid accountId = Guid.Parse((await accounts.LoginAsync(new(email, "long-enough-password"), TestCancellation))!.AccountId);

        ShopTopUpOffer offer = new("starter", "USD", 5m, 100);
        ShopTopUpRequest firstRequest = new(Guid.NewGuid(), offer);
        PayPalSettings settings = new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Shop:PayPal:CallbackUrl"] = "https://example.invalid/api/payments/paypal/return",
            ["Shop:PayPal:WebhookId"] = "test-webhook"
        }).Build());
        using PayPalTestHandler remote = new();
        using HttpClient client = new(remote) { BaseAddress = new Uri("https://api-m.sandbox.paypal.com/") };
        PayPalTopUpService service = new(new PayPalCheckout(client, settings), settings, store);
        ShopTopUpOrder first = (await service.CreateAsync(accountId, firstRequest, offer, TestCancellation))!;
        Assert.That(await service.CreateAsync(accountId, firstRequest, offer with { Amount = 6m }, TestCancellation), Is.EqualTo(first));
        Assert.That(remote.CreateCount, Is.EqualTo(1));
        Assert.That(await store.ListAsync(Guid.NewGuid(), true, TestCancellation), Is.Empty);
        Assert.That(await store.ListAsync(accountId, false, TestCancellation), Is.Empty);

        remote.CaptureStatus = "PENDING";
        await service.ReconcileAsync(first, true, false, null, TestCancellation);
        Assert.That(await wallet.GetBalanceAsync(accountId, TestCancellation), Is.EqualTo(new ShopBalance(0, 0)));
        remote.CaptureStatus = "COMPLETED";
        remote.VerifySignature = false;
        Assert.That(await NotifyAsync(service, first, "PAYMENT.CAPTURE.COMPLETED"), Is.False);
        Assert.That(await wallet.GetBalanceAsync(accountId, TestCancellation), Is.EqualTo(new ShopBalance(0, 0)));
        remote.VerifySignature = true;
        remote.Amount = "6.00";
        Assert.ThrowsAsync<HttpRequestException>(() => NotifyAsync(service, first, "PAYMENT.CAPTURE.COMPLETED"));
        remote.Amount = "5.00";
        Assert.That(await Task.WhenAll(NotifyAsync(service, first, "PAYMENT.CAPTURE.COMPLETED"),
            NotifyAsync(service, first, "PAYMENT.CAPTURE.COMPLETED")), Is.All.True);
        Assert.That(await wallet.GetBalanceAsync(accountId, TestCancellation), Is.EqualTo(new ShopBalance(0, 100)));

        ShopTopUpOrder second = (await service.CreateAsync(accountId, new(Guid.NewGuid(), offer), offer, TestCancellation))!;
        await service.ReconcileAsync(second, true, false, null, TestCancellation);
        ShopOffer exchange = new("exchange", 120, 100);
        ShopExchangeRequest exchangeRequest = new(Guid.NewGuid(), exchange.Id, exchange.CrystalCost, exchange.GoldAmount);
        Assert.That((await wallet.ExchangeAsync(accountId, exchangeRequest, exchange, TestCancellation)).Balance,
            Is.EqualTo(new ShopBalance(100, 80)));
        ShopOffer spend = new("spend", 0, 0, GoldCost: 80);
        await wallet.ExchangeAsync(accountId, new(Guid.NewGuid(), spend.Id, 0, 0, 80), spend, TestCancellation);

        remote.CaptureStatus = "PARTIALLY_REFUNDED";
        Assert.That(await NotifyAsync(service, first, "PAYMENT.CAPTURE.REFUNDED"), Is.True);
        Assert.That((await store.FindAsync(first.OrderId, true, TestCancellation))!.Status, Is.EqualTo(ShopTopUpStatus.PartiallyRefunded));
        Assert.That(await wallet.GetBalanceAsync(accountId, TestCancellation), Is.EqualTo(new ShopBalance(20, 80)));

        remote.CaptureStatus = "REFUNDED";
        await Task.WhenAll(NotifyAsync(service, first, "PAYMENT.CAPTURE.REFUNDED"),
            NotifyAsync(service, first, "PAYMENT.CAPTURE.REFUNDED"));
        Assert.That((await store.FindAsync(first.OrderId, true, TestCancellation))!.Status, Is.EqualTo(ShopTopUpStatus.Reversed));
        Assert.That(await wallet.GetBalanceAsync(accountId, TestCancellation), Is.EqualTo(new ShopBalance(0, 100)));

        ShopOffer reward = new("reward", 0, 100);
        ShopExchangeResult repayment = await wallet.ExchangeAsync(accountId, new(Guid.NewGuid(), reward.Id, 0, 100),
            reward, TestCancellation);
        Assert.That(repayment.Balance, Is.EqualTo(new ShopBalance(20, 100)));
        Assert.That(await NotifyAsync(service, second, "PAYMENT.CAPTURE.REVERSED"), Is.True);
        Assert.That(await wallet.GetBalanceAsync(accountId, TestCancellation), Is.EqualTo(new ShopBalance(20, 0)));
    }

    private static async Task<bool> NotifyAsync(PayPalTopUpService service, ShopTopUpOrder order, string type)
    {
        HeaderDictionary headers = new()
        {
            ["PAYPAL-TRANSMISSION-ID"] = "transmission",
            ["PAYPAL-TRANSMISSION-TIME"] = "time",
            ["PAYPAL-CERT-URL"] = "certificate",
            ["PAYPAL-AUTH-ALGO"] = "algorithm",
            ["PAYPAL-TRANSMISSION-SIG"] = "signature"
        };
        JsonElement notification = JsonSerializer.SerializeToElement(new
        {
            event_type = type,
            resource = new
            {
                id = "CAPTURE" + order.PayPalOrderId,
                supplementary_data = new { related_ids = new { order_id = order.PayPalOrderId } },
                links = new[] { new { rel = "up", href = $"https://api-m.sandbox.paypal.com/v2/payments/captures/CAPTURE{order.PayPalOrderId}" } }
            }
        });
        return await service.NotifyAsync(headers, notification, TestCancellation);
    }

    // 模擬 PayPal 建單、授權、請款及驗證回應。
    private sealed class PayPalTestHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, string> _orders = [];
        private readonly HashSet<string> _captures = [];
        internal int CreateCount { get; private set; }
        internal bool VerifySignature { get; set; } = true;
        internal string Amount { get; set; } = "5.00";
        internal string CaptureStatus { get; set; } = "COMPLETED";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = request.RequestUri!.AbsolutePath;
            if (path == "/v1/oauth2/token") return Json(new { access_token = "mock-token" });
            if (path == "/v1/notifications/verify-webhook-signature")
                return Json(new { verification_status = VerifySignature ? "SUCCESS" : "FAILURE" });

            if (request.Method == HttpMethod.Post && path == "/v2/checkout/orders")
            {
                string body = await request.Content!.ReadAsStringAsync(cancellationToken);
                using JsonDocument document = JsonDocument.Parse(body);
                string key = document.RootElement.GetProperty("purchase_units")[0].GetProperty("custom_id").GetString()!;
                Assert.That(request.Headers.GetValues("PayPal-Request-Id").Single(), Is.EqualTo(key));
                string paypalOrder = "ORDER" + ++CreateCount;
                _orders.Add(paypalOrder, key);
                return Json(new { id = paypalOrder, links = new[] { new { rel = "payer-action",
                    href = "https://www.sandbox.paypal.com/checkoutnow?token=" + paypalOrder } } });
            }

            string orderId = path.Split('/')[4];
            if (request.Method == HttpMethod.Post)
            {
                _captures.Add(orderId);
                return Json(new { id = orderId, status = "COMPLETED" });
            }

            bool captured = _captures.Contains(orderId);
            var amount = new { currency_code = "USD", value = Amount };
            return Json(new
            {
                id = orderId, intent = "CAPTURE", status = captured ? "COMPLETED" : "APPROVED",
                purchase_units = new[] { new
                {
                    custom_id = _orders[orderId], amount,
                    payments = captured ? new { captures = new[] { new { id = "CAPTURE" + orderId, status = CaptureStatus, amount } } } : null
                } }
            });
        }

        private static HttpResponseMessage Json(object content) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(content), Encoding.UTF8, "application/json")
        };
    }
}
