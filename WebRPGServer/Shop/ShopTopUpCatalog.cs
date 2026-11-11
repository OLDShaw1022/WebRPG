namespace WebRPGServer;

// 載入可公開銷售的水晶方案，並檢查 PayPal 接受的幣別與金額。
public sealed class ShopTopUpCatalog
{
    private const int DecimalCurrencyScale = 2;
    private static readonly HashSet<string> SupportedCurrencies = new(StringComparer.Ordinal)
    {
        "AUD", "BRL", "CAD", "CNY", "CZK", "DKK", "EUR", "HKD", "HUF", "ILS", "JPY", "MYR",
        "MXN", "TWD", "NZD", "NOK", "PHP", "PLN", "GBP", "SGD", "SEK", "CHF", "THB", "USD"
    };
    private static readonly HashSet<string> WholeCurrencies = new(StringComparer.Ordinal) { "HUF", "JPY", "TWD" };
    private readonly Dictionary<string, ShopTopUpOffer> _offers = new(StringComparer.Ordinal);

    public ShopTopUpCatalog(IConfiguration configuration)
    {
        foreach (IConfigurationSection section in configuration.GetSection("Shop:TopUpOffers").GetChildren())
        {
            if (!section.GetValue<bool>("Enabled")) continue;
            ShopTopUpOffer? offer = section.Get<ShopTopUpOffer>();
            if (offer is null || !IsValid(offer) || !_offers.TryAdd(offer.Id, offer))
                throw new InvalidOperationException("Shop:TopUpOffers contains invalid or duplicate payment terms.");
        }
    }

    public IReadOnlyCollection<ShopTopUpOffer> Offers => _offers.Values;

    public ShopTopUpOffer? Find(string id) => _offers.GetValueOrDefault(id);

    internal static bool IsValid(ShopTopUpOffer offer) =>
        !string.IsNullOrWhiteSpace(offer.Id) && offer.Id.Length <= ShopCatalog.MaximumOfferIdLength &&
        offer.Currency is not null && SupportedCurrencies.Contains(offer.Currency) && offer.Amount > 0 &&
        decimal.Round(offer.Amount, WholeCurrencies.Contains(offer.Currency) ? 0 : DecimalCurrencyScale) == offer.Amount &&
        offer.Crystals > 0;
}
