namespace WebRPGServer;

// 保存對應的 PayPal 訂單與收款資料。
internal sealed record ShopTopUpOrder(Guid OrderId, Guid AccountId, ShopTopUpOffer Offer, ShopTopUpStatus Status,
    DateTime CreatedAt, string? PayPalOrderId, string? ApprovalUrl, string? CaptureId, bool Sandbox)
{
    internal ShopTopUpReceipt Receipt => new(OrderId, Offer, Status, CreatedAt,
        Status == ShopTopUpStatus.AwaitingPayment ? ApprovalUrl : null);
}
