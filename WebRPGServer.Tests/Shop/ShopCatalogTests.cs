using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using WebRPGServer;

namespace WebRPGServer.Tests;

// 驗證預設商品目錄包含指定價格的兌換與儲值方案。
public sealed class ShopCatalogTests
{
    [Test]
    public void DefaultCatalogIncludesExchangePurchaseAndTopUp()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(TestContext.CurrentContext.TestDirectory)
            .AddJsonFile("appsettings.json")
            .Build();
        ShopCatalog catalog = new(configuration);

        ShopOffer? exchange = catalog.Find("crystals-for-gold");
        Assert.That(exchange, Is.EqualTo(new ShopOffer("crystals-for-gold", 10, 100)));

        ShopOffer? purchase = catalog.Find("empty-item");
        Assert.That(purchase, Is.EqualTo(new ShopOffer("empty-item", 0, 0, GoldCost: 100)));

        ShopTopUpCatalog topUps = new(configuration);
        Assert.That(
            topUps.Find("crystals-100-twd-1"), Is.EqualTo(new ShopTopUpOffer("crystals-100-twd-1", "TWD", 1m, 100)));
    }
}
