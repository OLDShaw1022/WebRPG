using System;
using UnityEngine;

namespace WebRPG
{
    // 呈現獨立中央選單、設定提示與返回確認，並轉送按鈕事件。
    public sealed class AdventureMenuView : MonoBehaviour, IAdventureMenuView
    {
        [SerializeField] private RectTransform _overlay;
        [SerializeField] private RectTransform _menu;
        [SerializeField] private RectTransform _settings;
        [SerializeField] private RectTransform _confirmation;
        [SerializeField] private AdventureButtonViewReferences _resume;
        [SerializeField] private AdventureButtonViewReferences _abandon;
        [SerializeField] private AdventureButtonViewReferences _restart;
        [SerializeField] private AdventureButtonViewReferences _openSettings;
        [SerializeField] private AdventureButtonViewReferences _return;
        [SerializeField] private AdventureButtonViewReferences _confirm;
        [SerializeField] private AdventureButtonViewReferences _cancel;
        [SerializeField] private AdventureButtonViewReferences _back;

        public event Action ResumeRequested;
        public event Action AbandonRequested;
        public event Action RestartRequested;
        public event Action SettingsRequested;
        public event Action ReturnRequested;
        public event Action ConfirmReturnRequested;
        public event Action BackRequested;

        private void Awake()
        {
            AdventureUiHelper.BindClick(_resume, () => ResumeRequested?.Invoke());
            AdventureUiHelper.BindClick(_abandon, () => AbandonRequested?.Invoke());
            AdventureUiHelper.BindClick(_restart, () => RestartRequested?.Invoke());
            AdventureUiHelper.BindClick(_openSettings, () => SettingsRequested?.Invoke());
            AdventureUiHelper.BindClick(_return, RequestReturn);
            AdventureUiHelper.BindClick(_confirm, () => ConfirmReturnRequested?.Invoke());
            AdventureUiHelper.BindClick(_cancel, () => BackRequested?.Invoke());
            AdventureUiHelper.BindClick(_back, () => BackRequested?.Invoke());
        }

        public void RequestReturn()
        {
            ReturnRequested?.Invoke();
        }

        public void SetRunState(bool canAbandon, bool canRestart)
        {
            _abandon.Button.interactable = canAbandon;
            _restart.Rect.gameObject.SetActive(canRestart);
        }

        public void Render(AdventureMenuScreen screen)
        {
            if (screen != AdventureMenuScreen.Hidden)
            {
                _overlay.SetAsLastSibling();
            }
            _overlay.gameObject.SetActive(screen != AdventureMenuScreen.Hidden);
            _menu.gameObject.SetActive(screen == AdventureMenuScreen.Menu);
            _settings.gameObject.SetActive(screen == AdventureMenuScreen.Settings);
            _confirmation.gameObject.SetActive(screen == AdventureMenuScreen.ConfirmReturn);
            if (screen == AdventureMenuScreen.Menu)
            {
                AdventureMenuNavigationHelper.Link(
                    _resume.Button, _abandon.Button, _openSettings.Button, _return.Button, _restart.Button);
                _resume.Button.Select();
            }
            else if (screen == AdventureMenuScreen.Settings)
            {
                AdventureMenuNavigationHelper.Link(_back.Button);
                _back.Button.Select();
            }
            else if (screen == AdventureMenuScreen.ConfirmReturn)
            {
                AdventureMenuNavigationHelper.Link(_cancel.Button, _confirm.Button);
                _cancel.Button.Select();
            }
        }
    }
}
