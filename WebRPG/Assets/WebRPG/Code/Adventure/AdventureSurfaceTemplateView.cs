// 提供冒險介面動態面板範本的元件參照。
using UnityEngine;

namespace WebRPG
{
    [DisallowMultipleComponent]
    public sealed class AdventureSurfaceTemplateView : MonoBehaviour
    {
        [SerializeField]
        private AdventureSurfaceViewReferences _control;

        public AdventureSurfaceViewReferences Control => _control;
    }
}
