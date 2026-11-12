namespace WebRPG
{
    // 保存儲值方案的價格、幣別與發放數量。
    internal sealed class ShopTopUpOffer
    {
        public string Id { get; set; }
        public string Currency { get; set; }
        public decimal Amount { get; set; }
        public long Crystals { get; set; }
    }
}
