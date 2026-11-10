using System;
using Newtonsoft.Json;
using UnityEngine;

namespace WebRPG
{
    // 透過登入帳號的伺服器讀取商店與送出已確認的兌換。
    internal sealed class ShopModel
    {
        private const string ShopPath = "shop";
        private const string ExchangePath = "shop/exchange";
        private const string InvalidResponseMessage = "商店回應不完整，請重試。";
        private readonly AccountModel _account;

        public ShopModel(AccountModel account) => _account = account;

        public async Awaitable<ShopSnapshot> LoadAsync()
        {
            string json = await _account.SendAuthenticatedAsync(ShopPath, string.Empty);
            ShopSnapshot snapshot = JsonConvert.DeserializeObject<ShopSnapshot>(json);
            if (snapshot?.Balance == null || snapshot.Offers == null)
                throw new InvalidOperationException(InvalidResponseMessage);
            return snapshot;
        }

        public async Awaitable<ShopExchangeResult> ExchangeAsync(Guid requestId, ShopOffer offer)
        {
            string body = JsonConvert.SerializeObject(new
            {
                requestId, offerId = offer.Id, crystalCost = offer.CrystalCost, goldAmount = offer.GoldAmount
            });
            string json = await _account.SendAuthenticatedAsync(ExchangePath, body);
            ShopExchangeResult result = JsonConvert.DeserializeObject<ShopExchangeResult>(json);
            if (result?.Balance == null) throw new InvalidOperationException(InvalidResponseMessage);
            return result;
        }
    }

    // 保存商店查詢回傳的餘額與兌換組合。
    internal sealed class ShopSnapshot
    {
        public ShopBalance Balance { get; set; }
        public ShopOffer[] Offers { get; set; }
    }

    // 保存帳號錢包兩種貨幣的餘額。
    internal sealed class ShopBalance
    {
        public long Gold { get; set; }
        public long Crystals { get; set; }
    }

    // 保存玩家可確認的伺服器商品條件。
    internal sealed class ShopOffer
    {
        public string Id { get; set; }
        public long CrystalCost { get; set; }
        public long GoldAmount { get; set; }
    }

    // 保存一次兌換的伺服器結果。
    internal sealed class ShopExchangeResult
    {
        public ShopExchangeStatus Status { get; set; }
        public ShopBalance Balance { get; set; }
        public ShopOffer Offer { get; set; }
    }

    // 對應商店 API 的兌換結果代碼。
    internal enum ShopExchangeStatus
    {
        Exchanged = 0,
        OfferChanged = 1,
        InsufficientCrystals = 2,
        BalanceLimit = 3,
        RequestConflict = 4
    }
}
