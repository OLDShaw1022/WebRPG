using UnityEngine;

namespace WebRPG
{
    // 將標準 Image 綁定到圖示對照表；沒有圖片時保留方塊佔位。
    public sealed class AdventureIconView : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Image _image;
        [SerializeField] private AdventureIconSpriteCatalog _catalog;

        public void SetIcon(string id)
        {
            _image.sprite = _catalog.GetSprite(id);
        }
    }
}
