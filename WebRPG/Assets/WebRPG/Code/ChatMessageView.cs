using System;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace WebRPG
{
    public sealed class ChatMessageView : MonoBehaviour
    {
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
            _time.text = displayTime.ToString("HH:mm", CultureInfo.InvariantCulture);
        }
    }
}
