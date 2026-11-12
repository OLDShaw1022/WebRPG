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
        private const string TopUpPath = "shop/top-ups";
        private const string TopUpSyncPath = "shop/top-ups/{0:D}/sync";
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

        public async Awaitable SyncTopUpAsync(Guid orderId)
        {
            await _account.SendAuthenticatedAsync(string.Format(TopUpSyncPath, orderId), string.Empty);
        }

        public async Awaitable<ShopTopUpResult> CreateTopUpAsync(Guid requestId, ShopTopUpOffer offer)
        {
            string body = JsonConvert.SerializeObject(new { requestId, offer });
            string json = await _account.SendAuthenticatedAsync(TopUpPath, body);
            ShopTopUpResult result = JsonConvert.DeserializeObject<ShopTopUpResult>(json);
            if (result == null || (!result.OfferChanged && result.Order?.Offer == null))
                throw new InvalidOperationException(InvalidResponseMessage);
            return result;
        }

        public async Awaitable<ShopExchangeResult> ExchangeAsync(Guid requestId, ShopOffer offer)
        {
            string body = JsonConvert.SerializeObject(new
            {
                requestId, offerId = offer.Id, crystalCost = offer.CrystalCost,
                goldAmount = offer.GoldAmount, goldCost = offer.GoldCost
            });
            string json = await _account.SendAuthenticatedAsync(ExchangePath, body);
            ShopExchangeResult result = JsonConvert.DeserializeObject<ShopExchangeResult>(json);
            if (result?.Balance == null) throw new InvalidOperationException(InvalidResponseMessage);
            return result;
        }
    }
}
