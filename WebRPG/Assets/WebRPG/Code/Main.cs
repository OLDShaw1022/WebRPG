using UnityEngine;

namespace WebRPG
{
    [DisallowMultipleComponent]
    public sealed class Main : MonoBehaviour
    {
        [SerializeField]
        private ChatModel _chatModel;

        [SerializeField]
        private ChatView _chatView;

        private ChatPresenter _chatPresenter;

        private void Start()
        {
            if (_chatModel == null || _chatView == null)
            {
                Debug.LogError("Main requires ChatModel and ChatView references.", this);
                enabled = false;
                return;
            }

            _chatPresenter = new ChatPresenter(_chatModel, _chatView);
        }

        private void OnDestroy()
        {
            _chatPresenter?.Dispose();
            _chatPresenter = null;
        }
    }
}
