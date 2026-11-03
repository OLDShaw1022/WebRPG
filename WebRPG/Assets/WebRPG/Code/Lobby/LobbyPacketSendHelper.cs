using System;
using NativeWebSocket;
using Newtonsoft.Json;
using UnityEngine;
using WebRPGShared;

namespace WebRPG
{
    // 統一序列化並傳送大廳封包，將失敗交回連線擁有者處理。
    internal static class LobbyPacketSendHelper
    {
        public static async Awaitable SendAsync(WebSocket socket, LobbyPacket packet,
            Action<WebSocket, Exception> failed)
        {
            try
            {
                await socket.SendText(JsonConvert.SerializeObject(packet));
            }
            catch (Exception exception)
            {
                failed(socket, exception);
            }
        }
    }
}
