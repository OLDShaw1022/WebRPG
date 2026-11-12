namespace WebRPGServer;

// 回傳兌換結果、餘額及目前的商品條件。
public sealed record ShopExchangeResult(
    ShopExchangeStatus Status, ShopBalance Balance, ShopOffer? Offer, long GoldDebtPaid = 0);
