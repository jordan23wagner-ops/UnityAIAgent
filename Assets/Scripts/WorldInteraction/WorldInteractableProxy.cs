using UnityEngine;

namespace Abyssbound.WorldInteraction
{
    [DisallowMultipleComponent]
    public sealed class WorldInteractableProxy : WorldInteractable
    {
        [SerializeField] private WorldInteractable target;

        public void SetTarget(WorldInteractable value)
        {
            target = value;
        }

        public override bool CanInteract(GameObject interactor, out string reason)
        {
            if (target == null)
            {
                reason = "Missing target";
                return false;
            }

            return target.CanInteract(interactor, out reason);
        }

        public override void Interact(GameObject interactor)
        {
            if (target == null)
                return;

            target.Interact(interactor);
        }

        public override Bounds GetHoverBounds()
        {
            if (target != null)
                return target.GetHoverBounds();

            return base.GetHoverBounds();
        }

        public override string GetHoverText()
        {
            if (target != null)
                return target.GetHoverText();

            return base.GetHoverText();
        }
    }
}
