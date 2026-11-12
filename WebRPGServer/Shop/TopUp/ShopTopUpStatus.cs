namespace WebRPGServer;

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
