using System;
using Abyssbound.BagUpgrades;
using Abyssbound.Loot;
using Abyssbound.Progression;
using Abyss.Inventory;
using Game.Systems;
using UnityEngine;

namespace Abyssbound.Items.Use
{
    public static class BagUpgradeUseHandler
    {
        private static bool s_warnedAtCap;

        public static bool TryUseFromInventory(string inventoryItemId)
        {
            var inv = PlayerInventoryResolver.GetOrFind();
            if (inv == null)
                return false;

            var reg = LootRegistryRuntime.GetOrCreate();
            if (!BagUpgradeIds.TryResolveTierFromInventoryId(inventoryItemId, reg, out int tier, out string consumeId, out _))
                return false;

            var prog = PlayerProgression.GetOrCreate();
            if (prog == null)
                return false;

            // One-time unlock per tier: do not allow repeat uses of the same tier.
            try
            {
                if (prog.HasAppliedBagUpgradeTier(tier))
                {
                    Debug.LogWarning($"[BagUpgrade] Tier {tier} already applied.");
                    return false;
                }
            }
            catch { }

            int before = prog != null ? prog.MaxInventorySlots : PlayerProgression.DefaultMaxInventorySlots;

            int delta = tier switch
            {
                1 => 2,
                2 => 2,
                3 => 2,
                4 => 4,
                5 => 4,
                _ => 0
            };

            int after = Mathf.Clamp(before + delta, PlayerProgression.DefaultMaxInventorySlots, PlayerProgression.MaxInventorySlotsCap);

            if (after == before)
            {
                if (!s_warnedAtCap)
                {
                    s_warnedAtCap = true;
                    Debug.LogWarning($"[BagUpgrade] Inventory slots already at cap ({PlayerProgression.MaxInventorySlotsCap}).");
                }
                return false;
            }

            // Ensure the item exists before applying (we consume after applying per UX ordering).
            try
            {
                if (!inv.Has(consumeId, 1))
                    return false;
            }
            catch
            {
                try
                {
                    if (inv.Count(consumeId) < 1)
                        return false;
                }
                catch { return false; }
            }

            // Apply + persist (fires PlayerProgression.OnInventoryCapacityChanged on success).
            if (!prog.ApplyBagUpgrade(tier))
                return false;

            Debug.Log($"[BagUpgrade] Applied tier {tier}: maxSlots {before}->{prog.MaxInventorySlots}");

            // Consume AFTER capacity has been applied.
            if (!inv.TryConsume(consumeId, 1))
                return false;

            // Notify UI listeners.
            try { ItemUseRouter.NotifyItemUsed(BagUpgradeIds.GetIdForTier(tier) ?? inventoryItemId); } catch { }

            return true;
        }

        public static bool CanHandle(string inventoryItemId)
        {
            if (string.IsNullOrWhiteSpace(inventoryItemId))
                return false;

            var reg = LootRegistryRuntime.GetOrCreate();
            return BagUpgradeIds.TryResolveTierFromInventoryId(inventoryItemId, reg, out _, out _, out _);
        }
    }
}
