using System.Text.Json.Serialization;

namespace WebRPGServer;

// 表示帳號錢包的金幣與水晶餘額。
public sealed record ShopBalance(long Gold, long Crystals);

// 定義伺服器管理的固定兌換組合。
public sealed record ShopOffer(string Id, long CrystalCost, long GoldAmount);

// 附上玩家確認的條件，避免目錄變更後仍用舊條件兌換。
public sealed record ShopExchangeRequest(Guid RequestId, string OfferId,
    [property: JsonRequired] long CrystalCost, long GoldAmount);

// 回傳兌換結果、餘額及目前的商品條件。
public sealed record ShopExchangeResult(ShopExchangeStatus Status, ShopBalance Balance, ShopOffer? Offer);

// 定義客戶端處理兌換結果時使用的狀態。
public enum ShopExchangeStatus
{
    Exchanged = 0,
    OfferChanged = 1,
    InsufficientCrystals = 2,
    BalanceLimit = 3,
    RequestConflict = 4
}
