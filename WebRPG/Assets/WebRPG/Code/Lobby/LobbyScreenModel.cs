using WebRPGShared;

namespace WebRPG
{
    // 保存大廳 View 所需的顯示內容與可操作狀態。
    public sealed class LobbyScreenModel
    {
        public bool Connected;
        public bool Busy;
        public bool InRoom;
        public string Status;
        public string Members;
        public string ReadyLabel;
        public LobbyRoomSummary[] Rooms;
    }
}
