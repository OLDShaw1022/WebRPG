using System.Text.Json.Serialization;

namespace WebRPGServer;

// 附上玩家確認的條件，避免目錄變更後仍用舊條件兌換。
public sealed record ShopExchangeRequest(Guid RequestId, string OfferId,
    [property: JsonRequired]
    long CrystalCost, long GoldAmount, long GoldCost = 0)
{
    internal ShopOffer ConfirmedOffer => new(OfferId, CrystalCost, GoldAmount, GoldCost);
}
