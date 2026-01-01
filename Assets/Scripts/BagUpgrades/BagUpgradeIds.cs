using System;
using Abyssbound.Loot;

namespace Abyssbound.BagUpgrades
{
    public static class BagUpgradeIds
    {
        public const string BagUpgradeT1 = "bag_upgrade_t1";
        public const string BagUpgradeT2 = "bag_upgrade_t2";
        public const string BagUpgradeT3 = "bag_upgrade_t3";
        public const string BagUpgradeT4 = "bag_upgrade_t4";
        public const string BagUpgradeT5 = "bag_upgrade_t5";

        public static string GetIdForTier(int tier)
        {
            return tier switch
            {
                1 => BagUpgradeT1,
                2 => BagUpgradeT2,
                3 => BagUpgradeT3,
                4 => BagUpgradeT4,
                5 => BagUpgradeT5,
                _ => null
            };
        }

        public static bool IsBagUpgradeBaseId(string baseItemId)
        {
            if (string.IsNullOrWhiteSpace(baseItemId))
                return false;

            return string.Equals(baseItemId, BagUpgradeT1, StringComparison.OrdinalIgnoreCase)
                || string.Equals(baseItemId, BagUpgradeT2, StringComparison.OrdinalIgnoreCase)
                || string.Equals(baseItemId, BagUpgradeT3, StringComparison.OrdinalIgnoreCase)
                || string.Equals(baseItemId, BagUpgradeT4, StringComparison.OrdinalIgnoreCase)
                || string.Equals(baseItemId, BagUpgradeT5, StringComparison.OrdinalIgnoreCase);
        }

        public static bool TryResolveTierFromInventoryId(string inventoryItemId, LootRegistryRuntime registry, out int tier, out string consumeItemId, out string resolvedBaseItemId)
        {
            tier = 0;
            consumeItemId = inventoryItemId;
            resolvedBaseItemId = inventoryItemId;

            if (string.IsNullOrWhiteSpace(inventoryItemId))
                return false;

            // If this is a rolled instance id, resolve its base item id.
            if (inventoryItemId.StartsWith("ri_", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    if (registry != null && registry.TryGetRolledInstance(inventoryItemId, out var inst) && inst != null)
                    {
                        resolvedBaseItemId = inst.baseItemId;
                        consumeItemId = inventoryItemId;
                    }
                }
                catch { }
            }

            if (string.IsNullOrWhiteSpace(resolvedBaseItemId))
                return false;

            if (string.Equals(resolvedBaseItemId, BagUpgradeT1, StringComparison.OrdinalIgnoreCase)) { tier = 1; return true; }
            if (string.Equals(resolvedBaseItemId, BagUpgradeT2, StringComparison.OrdinalIgnoreCase)) { tier = 2; return true; }
            if (string.Equals(resolvedBaseItemId, BagUpgradeT3, StringComparison.OrdinalIgnoreCase)) { tier = 3; return true; }
            if (string.Equals(resolvedBaseItemId, BagUpgradeT4, StringComparison.OrdinalIgnoreCase)) { tier = 4; return true; }
            if (string.Equals(resolvedBaseItemId, BagUpgradeT5, StringComparison.OrdinalIgnoreCase)) { tier = 5; return true; }

            return false;
        }
    }
}
