using System;

namespace WebRPG
{
    // 保存商店查詢回傳的餘額與兌換組合。
    internal sealed class ShopSnapshot
    {
        public ShopBalance Balance { get; set; }
        public ShopOffer[] Offers { get; set; }
        public ShopTopUpOffer[] TopUpOffers { get; set; } = Array.Empty<ShopTopUpOffer>();
        public ShopTopUpOrder[] TopUpOrders { get; set; } = Array.Empty<ShopTopUpOrder>();
    }
}
