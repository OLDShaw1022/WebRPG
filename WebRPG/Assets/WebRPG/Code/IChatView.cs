using System;
using WebRPGShared;

namespace WebRPG
{
    public interface IChatView
    {
        public event Action ConnectRequested;
        public event Action SendRequested;

        public string UsernameText { get; }
        public string MessageText { get; set; }

        public void SetLoginHint(string hint);

        public void SetChannelStatus(string status);

        public void SetConnectEnabled(bool enabled);

        public void SetSendEnabled(bool enabled);

        public void ShowChat();

        public void ShowMessage(ChatPacket packet, bool isOutgoing);

        public void ReportError(Exception exception);
    }
}