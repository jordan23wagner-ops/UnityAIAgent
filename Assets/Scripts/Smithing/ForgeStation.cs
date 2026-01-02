using UnityEngine;
using Abyssbound.WorldInteraction;

namespace Abyssbound.Smithing
{
    public sealed class ForgeStation : WorldInteractable
    {
        [Header("Forge")]
        [SerializeField] private float interactRange = 3.0f;

        private void Reset()
        {
            SetDisplayName("Forge");
        }

        public override bool CanInteract(Vector3 interactorPos)
        {
            float d = Vector3.Distance(transform.position, interactorPos);
            return d <= interactRange;
        }

        public override void Interact(GameObject interactor)
        {
            Debug.Log("[Abyssbound] Forge opened (placeholder)");
        }
    }
}
