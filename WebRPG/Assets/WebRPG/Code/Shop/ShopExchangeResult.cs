namespace WebRPG
{
    // 保存一次兌換的伺服器結果。
    internal sealed class ShopExchangeResult
    {
        public ShopExchangeStatus Status { get; set; }
        public long GoldDebtPaid { get; set; }
        public ShopBalance Balance { get; set; }
        public ShopOffer Offer { get; set; }
    }
}
