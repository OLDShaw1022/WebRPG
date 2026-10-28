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
