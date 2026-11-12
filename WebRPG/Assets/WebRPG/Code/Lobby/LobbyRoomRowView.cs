using System;
using TMPro;
using UnityEngine;
using WebRPGShared;

namespace WebRPG
{
    // 顯示一筆公開房間並轉送加入要求。
    public sealed class LobbyRoomRowView : MonoBehaviour
    {
        private const string RoomLabelFormat = "{0}　{1}/{2}　加入";
        [SerializeField]
        private UnityEngine.UI.Button _join;
        [SerializeField]
        private TextMeshProUGUI _label;
        private string _roomId;
        private Action<string> _selected;

        public void Bind(LobbyRoomSummary room, bool interactable, Action<string> selected)
        {
            _roomId = room.Id;
            _selected = selected;
            _label.richText = false;
            _label.text = string.Format(RoomLabelFormat, room.Name, room.MemberCount, room.Capacity);
            _join.interactable = interactable && room.MemberCount < room.Capacity;
            _join.onClick.RemoveListener(Select);
            _join.onClick.AddListener(Select);
        }

        private void Select() => _selected?.Invoke(_roomId);
        private void OnDestroy()
        {
            if (_join) _join.onClick.RemoveListener(Select);
        }
    }
}
