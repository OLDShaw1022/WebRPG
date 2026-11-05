using System;
using UnityEngine;

namespace WebRPG
{
    // 協調帳號表單與 API，防止重複送出並處理非同步錯誤。
    public sealed class AccountPresenter : IDisposable
    {
        private const string InitialMessage = "請登入，或填寫使用者名稱後註冊新帳號。\n密碼須為 8～128 個字元。";
        private const string PendingMessage = "處理中，請稍候…";
        private readonly AccountModel _model;
        private readonly AccountView _view;
        private readonly Action _loggedIn;
        private bool _busy;
        private bool _disposed;

        public AccountPresenter(AccountModel model, AccountView view, Action loggedIn)
        {
            _model = model;
            _view = view;
            _loggedIn = loggedIn;
            _view.Configure(model.ServerNames, model.DefaultServerIndex);
            _view.Submitted += Submit;
            _view.Render(false, InitialMessage);
        }

        private void Submit(int server, string email, string password, string username, bool register)
        {
            if (_busy || _disposed) return;
            SubmitAsync(server, email, password, username, register).Forget();
        }

        private async Awaitable SubmitAsync(int server, string email, string password, string username, bool register)
        {
            _busy = true;
            _view.Render(true, PendingMessage);
            try
            {
                string status = await _model.SubmitAsync(server, email, password, username, register);
                if (_disposed || !_view) return;
                _view.ClearPassword();
                _view.Render(false, status);
                if (_model.IsLoggedIn) _loggedIn();
            }
            catch (Exception exception)
            {
                if (!_disposed && _view) _view.Render(false, exception.Message);
            }
            finally { _busy = false; }
        }

        public void Dispose()
        {
            _disposed = true;
            if (_view) _view.Submitted -= Submit;
        }
    }
}
