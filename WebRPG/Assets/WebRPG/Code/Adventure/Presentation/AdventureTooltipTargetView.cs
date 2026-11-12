using UnityEngine;
using UnityEngine.EventSystems;

namespace WebRPG
{
    // 保存圖示說明並將懸停與鍵盤焦點轉送至共用提示窗。
    public sealed class AdventureTooltipTargetView : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private RectTransform _rect;
        [SerializeField] private AdventureTooltipView _tooltip;
        private string _title = string.Empty;
        private string _description = string.Empty;
        private string _status = string.Empty;

        public void SetContent(string title, string description, string status)
        {
            _title = title;
            _description = description;
            _status = status;
            _tooltip.Refresh(this, title, description, status);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _tooltip.Show(this, _rect, _title, _description, _status, eventData);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            _tooltip.MovePointer(this, eventData);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _tooltip.Hide(this);
        }

        public void OnSelect(BaseEventData eventData)
        {
            _tooltip.Show(this, _rect, _title, _description, _status);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _tooltip.Hide(this);
        }

        private void OnDisable()
        {
            if (_tooltip)
            {
                _tooltip.Hide(this);
            }
        }
    }
}
