namespace WebRPGServer;

// 定義客戶端處理兌換結果時使用的狀態。
public enum ShopExchangeStatus
{
    Exchanged = 0,
    OfferChanged = 1,
    InsufficientCrystals = 2,
    BalanceLimit = 3,
    RequestConflict = 4,
    InsufficientGold = 5
}
