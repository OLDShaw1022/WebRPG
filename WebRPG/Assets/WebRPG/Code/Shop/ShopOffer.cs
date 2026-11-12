namespace WebRPG
{
    // 保存玩家可確認的伺服器商品條件。
    internal sealed class ShopOffer
    {
        public string Id { get; set; }
        public long CrystalCost { get; set; }
        public long GoldAmount { get; set; }
        public long GoldCost { get; set; }
        public bool IsGoldPurchase => GoldCost > 0;
    }
}
