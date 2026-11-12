using System;
using UnityEngine;
using WebRPGShared;

namespace WebRPG.Tests
{
    // 模擬伺服器狀態並記錄送出的命令。
    internal sealed class TestLobbyModel : ILobbyModel
    {
        public event Action Changed;
        public event Action<AdventurePacket> AdventureReceived;
        public bool IsConnected { get; set; } = true;
        public bool IsBusy => false;
        public string PlayerId => "self";
        public string Status => "Connected";
        public LobbyRoomSnapshot Room { get; set; }
        public LobbyRoomSummary[] Rooms => Array.Empty<LobbyRoomSummary>();
        public LobbyPacket Last;
        public int ConnectCalls;
        public void Notify() => Changed?.Invoke();
        public Awaitable ConnectAsync()
        {
            ConnectCalls++;
            AwaitableCompletionSource source = new AwaitableCompletionSource();
            source.SetResult();
            return source.Awaitable;
        }
        public Awaitable SendAsync(LobbyPacket packet)
        {
            Last = packet;
            AwaitableCompletionSource source = new AwaitableCompletionSource();
            source.SetResult();
            return source.Awaitable;
        }
        public Awaitable SendAdventureAsync(AdventurePacket packet)
        {
            AdventureReceived?.Invoke(packet);
            AwaitableCompletionSource source = new AwaitableCompletionSource();
            source.SetResult();
            return source.Awaitable;
        }
    }
}
