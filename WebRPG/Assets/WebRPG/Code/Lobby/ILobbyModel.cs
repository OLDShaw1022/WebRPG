using System;
using UnityEngine;
using WebRPGShared;

namespace WebRPG
{
    // 定義大廳 Presenter 使用的連線與伺服器狀態介面。
    public interface ILobbyModel
    {
        public event Action Changed;
        public event Action<AdventurePacket> AdventureReceived;
        public bool IsConnected { get; }
        public bool IsBusy { get; }
        public string PlayerId { get; }
        public string Status { get; }
        public LobbyRoomSnapshot Room { get; }
        public LobbyRoomSummary[] Rooms { get; }
        public Awaitable ConnectAsync();
        public Awaitable SendAsync(LobbyPacket packet);
        public Awaitable SendAdventureAsync(AdventurePacket packet);
    }
}
