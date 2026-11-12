namespace WebRPG
{
    // 對應商店 API 的兌換結果代碼。
    internal enum ShopExchangeStatus
    {
        Exchanged = 0,
        OfferChanged = 1,
        InsufficientCrystals = 2,
        BalanceLimit = 3,
        RequestConflict = 4,
        InsufficientGold = 5
    }
}
