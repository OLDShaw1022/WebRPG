using System;
using UnityEngine;
using WebRPGShared;

namespace WebRPG
{
    public interface IChatModel
    {
        public event Action<ChatPacket> PacketReceived;
        public event Action<string> ConnectionStateChanged;

        public bool IsJoined { get; }

        public bool IsUsernameValid(string username);

        public Awaitable ConnectAsync(string username);

        public Awaitable<bool> SendChatAsync(string text);
    }
}