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

    // 保存商店查詢回傳的餘額與兌換組合。
    internal sealed class ShopSnapshot
    {
        public ShopBalance Balance { get; set; }
        public ShopOffer[] Offers { get; set; }
        public ShopTopUpOffer[] TopUpOffers { get; set; } = Array.Empty<ShopTopUpOffer>();
        public ShopTopUpOrder[] TopUpOrders { get; set; } = Array.Empty<ShopTopUpOrder>();
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
        public long GoldCost { get; set; }
        public bool IsGoldPurchase => GoldCost > 0;
    }

    // 保存一次兌換的伺服器結果。
    internal sealed class ShopExchangeResult
    {
        public ShopExchangeStatus Status { get; set; }
        public long GoldDebtPaid { get; set; }
        public ShopBalance Balance { get; set; }
        public ShopOffer Offer { get; set; }
    }

    // 保存儲值方案的價格、幣別與發放數量。
    internal sealed class ShopTopUpOffer
    {
        public string Id { get; set; }
        public string Currency { get; set; }
        public decimal Amount { get; set; }
        public long Crystals { get; set; }
    }

    // 保存可查詢的訂單及可繼續付款的網址。
    internal sealed class ShopTopUpOrder
    {
        private const string LiveHost = "www.paypal.com";
        private const string SandboxHost = "www.sandbox.paypal.com";

        public Guid OrderId { get; set; }
        public ShopTopUpOffer Offer { get; set; }
        public ShopTopUpStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CheckoutUrl { get; set; }

        internal bool CanOpenCheckout => Status == ShopTopUpStatus.AwaitingPayment &&
            Uri.TryCreate(CheckoutUrl, UriKind.Absolute, out Uri url) && url.Scheme == Uri.UriSchemeHttps &&
            url.IsDefaultPort && url.UserInfo.Length == 0 && (url.Host == LiveHost || url.Host == SandboxHost);
    }

    // 保存建立儲值訂單的結果。
    internal sealed class ShopTopUpResult
    {
        public bool OfferChanged { get; set; }
        public ShopTopUpOrder Order { get; set; }
    }

    // 對應伺服器回傳的訂單狀態。
    internal enum ShopTopUpStatus
    {
        AwaitingPayment,
        Processing,
        Credited,
        Failed,
        Canceled,
        PartiallyRefunded,
        Reversed
    }

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
