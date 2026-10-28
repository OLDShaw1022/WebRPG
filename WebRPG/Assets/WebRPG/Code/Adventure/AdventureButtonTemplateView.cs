// 提供冒險介面動態按鈕範本的元件參照。
using UnityEngine;

namespace WebRPG
{
    [DisallowMultipleComponent]
    public sealed class AdventureButtonTemplateView : MonoBehaviour
    {
        [SerializeField]
        private AdventureButtonViewReferences _control;

        public AdventureButtonViewReferences Control => _control;
    }
}
