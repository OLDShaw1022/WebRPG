using System;
using UnityEngine;

namespace WebRPG
{
    // 協調帳號表單與 API，防止重複送出並處理非同步錯誤。
    public sealed class AccountPresenter : IDisposable
    {
        private const string InitialMessage = "請使用 Email 與密碼登入。";
        private const string RegistrationMessage = "請填寫 Email、密碼與使用者名稱。\n密碼須為 8～128 個字元。";
        private const string PendingMessage = "處理中，請稍候…";
        private const string RestoringMessage = "正在恢復登入…";
        private const string RestoreFailedMessage = "無法恢復登入，請重新整理重試，或使用 Email 與密碼登入。";
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
            _view.Configure(model.ServerNames, model.StartupServerIndex);
            _view.Submitted += Submit;
            _view.PageRequested += ChangePage;
            _view.Render(false, InitialMessage);
        }

        public async Awaitable RestoreAsync()
        {
            if (_busy || _disposed) return;
            _busy = true;
            _view.Render(true, RestoringMessage);
            try
            {
                bool restored = await _model.TryRestoreAsync(_model.StartupServerIndex);
                if (_disposed || !_view) return;
                _view.Render(false, InitialMessage);
                if (restored) _loggedIn();
            }
            catch (Exception)
            {
                if (!_disposed && _view) _view.Render(false, RestoreFailedMessage);
            }
            finally { _busy = false; }
        }

        private void ChangePage(bool registration)
        {
            if (_busy || _disposed) return;
            _view.ShowPage(registration);
            _view.Render(false, registration ? RegistrationMessage : InitialMessage);
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
                string status = await _model.SubmitAsync(server, email, password, username, register, _view.RememberMe);
                if (_disposed || !_view) return;
                _view.ClearPassword();
                if (register && _model.RegistrationSucceeded) _view.ShowPage(false);
                _view.Render(false, status);
                if (_model.IsLoggedIn)
                {
                    _model.SaveLoginServerSelection();
                    _loggedIn();
                }
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
            if (_view)
            {
                _view.Submitted -= Submit;
                _view.PageRequested -= ChangePage;
            }
        }
    }
}
