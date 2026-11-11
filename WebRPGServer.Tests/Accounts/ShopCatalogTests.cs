using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using WebRPGServer;

namespace WebRPGServer.Tests;

// 驗證預設商品目錄包含兩種指定價格的商品。
public sealed class ShopCatalogTests
{
    [Test]
    public void DefaultCatalogIncludesCrystalExchangeAndGoldPurchase()
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
    }
}
