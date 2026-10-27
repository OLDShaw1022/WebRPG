using System;
using Newtonsoft.Json;
using UnityEngine;

namespace WebRPG
{
    [DisallowMultipleComponent]
    public sealed class Main : MonoBehaviour
    {
        [SerializeField]
        private ChatView _chatViewPrefab;

        [SerializeField]
        private bool _startWithAdventure = true;

        [SerializeField]
        private AdventureView _adventureViewPrefab;

        [SerializeField]
        private TextAsset _adventureConfiguration;

        private AdventureView _adventureView;
        private AdventurePresenter _adventurePresenter;

        private ChatView _chatView;
        private ChatModel _chatModel;
        private ChatPresenter _chatPresenter;

        private void Start()
        {
            if (_startWithAdventure)
            {
                StartAdventure();
                return;
            }

            if (!_chatViewPrefab)
            {
                Debug.LogError("Main requires a ChatView prefab reference.", this);
                enabled = false;
                return;
            }

            _chatView = Instantiate(_chatViewPrefab);
            _chatModel = new ChatModel();
            _chatPresenter = new ChatPresenter(_chatModel, _chatView);
        }

        private void OnApplicationQuit()
        {
            DisposeAdventure();
            DisposeChat();
        }

        private void OnDestroy()
        {
            DisposeAdventure();
            DisposeChat();

            if (_adventureView)
            {
                Destroy(_adventureView.gameObject);
                _adventureView = null;
            }

            if (_chatView)
            {
                Destroy(_chatView.gameObject);
                _chatView = null;
            }
        }

        private void Update()
        {
            _adventurePresenter?.Tick(Time.deltaTime);
        }

        private void StartAdventure()
        {
            try
            {
                if (!_adventureViewPrefab || !_adventureConfiguration)
                {
                    throw new InvalidOperationException("Main requires adventure prefab and configuration references.");
                }

                AdventureConfiguration configuration =
                    JsonConvert.DeserializeObject<AdventureConfiguration>(_adventureConfiguration.text);
                AdventureModel model = new AdventureModel(configuration, Environment.TickCount);
                _adventureView = Instantiate(_adventureViewPrefab);
                _adventurePresenter = new AdventurePresenter(model, _adventureView);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                enabled = false;
            }
        }

        private void DisposeAdventure()
        {
            _adventurePresenter?.Dispose();
            _adventurePresenter = null;
        }

        private void DisposeChat()
        {
            _chatPresenter?.Dispose();
            _chatPresenter = null;

            if (_chatModel != null)
            {
                ChatModel chatModel = _chatModel;
                _chatModel = null;
                chatModel.DisposeAsync().Forget();
            }
        }
    }
}
