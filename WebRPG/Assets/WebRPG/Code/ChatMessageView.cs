using TMPro;
using UnityEngine;

namespace WebRPG
{
    public sealed class ChatMessageView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _author;
        [SerializeField] private TMP_Text _messageText;

        public void SetMessage(string author, string message)
        {
            _author.text = author;
            _messageText.text = message;
            _messageText.richText = false;
        }
    }
}
