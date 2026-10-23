using System;
using System.Threading.Tasks;
using WebRPGShared;

namespace WebRPG
{
    public interface IChatModel
    {
        public event Action<ChatPacket> PacketReceived;
        public event Action<string> ConnectionStateChanged;

        public bool IsJoined { get; }

        public bool IsUsernameValid(string username);

        public Task ConnectAsync(string username);

        public Task<bool> SendChatAsync(string text);
    }
}