using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace WebRPG
{
    // 跟隨滑鼠顯示圖示說明，保留鍵盤定位且不攔截游標事件。
    public sealed class AdventureTooltipView : MonoBehaviour
    {
        [SerializeField] private RectTransform _root;
        [SerializeField] private RectTransform _panel;
        [SerializeField] private TextMeshProUGUI _title;
        [SerializeField] private TextMeshProUGUI _description;
        [SerializeField] private TextMeshProUGUI _status;
        private AdventureTooltipTargetView _owner;
        private RectTransform _anchor;
        private PointerEventData _pointer;
        private Vector2 _preferredSize;
        private bool _hasPreferredSize;

        public void Show(AdventureTooltipTargetView owner, RectTransform anchor,
            string title, string description, string status, PointerEventData pointer = null)
        {
            if (!_hasPreferredSize)
            {
                _preferredSize = _panel.rect.size;
                _hasPreferredSize = true;
            }
            _owner = owner;
            _anchor = anchor;
            _pointer = pointer;
            _panel.gameObject.SetActive(true);
            Refresh(owner, title, description, status);
            UpdatePosition();
        }

        public void MovePointer(AdventureTooltipTargetView owner, PointerEventData pointer)
        {
            if (_owner != owner || _pointer == null || pointer == null)
            {
                return;
            }
            _pointer = pointer;
            UpdatePosition();
        }

        private void LateUpdate()
        {
            if (_owner && _panel.gameObject.activeSelf)
            {
                UpdatePosition();
            }
        }

        private void UpdatePosition()
        {
            if (_pointer != null)
            {
                AdventureTooltipLayoutHelper.PlaceAtPointer(_root, _panel, _pointer.position,
                    _pointer.enterEventCamera, _preferredSize);
            }
            else if (_anchor)
            {
                AdventureTooltipLayoutHelper.Place(_root, _panel, _anchor, _preferredSize);
            }
        }

        public void Refresh(AdventureTooltipTargetView owner, string title, string description, string status)
        {
            if (_owner != owner)
            {
                return;
            }
            _title.text = title;
            _description.text = description;
            _status.text = status;
        }

        public void Hide(AdventureTooltipTargetView owner)
        {
            if (_owner == owner)
            {
                HideAll();
            }
        }

        public void HideAll()
        {
            _owner = null;
            _anchor = null;
            _pointer = null;
            _panel.gameObject.SetActive(false);
        }
    }
}
