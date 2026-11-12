using System;

namespace WebRPG
{
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
}
