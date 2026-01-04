using UnityEngine;
using Abyssbound.Cooking;

namespace Abyssbound.WorldInteraction
{
    [DisallowMultipleComponent]
    public sealed class BonfireInteractable : WorldInteractable
    {
        private void Reset()
        {
            SetDisplayName("Bonfire");
            SetRequiresRange(true);
            SetInteractionRange(3f);

            // Prefer explicit highlight renderers if already set; otherwise auto-pick child renderers.
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
            return "Bonfire";
        }

        public override bool CanInteract(GameObject interactor, out string reason)
        {
            if (!base.CanInteract(interactor, out reason))
            {
                WorldInteractionFeedback.LogBlocked(reason, "rest at Bonfire", this);
                return false;
            }

            reason = null;
            return true;
        }

        public override void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor, out _))
                return;

            // Open Cooking UI if this bonfire has a CookingStation.
            try
            {
                var station = GetComponent<CookingStation>();
                if (station == null)
                    station = GetComponentInChildren<CookingStation>(includeInactive: true);

                if (station != null)
                {
                    station.Open();
                    return;
                }
            }
            catch { }

            Debug.Log("[Bonfire] Interact (no CookingStation found)", this);
        }
    }
}
