using System;
using Newtonsoft.Json;
using UnityEngine;

namespace WebRPG
{
    // 建立主選單與冒險系統，管理遊戲啟動、更新與結束流程。
    [DisallowMultipleComponent]
    public sealed class Main : MonoBehaviour
    {
        private const string MissingMainMenuPrefabMessage = "Main requires a MainMenuView prefab reference.";
        private const string MissingAdventureReferencesMessage = "Main requires adventure prefab and configuration references.";
        private const string AdventureStartFailureMessage = "無法開始冒險，請檢查遊戲設定後重試。";
        private const string WebGlQuitMessage = "請關閉瀏覽器分頁以離開遊戲。";

        [SerializeField]
        private MainMenuView _mainMenuViewPrefab;

        [SerializeField]
        private AdventureView _adventureViewPrefab;

        [SerializeField]
        private TextAsset _adventureConfiguration;

        private AdventureView _adventureView;
        private AdventurePresenter _adventurePresenter;

        private MainMenuView _mainMenuView;
        private MainMenuPresenter _mainMenuPresenter;

        private void Start()
        {
            if (!_mainMenuViewPrefab)
            {
                Debug.LogError(MissingMainMenuPrefabMessage, this);
                enabled = false;
                return;
            }

            _mainMenuView = Instantiate(_mainMenuViewPrefab);
            _mainMenuPresenter = new MainMenuPresenter(
                new MainMenuModel(), _mainMenuView, StartAdventure, QuitGame);
        }

        private void OnApplicationQuit()
        {
            DisposeAdventure();
            _mainMenuPresenter?.Dispose();
        }

        private void OnDestroy()
        {
            DisposeAdventure();
            _mainMenuPresenter?.Dispose();
            _mainMenuPresenter = null;

            if (_adventureView)
            {
                Destroy(_adventureView.gameObject);
                _adventureView = null;
            }

            if (_mainMenuView)
            {
                Destroy(_mainMenuView.gameObject);
                _mainMenuView = null;
            }
        }

        private void Update()
        {
            _adventurePresenter?.Tick(Time.deltaTime);
        }

        private bool StartAdventure()
        {
            if (_adventurePresenter != null)
            {
                return false;
            }

            try
            {
                if (!_adventureViewPrefab || !_adventureConfiguration)
                {
                    throw new InvalidOperationException(MissingAdventureReferencesMessage);
                }

                AdventureConfiguration configuration =
                    JsonConvert.DeserializeObject<AdventureConfiguration>(_adventureConfiguration.text);
                AdventureModel model = new AdventureModel(configuration, Environment.TickCount);
                model.StartRun();
                _adventureView = Instantiate(_adventureViewPrefab);
                _adventurePresenter = new AdventurePresenter(model, _adventureView);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                DisposeAdventure();
                if (_adventureView)
                {
                    Destroy(_adventureView.gameObject);
                    _adventureView = null;
                }
                _mainMenuView.ShowMessage(AdventureStartFailureMessage);
                return false;
            }
        }

        private void DisposeAdventure()
        {
            _adventurePresenter?.Dispose();
            _adventurePresenter = null;
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
            _mainMenuView.ShowMessage(WebGlQuitMessage);
#else
            Application.Quit();
#endif
        }
    }
}
