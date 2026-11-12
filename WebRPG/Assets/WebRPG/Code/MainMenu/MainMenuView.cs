using System;
using TMPro;
using UnityEngine;

namespace WebRPG
{
    // 顯示主選單提示，並轉送單人遊戲與離開按鈕事件。
    [DisallowMultipleComponent]
    public sealed class MainMenuView : MonoBehaviour, IMainMenuView
    {
        private const string MissingViewReferencesMessage = "MainMenuView requires button and message references.";

        [SerializeField]
        private UnityEngine.UI.Button _singlePlayerButton;

        [SerializeField]
        private UnityEngine.UI.Button _multiplayerButton;
        [SerializeField]
        private UnityEngine.UI.Button _logoutButton;
        [SerializeField]
        private GameObject _menu;
        [SerializeField]
        private UnityEngine.UI.Button _shopButton;
        [SerializeField]
        private ShopView _shop;
        [SerializeField]
        private UnityEngine.UI.Button _quitButton;

        [SerializeField]
        private TextMeshProUGUI _message;

        public ShopView Shop => _shop;

        public event Action SinglePlayerRequested;
        public event Action MultiplayerRequested;
        public event Action ShopRequested;
        public event Action LogoutRequested;
        public event Action QuitRequested;

        private void Awake()
        {
            if (!_singlePlayerButton || !_multiplayerButton || !_quitButton || !_message)
            {
                throw new InvalidOperationException(MissingViewReferencesMessage);
            }

            _singlePlayerButton.onClick.AddListener(HandleSinglePlayerRequested);
            _multiplayerButton.onClick.AddListener(HandleMultiplayerRequested);
            _quitButton.onClick.AddListener(HandleQuitRequested);
            if (_shopButton) _shopButton.onClick.AddListener(HandleShopRequested);
            if (_logoutButton) _logoutButton.onClick.AddListener(HandleLogoutRequested);
        }

        private void OnDestroy()
        {
            if (_shopButton) _shopButton.onClick.RemoveListener(HandleShopRequested);
            if (_logoutButton) _logoutButton.onClick.RemoveListener(HandleLogoutRequested);
            if (_multiplayerButton) _multiplayerButton.onClick.RemoveListener(HandleMultiplayerRequested);
            if (_singlePlayerButton)
            {
                _singlePlayerButton.onClick.RemoveListener(HandleSinglePlayerRequested);
            }

            if (_quitButton)
            {
                _quitButton.onClick.RemoveListener(HandleQuitRequested);
            }
        }

        public void ShowShop(bool visible)
        {
            _menu.SetActive(!visible);
            _shop.gameObject.SetActive(visible);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void ShowMessage(string message)
        {
            _message.text = message;
        }

        private void HandleSinglePlayerRequested()
        {
            SinglePlayerRequested?.Invoke();
        }

        private void HandleMultiplayerRequested() => MultiplayerRequested?.Invoke();
        private void HandleShopRequested() => ShopRequested?.Invoke();
        private void HandleLogoutRequested() => LogoutRequested?.Invoke();

        private void HandleQuitRequested()
        {
            QuitRequested?.Invoke();
        }
    }
}
