using System;
using Newtonsoft.Json;
using UnityEngine;
using WebRPGShared;

namespace WebRPG
{
    // 建立主選單與冒險系統，管理遊戲啟動、更新與結束流程。
    [DisallowMultipleComponent]
    public sealed class Main : MonoBehaviour
    {
        private const string MissingMainMenuPrefabMessage = "Main requires a MainMenuView prefab reference.";
        private const string MissingAdventureReferencesMessage = "Main requires adventure prefab and configuration references.";
        private const string AdventureStartFailureMessage = "無法開始冒險，請檢查遊戲設定後重試。";
        private const string LobbyStartFailureMessage = "無法開啟房間大廳，請檢查設定後重試。";
        private const string MissingLobbyPrefabMessage = "Main requires a LobbyView prefab reference.";
        private const string MissingAccountPrefabMessage = "Main requires an AccountView prefab reference.";
        private const string LoggedInFormat = "已登入：{0}";
        private const string SessionExpiredMessage = "登入已到期，請重新登入。";
        [SerializeField] private AccountView _accountViewPrefab;
        private AccountView _accountView;
        private AccountModel _accountModel;
        private AccountPresenter _accountPresenter;
        private bool _loggingOut;

        [SerializeField] private LobbyView _lobbyViewPrefab;
        [SerializeField] private TextAsset _lobbyConfiguration;
        private LobbyView _lobbyView;
        private LobbyModel _lobbyModel;
        private LobbyPresenter _lobbyPresenter;
        private RoomChatPresenter _lobbyChatPresenter;
        private RoomChatPresenter _adventureChatPresenter;
        private MultiplayerAdventureSession _multiplayerAdventureSession;

        private const string WebGlQuitMessage = "請關閉瀏覽器分頁以離開遊戲。";

        [SerializeField]
        private MainMenuView _mainMenuViewPrefab;

        [SerializeField]
        private AdventureView _adventureViewPrefab;

        [SerializeField]
        private TextAsset _adventureConfiguration;

        private AdventureView _adventureView;
        private AdventurePresenter _adventurePresenter;
        private AdventureMenuPresenter _adventureMenuPresenter;

        private MainMenuView _mainMenuView;
        private MainMenuPresenter _mainMenuPresenter;

        private void Awake()
        {
            Application.runInBackground = true;
        }

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
                new MainMenuModel(), _mainMenuView, StartAdventure, QuitGame, OpenLobby);
            _mainMenuView.LogoutRequested += Logout;
            _mainMenuView.gameObject.SetActive(false);
            if (!_accountViewPrefab || !_lobbyConfiguration)
                throw new InvalidOperationException(MissingAccountPrefabMessage);
            LobbyClientConfiguration accountConfiguration =
                JsonConvert.DeserializeObject<LobbyClientConfiguration>(_lobbyConfiguration.text);
            _accountModel = new AccountModel(accountConfiguration, Application.isEditor);
            _accountView = Instantiate(_accountViewPrefab);
            _accountPresenter = new AccountPresenter(_accountModel, _accountView, ShowAuthenticatedMenu);
            _accountPresenter.RestoreAsync(_accountModel.DefaultServerIndex).Forget();
        }

        private void OnApplicationQuit()
        {
            DisposeLobby();
            DisposeAdventure();
            _mainMenuPresenter?.Dispose();
        }

        private void OnDestroy()
        {
            _accountPresenter?.Dispose();
            _accountModel?.Dispose();
            if (_accountView) Destroy(_accountView.gameObject);
            if (_mainMenuView) _mainMenuView.LogoutRequested -= Logout;
            DisposeLobby();
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
            if (_accountModel != null && !string.IsNullOrEmpty(_accountModel.AccessToken) && !_accountModel.IsLoggedIn)
                ShowLogin(SessionExpiredMessage);
            _adventurePresenter?.Tick(Time.deltaTime);
            _lobbyModel?.Tick(Time.unscaledDeltaTime);
        }

        private bool StartAdventure()
        {
            return StartAdventure(Environment.TickCount, false);
        }

        private bool StartAdventure(int seed, bool multiplayer)
        {
            if (_accountModel == null || !_accountModel.IsLoggedIn) return false;
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
                AdventureModel model;
                if (multiplayer)
                {
                    _multiplayerAdventureSession = new MultiplayerAdventureSession(_lobbyModel, seed, configuration);
                    model = _multiplayerAdventureSession.LocalModel;
                }
                else
                {
                    model = new AdventureModel(configuration, seed);
                    model.StartRun();
                }
                _adventureView = Instantiate(_adventureViewPrefab);
                if (multiplayer)
                {
                    _adventureView.EnableRoomChat();
                    _adventureChatPresenter = new RoomChatPresenter(_lobbyModel, _adventureView.RoomChat, model);
                }
                _adventurePresenter = new AdventurePresenter(model, _adventureView, _multiplayerAdventureSession);
                _adventureMenuPresenter = new AdventureMenuPresenter(new AdventureMenuModel(!multiplayer),
                    _adventureView.Menu, multiplayer ? _ => { } : _adventurePresenter.SetPaused, _adventureView.RequestAbandon,
                    _adventureView.RequestStart, ReturnToMainMenu);
                _adventureView.MenuRequested += _adventureMenuPresenter.Open;
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
            _adventureChatPresenter?.Dispose();
            _adventureChatPresenter = null;
            if (_adventureView && _adventureMenuPresenter != null)
            {
                _adventureView.MenuRequested -= _adventureMenuPresenter.Open;
            }
            _adventureMenuPresenter?.Dispose();
            _adventureMenuPresenter = null;
            _adventurePresenter?.Dispose();
            _adventurePresenter = null;
            _multiplayerAdventureSession?.Dispose();
            _multiplayerAdventureSession = null;
        }

        private void ReturnToMainMenu()
        {
            DisposeAdventure();
            if (_lobbyModel != null) DisposeLobby();
            if (_adventureView)
            {
                Destroy(_adventureView.gameObject);
                _adventureView = null;
            }
            _mainMenuPresenter?.Dispose();
            _mainMenuPresenter = new MainMenuPresenter(new MainMenuModel(), _mainMenuView, StartAdventure, QuitGame, OpenLobby);
            _mainMenuView.ShowMessage(string.Empty);
            _mainMenuView.gameObject.SetActive(true);
        }

        private bool OpenLobby()
        {
            if (_accountModel == null || !_accountModel.IsLoggedIn) return false;
            if (_lobbyPresenter != null || _adventurePresenter != null) return false;
            try
            {
                if (!_lobbyViewPrefab || !_lobbyConfiguration)
                    throw new InvalidOperationException(MissingLobbyPrefabMessage);
                LobbyClientConfiguration configuration =
                    JsonConvert.DeserializeObject<LobbyClientConfiguration>(_lobbyConfiguration.text);
                configuration.Validate();
                _lobbyView = Instantiate(_lobbyViewPrefab);
                _lobbyModel = new LobbyModel(configuration, Application.isEditor, _accountModel);
                _lobbyModel.AdventureReceived += HandleAdventurePacket;
                _lobbyPresenter = new LobbyPresenter(_lobbyModel, _lobbyView, ReturnFromLobby);
                _lobbyChatPresenter = new RoomChatPresenter(_lobbyModel, _lobbyView.RoomChat);
                _lobbyView.SetAccount(_accountModel.Username, _accountModel.ServerIndex);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                DisposeLobby();
                _mainMenuView.ShowMessage(LobbyStartFailureMessage);
                return false;
            }
        }

        private void HandleAdventurePacket(AdventurePacket packet)
        {
            if (packet?.Type != AdventurePacketType.Start || _adventurePresenter != null ||
                !int.TryParse(packet.Payload, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out int seed))
            {
                return;
            }

            if (_lobbyView) _lobbyView.gameObject.SetActive(false);
            if (!StartAdventure(seed, true) && _lobbyView) _lobbyView.gameObject.SetActive(true);
        }

        private void ReturnFromLobby()
        {
            DisposeLobby();
            _mainMenuView.gameObject.SetActive(true);
        }

        private void DisposeLobby()
        {
            _lobbyChatPresenter?.Dispose();
            _lobbyChatPresenter = null;
            if (_lobbyModel != null) _lobbyModel.AdventureReceived -= HandleAdventurePacket;
            _lobbyPresenter?.Dispose();
            _lobbyPresenter = null;
            if (_lobbyModel != null) _lobbyModel.DisposeAsync().Forget();
            _lobbyModel = null;
            if (_lobbyView) Destroy(_lobbyView.gameObject);
            _lobbyView = null;
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

        private void ShowAuthenticatedMenu()
        {
            _accountView.gameObject.SetActive(false);
            _mainMenuView.gameObject.SetActive(true);
            _mainMenuView.ShowMessage(string.Format(LoggedInFormat, _accountModel.Username));
        }

        private void Logout()
        {
            if (!_loggingOut) LogoutAsync().Forget();
        }

        private async Awaitable LogoutAsync()
        {
            _loggingOut = true;
            try
            {
                await _accountModel.LogoutAsync();
                if (this) ShowLogin(string.Empty);
            }
            catch (Exception exception)
            {
                if (_mainMenuView) _mainMenuView.ShowMessage(exception.Message);
            }
            finally { _loggingOut = false; }
        }

        private void ShowLogin(string message)
        {
            DisposeLobby();
            DisposeAdventure();
            if (_adventureView) Destroy(_adventureView.gameObject);
            _adventureView = null;
            _accountModel.ClearSession();
            _mainMenuPresenter?.Dispose();
            _mainMenuPresenter = new MainMenuPresenter(new MainMenuModel(), _mainMenuView, StartAdventure, QuitGame, OpenLobby);
            _mainMenuView.gameObject.SetActive(false);
            _accountView.gameObject.SetActive(true);
            _accountView.ClearPassword();
            _accountView.Render(false, message);
        }
    }
}
