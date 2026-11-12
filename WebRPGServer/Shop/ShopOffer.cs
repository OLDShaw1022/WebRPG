namespace WebRPGServer;

// 定義伺服器管理的固定兌換組合。
public sealed record ShopOffer(string Id, long CrystalCost, long GoldAmount, long GoldCost = 0)
{
    internal bool IsValid => !string.IsNullOrWhiteSpace(Id) && Id.Length <= ShopCatalog.MaximumOfferIdLength &&
        (GoldCost > 0 ? CrystalCost == 0 && GoldAmount == 0 : GoldCost == 0 && CrystalCost >= 0 && GoldAmount > 0);
}
