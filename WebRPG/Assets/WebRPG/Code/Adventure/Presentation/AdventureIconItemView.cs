using UnityEngine;

namespace WebRPG
{
    // 保存遺物圖示範本與提示目標，依資料更新顯示。
    public sealed class AdventureIconItemView : MonoBehaviour
    {
        [SerializeField] private RectTransform _rect;
        [SerializeField] private AdventureIconView _icon;
        [SerializeField] private TMPro.TextMeshProUGUI _name;
        [SerializeField] private AdventureTooltipTargetView _tooltip;
        public RectTransform Rect => _rect;

        public void Render(AdventureIconScreenModel model)
        {
            _icon.SetIcon(model.Id);
            _name.text = model.Title;
            _tooltip.SetContent(model.Title, model.Description, string.Empty);
        }
    }
}
