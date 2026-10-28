using System;
using UnityEngine;
using WebRPGShared;

namespace WebRPG
{
    public sealed class ChatPresenter : IDisposable
    {
        private const string UsernameLengthHintFormat = "請輸入 1 到 {0} 個字元的使用者名稱。";
        private const string ConnectingHint = "正在連接聊天室...";
        private const string ConnectingStatus = "Connecting";
        private const string ConnectionFailedStatusPrefix = "Connection failed";

        private readonly IChatModel _model;
        private readonly IChatView _view;
        private string _connectionId;
        private bool _hasStartedConnecting;
        private bool _hasOpenedChat;
        private bool _isSending;
        private bool _isDisposed;

        public ChatPresenter(IChatModel model, IChatView view)
        {
            _model = model;
            _view = view;
            _view.ConnectRequested += HandleConnectRequested;
            _view.SendRequested += HandleSendRequested;
            _model.PacketReceived += HandlePacket;
            _model.ConnectionStateChanged += HandleConnectionStateChanged;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _view.ConnectRequested -= HandleConnectRequested;
            _view.SendRequested -= HandleSendRequested;
            _model.PacketReceived -= HandlePacket;
            _model.ConnectionStateChanged -= HandleConnectionStateChanged;
        }

        private void HandleConnectRequested()
        {
            HandleConnectRequestedAsync().Forget();
        }

        private async Awaitable HandleConnectRequestedAsync()
        {
            if (_isDisposed || _hasStartedConnecting)
            {
                return;
            }

            string username = _view.UsernameText.Trim();
            if (!_model.IsUsernameValid(username))
            {
                _view.SetLoginHint(string.Format(UsernameLengthHintFormat, ChatPacketLimits.UsernameLength));
                return;
            }

            _view.SetLoginHint(ConnectingHint);
            _hasStartedConnecting = true;
            _view.SetConnectEnabled(false);

            try
            {
                await _model.ConnectAsync(username);
            }
            catch (Exception exception)
            {
                if (!_isDisposed)
                {
                    _hasStartedConnecting = false;
                    _view.SetConnectEnabled(true);
                    _view.ReportError(exception);
                }
            }
        }

        private void HandleSendRequested()
        {
            HandleSendRequestedAsync().Forget();
        }

        private async Awaitable HandleSendRequestedAsync()
        {
            if (_isDisposed || _isSending || !_model.IsJoined)
            {
                return;
            }

            string originalText = _view.MessageText;
            _isSending = true;
            _view.SetSendEnabled(false);

            try
            {
                bool wasSent = await _model.SendChatAsync(originalText);
                if (!_isDisposed && wasSent && _view.MessageText == originalText)
                {
                    _view.MessageText = string.Empty;
                }
            }
            catch (Exception exception)
            {
                if (!_isDisposed)
                {
                    _view.ReportError(exception);
                }
            }
            finally
            {
                _isSending = false;
                if (!_isDisposed)
                {
                    _view.SetSendEnabled(_model.IsJoined);
                }
            }
        }

        private void HandlePacket(ChatPacket packet)
        {
            if (packet.Type == ChatPacketType.Joined)
            {
                _connectionId = packet.ConnectionId;
                _hasOpenedChat = true;
                _view.ShowChat();
                _view.SetSendEnabled(!_isSending);
                _view.SetLoginHint(string.Empty);
                return;
            }

            if (packet.Type == ChatPacketType.Chat)
            {
                _view.ShowMessage(packet, packet.ConnectionId == _connectionId);
            }
            else if (packet.Type == ChatPacketType.Error)
            {
                _view.SetLoginHint(packet.Text);
            }
        }

        private void HandleConnectionStateChanged(string status)
        {
            _view.SetChannelStatus(status);
            bool isJoined = _model.IsJoined;
            _view.SetSendEnabled(isJoined && !_isSending);
            if (!_hasOpenedChat)
            {
                _view.SetConnectEnabled(!_hasStartedConnecting && !isJoined && status != ConnectingStatus);
                if (status.StartsWith(ConnectionFailedStatusPrefix, StringComparison.Ordinal))
                {
                    _view.SetLoginHint(status);
                }
            }
        }
    }
}
