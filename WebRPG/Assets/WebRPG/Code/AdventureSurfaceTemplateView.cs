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
