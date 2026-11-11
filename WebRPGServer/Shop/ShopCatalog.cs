namespace WebRPGServer;

// 載入並驗證伺服器的兌換商品目錄。
public sealed class ShopCatalog
{
    public const int MaximumOfferIdLength = 64;
    private readonly Dictionary<string, ShopOffer> _offers;
    public IReadOnlyCollection<ShopOffer> Offers => _offers.Values;

    public ShopCatalog(IConfiguration configuration)
    {
        ShopOffer[] offers = configuration.GetSection("Shop:Offers").Get<ShopOffer[]>() ?? [];
        _offers = new Dictionary<string, ShopOffer>(StringComparer.Ordinal);
        foreach (ShopOffer offer in offers)
        {
            if (!offer.IsValid || !_offers.TryAdd(offer.Id, offer))
                throw new InvalidOperationException("Shop:Offers contains invalid or duplicate exchange terms.");
        }
    }

    public ShopOffer? Find(string id) => _offers.GetValueOrDefault(id);
}
