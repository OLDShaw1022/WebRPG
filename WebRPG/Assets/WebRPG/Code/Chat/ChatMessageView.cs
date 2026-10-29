using System;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace WebRPG
{
    // 顯示單則聊天訊息的作者、內容與本地時間。
    public sealed class ChatMessageView : MonoBehaviour
    {
        private const string DisplayTimeFormat = "HH:mm";

        [SerializeField] private TMP_Text _author;
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private TMP_Text _time;

        public void SetMessage(string author, string message, DateTimeOffset timestampUtc)
        {
            _author.text = author;
            _messageText.text = message;
            _messageText.richText = false;
            DateTimeOffset displayTime = timestampUtc == default
                ? DateTimeOffset.Now
                : timestampUtc.ToLocalTime();
            _time.text = displayTime.ToString(DisplayTimeFormat, CultureInfo.InvariantCulture);
        }
    }
}
