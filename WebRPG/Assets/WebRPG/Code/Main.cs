using UnityEngine;

namespace WebRPG
{
    [DisallowMultipleComponent]
    public sealed class Main : MonoBehaviour
    {
        [SerializeField]
        private ChatView _chatViewPrefab;

        private ChatView _chatView;
        private ChatModel _chatModel;
        private ChatPresenter _chatPresenter;

        private void Start()
        {
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
            DisposeChat();
        }

        private void OnDestroy()
        {
            DisposeChat();

            if (_chatView)
            {
                Destroy(_chatView.gameObject);
                _chatView = null;
            }
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
