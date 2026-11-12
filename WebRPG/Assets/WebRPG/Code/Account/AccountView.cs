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
        [SerializeField] private GameObject _loginPage;
        [SerializeField] private GameObject _registrationPage;
        [SerializeField] private TMP_InputField _registrationEmail;
        [SerializeField] private TMP_InputField _registrationPassword;
        [SerializeField] private TMP_Dropdown _registrationServer;
        [SerializeField] private TextMeshProUGUI _registrationStatus;
        [SerializeField] private UnityEngine.UI.Button _openRegistration;
        [SerializeField] private UnityEngine.UI.Button _backToLogin;
        [SerializeField] private UnityEngine.UI.Toggle _rememberMe;
        public bool RememberMe => _rememberMe.isOn;
        public event Action<bool> PageRequested;
        public event Action<int, string, string, string, bool> Submitted;

        private void Awake()
        {
            _login.onClick.AddListener(Login);
            _register.onClick.AddListener(Register);
            _status.richText = false;
            _registrationStatus.richText = false;
            _openRegistration.onClick.AddListener(OpenRegistration);
            _backToLogin.onClick.AddListener(BackToLogin);
        }

        public void Configure(string[] servers, int defaultIndex)
        {
            _server.ClearOptions();
            _server.AddOptions(new List<string>(servers));
            _server.SetValueWithoutNotify(defaultIndex);
            _server.RefreshShownValue();
            _registrationServer.ClearOptions();
            _registrationServer.AddOptions(new List<string>(servers));
            _registrationServer.SetValueWithoutNotify(defaultIndex);
            _registrationServer.RefreshShownValue();
            ShowPage(false);
        }

        public void ShowPage(bool registration)
        {
            if (registration)
            {
                _registrationEmail.text = _email.text;
                _registrationServer.SetValueWithoutNotify(_server.value);
                _registrationServer.RefreshShownValue();
            }
            else if (_registrationPage.activeSelf)
            {
                _email.text = _registrationEmail.text;
                _server.SetValueWithoutNotify(_registrationServer.value);
                _server.RefreshShownValue();
            }
            ClearPassword();
            _loginPage.SetActive(!registration);
            _registrationPage.SetActive(registration);
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
            _registrationStatus.text = status;
            _registrationEmail.interactable = !busy;
            _registrationPassword.interactable = !busy;
            _registrationServer.interactable = !busy;
            _openRegistration.interactable = !busy;
            _backToLogin.interactable = !busy;
            _rememberMe.interactable = !busy;
        }

        public void ClearPassword()
        {
            _password.text = string.Empty;
            _registrationPassword.text = string.Empty;
        }
        private void OpenRegistration() => PageRequested?.Invoke(true);
        private void BackToLogin() => PageRequested?.Invoke(false);
        private void Login() => Submitted?.Invoke(_server.value, _email.text, _password.text, _username.text, false);
        private void Register() => Submitted?.Invoke(
            _registrationServer.value, _registrationEmail.text, _registrationPassword.text, _username.text, true);
        private void OnDestroy()
        {
            _login.onClick.RemoveListener(Login);
            _register.onClick.RemoveListener(Register);
            _openRegistration.onClick.RemoveListener(OpenRegistration);
            _backToLogin.onClick.RemoveListener(BackToLogin);
        }
    }
}
