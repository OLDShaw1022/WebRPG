namespace WebRPGServer;

// 保存伺服器提供及玩家確認的儲值條件。
public sealed record ShopTopUpOffer(string Id, string Currency, decimal Amount, long Crystals);

// 使用同一個請求識別重試，並核對玩家確認的方案。
public sealed record ShopTopUpRequest(Guid RequestId, ShopTopUpOffer Offer);

// 回傳付款與沖銷結果。
public enum ShopTopUpStatus
{
    AwaitingPayment,
    Processing,
    Credited,
    Failed,
    Canceled,
    PartiallyRefunded,
    Reversed
}

// 提供玩家可查詢的訂單快照。
public sealed record ShopTopUpReceipt(Guid OrderId, ShopTopUpOffer Offer, ShopTopUpStatus Status,
    DateTime CreatedAt, string? CheckoutUrl);

// 保存對應的 PayPal 訂單與收款資料。
internal sealed record ShopTopUpOrder(Guid OrderId, Guid AccountId, ShopTopUpOffer Offer, ShopTopUpStatus Status,
    DateTime CreatedAt, string? PayPalOrderId, string? ApprovalUrl, string? CaptureId, bool Sandbox)
{
    internal ShopTopUpReceipt Receipt => new(OrderId, Offer, Status, CreatedAt,
        Status == ShopTopUpStatus.AwaitingPayment ? ApprovalUrl : null);
}
