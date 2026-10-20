using System;

namespace WebRPGShared
{
    [Serializable]
    public sealed class ChatPacket
    {
        public string Type { get; set; }
        public string Username { get; set; }
        public string Text { get; set; }
        public string ConnectionId { get; set; }
        public DateTimeOffset TimestampUtc { get; set; }
    }

    public static class ChatPacketType
    {
        public const string Join = "Join";
        public const string Joined = "Joined";
        public const string Chat = "Chat";
        public const string Error = "Error";
    }

    public static class ChatPacketLimits
    {
        public const int UsernameLength = 24;
        public const int MessageLength = 200;
    }
}
