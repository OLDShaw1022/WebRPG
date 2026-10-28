using System;
using TMPro;
using UnityEngine;

namespace WebRPG
{
    [DisallowMultipleComponent]
    public sealed class MainMenuView : MonoBehaviour, IMainMenuView
    {
        private const string MissingViewReferencesMessage = "MainMenuView requires button and message references.";

        [SerializeField]
        private UnityEngine.UI.Button _singlePlayerButton;

        [SerializeField]
        private UnityEngine.UI.Button _quitButton;

        [SerializeField]
        private TextMeshProUGUI _message;

        public event Action SinglePlayerRequested;
        public event Action QuitRequested;

        private void Awake()
        {
            if (!_singlePlayerButton || !_quitButton || !_message)
            {
                throw new InvalidOperationException(MissingViewReferencesMessage);
            }

            _singlePlayerButton.onClick.AddListener(HandleSinglePlayerRequested);
            _quitButton.onClick.AddListener(HandleQuitRequested);
        }

        private void OnDestroy()
        {
            if (_singlePlayerButton)
            {
                _singlePlayerButton.onClick.RemoveListener(HandleSinglePlayerRequested);
            }

            if (_quitButton)
            {
                _quitButton.onClick.RemoveListener(HandleQuitRequested);
            }
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

        private void HandleQuitRequested()
        {
            QuitRequested?.Invoke();
        }
    }
}
