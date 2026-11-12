namespace WebRPGShared
{
    // 定義多人冒險的開局、操作及終止封包。
    public static class AdventurePacketType
    {
        public const string Start = "AdventureStart";
        public const string Command = "AdventureCommand";
        public const string Failed = "AdventureFailed";
        public const string Error = "AdventureError";
    }
}
