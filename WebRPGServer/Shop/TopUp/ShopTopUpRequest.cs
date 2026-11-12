namespace WebRPGServer;

// 使用同一個請求識別重試，並核對玩家確認的方案。
public sealed record ShopTopUpRequest(Guid RequestId, ShopTopUpOffer Offer);
