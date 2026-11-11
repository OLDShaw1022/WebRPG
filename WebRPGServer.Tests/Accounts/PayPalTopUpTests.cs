using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using WebRPGServer;

namespace WebRPGServer.Tests;

// 驗證儲值目錄只公開已啟用且符合付款幣別精度的方案。
public sealed class PayPalTopUpTests
{
    [Test]
    public void EmptyCatalogDoesNotPublishProducts()
    {
        ShopTopUpCatalog catalog = new(new ConfigurationBuilder().Build());

        Assert.That(catalog.Offers, Is.Empty);
    }

    [Test]
    public void DisabledProductIsNotPublished()
    {
        ShopTopUpCatalog catalog = BuildCatalog("TWD", "150", "false");

        Assert.That(catalog.Offers, Is.Empty);
        Assert.That(catalog.Find("starter"), Is.Null);
    }

    [TestCase("TWD", "150")]
    [TestCase("USD", "4.99")]
    public void EnabledProductPreservesConfirmedTerms(string currency, string amount)
    {
        ShopTopUpCatalog catalog = BuildCatalog(currency, amount, "true");

        Assert.That(catalog.Find("starter"), Is.EqualTo(new ShopTopUpOffer(
            "starter", currency, decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), 100)));
    }

    [TestCase("TWD", "150.5")]
    [TestCase("USD", "4.999")]
    [TestCase("INVALID", "5")]
    [TestCase("USD", "0")]
    public void InvalidPricePreventsStartup(string currency, string amount)
    {
        Assert.Throws<InvalidOperationException>(() => BuildCatalog(currency, amount, "true"));
    }

    private static ShopTopUpCatalog BuildCatalog(string currency, string amount, string enabled)
    {
        return new ShopTopUpCatalog(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Shop:TopUpOffers:0:Id"] = "starter",
            ["Shop:TopUpOffers:0:Currency"] = currency,
            ["Shop:TopUpOffers:0:Amount"] = amount,
            ["Shop:TopUpOffers:0:Crystals"] = "100",
            ["Shop:TopUpOffers:0:Enabled"] = enabled
        }).Build());
    }
}
