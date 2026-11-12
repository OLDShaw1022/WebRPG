namespace WebRPGServer;

// 提供玩家可查詢的訂單快照。
public sealed record ShopTopUpReceipt(Guid OrderId, ShopTopUpOffer Offer, ShopTopUpStatus Status,
    DateTime CreatedAt, string? CheckoutUrl);
