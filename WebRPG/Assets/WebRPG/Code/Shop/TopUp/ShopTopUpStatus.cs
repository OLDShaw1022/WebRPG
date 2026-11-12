namespace WebRPG
{
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
}
