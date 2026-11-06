using System;
using System.Collections.Generic;
using UnityEngine;
using WebRPGShared;

namespace WebRPG.Tests
{
    // 提供多人重播測試的獨立連線狀態與封包佇列。
    internal sealed class TestMultiplayerLobbyModel : ILobbyModel
    {
        public event Action Changed;
        public event Action<AdventurePacket> AdventureReceived;
        public bool IsConnected { get; set; } = true;
        public bool IsBusy => false;
        public string PlayerId { get; set; }
        public string Status => string.Empty;
        public LobbyRoomSnapshot Room { get; set; }
        public LobbyRoomSummary[] Rooms => Array.Empty<LobbyRoomSummary>();
        public List<AdventurePacket> Sent { get; } = new List<AdventurePacket>();
        public void Emit(AdventurePacket packet) => AdventureReceived?.Invoke(packet);
        public void Notify() => Changed?.Invoke();
        public Awaitable ConnectAsync() => TestAwaitableHelper.Complete();
        public Awaitable SendAsync(LobbyPacket packet) => TestAwaitableHelper.Complete();
        public Awaitable SendAdventureAsync(AdventurePacket packet)
        {
            Sent.Add(packet);
            return TestAwaitableHelper.Complete();
        }

    }
}
