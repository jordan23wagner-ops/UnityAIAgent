using UnityEngine;

namespace Abyssbound.WorldInteraction
{
    [DisallowMultipleComponent]
    public sealed class ForgeInteractable : WorldInteractable
    {
        private void Reset()
        {
            SetDisplayName("Forge");
            SetRequiresRange(true);
            SetInteractionRange(3f);

            try
            {
                var existing = HighlightRenderers;
                if (existing == null || existing.Length == 0)
                {
                    var rs = GetComponentsInChildren<Renderer>(includeInactive: true);
                    SetHighlightRenderers(rs);
                }
            }
            catch { }
        }

        public override string GetHoverText()
        {
            return "Forge";
        }

        public override bool CanInteract(GameObject interactor, out string reason)
        {
            if (!base.CanInteract(interactor, out reason))
            {
                WorldInteractionFeedback.LogBlocked(reason, "use Forge", this);
                return false;
            }

            reason = null;
            return true;
        }

        public override void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor, out _))
                return;

            Debug.Log("[Forge] Open forge UI (placeholder)", this);
        }

        // Compatibility: older systems may use SendMessage("Interact")
        public void Interact()
        {
            Interact(null);
        }
    }
}
