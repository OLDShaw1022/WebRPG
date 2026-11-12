namespace WebRPG
{
    // 保存建立儲值訂單的結果。
    internal sealed class ShopTopUpResult
    {
        public bool OfferChanged { get; set; }
        public ShopTopUpOrder Order { get; set; }
    }
}
