using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace WebRPG
{
    // 顯示登入與註冊表單，並轉送使用者操作。
    public sealed class AccountView : MonoBehaviour
    {
        [SerializeField] private TMP_InputField _email;
        [SerializeField] private TMP_InputField _password;
        [SerializeField] private TMP_InputField _username;
        [SerializeField] private TMP_Dropdown _server;
        [SerializeField] private UnityEngine.UI.Button _login;
        [SerializeField] private UnityEngine.UI.Button _register;
        [SerializeField] private TextMeshProUGUI _status;
        public event Action<int, string, string, string, bool> Submitted;

        private void Awake()
        {
            _login.onClick.AddListener(Login);
            _register.onClick.AddListener(Register);
            _status.richText = false;
        }

        public void Configure(string[] servers, int defaultIndex)
        {
            _server.ClearOptions();
            _server.AddOptions(new List<string>(servers));
            _server.SetValueWithoutNotify(defaultIndex);
            _server.RefreshShownValue();
        }

        public void Render(bool busy, string status)
        {
            _login.interactable = !busy;
            _register.interactable = !busy;
            _email.interactable = !busy;
            _password.interactable = !busy;
            _username.interactable = !busy;
            _server.interactable = !busy;
            _status.text = status;
        }

        public void ClearPassword() => _password.text = string.Empty;
        private void Login() => Submitted?.Invoke(_server.value, _email.text, _password.text, _username.text, false);
        private void Register() => Submitted?.Invoke(_server.value, _email.text, _password.text, _username.text, true);
        private void OnDestroy()
        {
            _login.onClick.RemoveListener(Login);
            _register.onClick.RemoveListener(Register);
        }
    }
}
