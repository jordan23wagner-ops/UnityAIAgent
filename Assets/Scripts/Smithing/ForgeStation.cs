using UnityEngine;
using Abyssbound.WorldInteraction;
using Game.Systems;
using Abyssbound.Skilling;
using Abyssbound.Skills;

namespace Abyssbound.Smithing
{
    public sealed class ForgeStation : WorldInteractable
    {
        private void Reset()
        {
            SetDisplayName("Forge");
            SetRequiresRange(true);
            SetInteractionRange(3f);
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

        public override string GetHoverText()
        {
            return "Forge";
        }

        public override void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor, out _))
                return;

            var inv = PlayerInventoryResolver.GetOrFind();
            if (inv == null)
            {
                Debug.LogWarning("[Forge] No PlayerInventory found");
                return;
            }

            int oreCount = inv.Count(SkillingItemIds.CopperOre);
            if (oreCount < 3)
            {
                Debug.Log($"[Forge] Need 3x Copper Ore (you have {oreCount})");
                return;
            }

            if (!inv.TryConsume(SkillingItemIds.CopperOre, 3))
            {
                Debug.LogWarning("[Forge] Failed to consume ore (unexpected)");
                return;
            }

            inv.Add(SkillingItemIds.CopperBar, 1);
            Debug.Log("[Forge] Smelted 1x Copper Bar (used 3x Copper Ore)");

            var skills = PlayerSkills.FindOrCreateOnPlayer();
            if (skills != null)
                skills.AddXp(SkillType.Smithing, 15, source: "Smelting");
        }
    }
}
