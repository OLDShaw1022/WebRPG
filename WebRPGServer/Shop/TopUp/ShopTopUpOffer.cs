namespace WebRPGServer;

// 保存伺服器提供及玩家確認的儲值條件。
public sealed record ShopTopUpOffer(string Id, string Currency, decimal Amount, long Crystals);
