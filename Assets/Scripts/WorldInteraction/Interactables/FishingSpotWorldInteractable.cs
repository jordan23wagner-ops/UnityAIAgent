using Abyssbound.Skills.Fishing;
using Abyssbound.Skills.Gathering;
using UnityEngine;

namespace Abyssbound.WorldInteraction
{
    [DisallowMultipleComponent]
    public sealed class FishingSpotWorldInteractable : WorldInteractable
    {
        [Header("Fishing")]
        [SerializeField] private string spotType = "Shrimp Spot";

        [Header("Wiring")]
        [SerializeField] private FishingSpot spot;
        [SerializeField] private FishingSpotInteractable sharedGating;

        public string SpotType => spotType;

        private void Reset()
        {
            SetDisplayName("Fishing");
            SetRequiresRange(true);
            SetInteractionRange(3f);

            TryAutoWire();
        }

        private void Awake()
        {
            TryAutoWire();
        }

        private void OnEnable()
        {
            TryAutoWire();
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(spotType))
                spotType = "Fishing Spot";

            TryAutoWire();
        }

        public void SetSpotType(string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                spotType = value;
        }

        private void TryAutoWire()
        {
            if (spot == null)
            {
                try { spot = GetComponent<FishingSpot>(); }
                catch { spot = null; }
            }

            if (sharedGating == null)
            {
                try { sharedGating = GetComponentInParent<FishingSpotInteractable>(); }
                catch { sharedGating = null; }
            }

            // Keep highlight consistent with the shared world-interaction wrapper.
            try
            {
                if (sharedGating != null)
                    SetHighlightRenderers(sharedGating.HighlightRenderers);
            }
            catch { }

            // Mirror range settings from the shared wrapper if present.
            try
            {
                if (sharedGating != null)
                {
                    SetRequiresRange(sharedGating.RequiresRange);
                    SetInteractionRange(sharedGating.InteractionRange);
                }
            }
            catch { }
        }

        public override string GetHoverText()
        {
            var t = string.IsNullOrWhiteSpace(spotType) ? "Fishing Spot" : spotType;
            return $"Fish: {t}";
        }

        public override bool CanInteract(GameObject interactor, out string reason)
        {
            // Delegate tool gating and any other fishing-specific checks to the shared wrapper.
            if (sharedGating != null)
                return sharedGating.CanInteract(interactor, out reason);

            return base.CanInteract(interactor, out reason);
        }

        public override void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor, out _))
                return;

            if (spot == null)
            {
                Debug.LogWarning($"[Fishing] Missing FishingSpot on {name}", this);
                return;
            }

            var controller = interactor != null ? interactor.GetComponentInParent<GatheringSkillController>() : null;
            if (controller == null)
                controller = GatheringSkillController.GetOrAttachToPlayer();

            if (controller == null)
            {
                Debug.LogWarning("[Fishing] Missing GatheringSkillController.", this);
                return;
            }

            controller.StartGathering(spot);
        }
    }
}
