using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abyssbound.Loot.SetDrops
{
    public static class SetDropRuntime
    {
        private sealed class BossPityState
        {
            public int killsSinceLastSetDrop;
        }

        // Per-session runtime pity state keyed by setId.
        private static readonly Dictionary<string, BossPityState> s_bossPity = new(StringComparer.OrdinalIgnoreCase);

        public static List<ItemInstance> TryRollExtraSetDrops(
            LootTableSO table,
            int itemLevel,
            int? seed,
            string rarityIdFallback,
            bool logPity)
        {
            var results = new List<ItemInstance>(1);

            if (!Zone1AbyssalInitiateSetDrops.IsZone1Table(table))
                return results;

            var config = Zone1AbyssalInitiateSetDrops.GetConfig();
            if (config == null || !config.HasPieces)
                return results;

            var tier = Zone1AbyssalInitiateSetDrops.ResolveTierFromTable(table);

            // Only support AbyssalInitiate for Zone1 for now; easy to extend later.
            if (!string.IsNullOrWhiteSpace(config.setId) &&
                !string.Equals(config.setId, Zone1AbyssalInitiateSetDrops.SetId, StringComparison.OrdinalIgnoreCase))
                return results;

            var pieces = config.GetValidPieces();
            if (pieces.Count == 0)
                return results;

            // Use deterministic RNG when seed is provided.
            // Derive a stable per-system seed so we don't mirror the main loot selection RNG exactly.
            const int salt = unchecked((int)0x5E7D0F);
            var rng = seed.HasValue
                ? new System.Random(seed.Value ^ salt ^ (int)tier ^ Mathf.Max(1, itemLevel))
                : null;

            float chance01 = Mathf.Clamp01(config.GetRollChancePercent(tier) / 100f);
            int rollsOnHit = config.GetPiecesToRollOnHit(tier);

            bool isBoss = tier == LootTier.Boss;
            bool forcedByPity = false;

            BossPityState pity = null;
            if (isBoss && config.bossPityEnabled)
            {
                if (!s_bossPity.TryGetValue(config.setId ?? Zone1AbyssalInitiateSetDrops.SetId, out pity) || pity == null)
                {
                    pity = new BossPityState();
                    s_bossPity[config.setId ?? Zone1AbyssalInitiateSetDrops.SetId] = pity;
                }

                pity.killsSinceLastSetDrop++;
            }

            bool hit = Next01(rng) < chance01;

            if (!hit && isBoss && config.bossPityEnabled && config.bossPityGuaranteeOnePiece)
            {
                int threshold = Mathf.Max(1, config.bossPityThresholdKills);
                if (pity != null && pity.killsSinceLastSetDrop >= threshold)
                {
                    hit = true;
                    forcedByPity = true;
                    rollsOnHit = 1;
                }
            }

            if (!hit)
                return results;

            var registry = LootRegistryRuntime.GetOrCreate();
            registry.BuildIfNeeded();

            string rarityId = !string.IsNullOrWhiteSpace(rarityIdFallback) ? rarityIdFallback : "Common";
            if (!registry.TryGetRarity(rarityId, out var rarity) || rarity == null)
            {
                // Last-resort fallback if registry is missing rarities.
                registry.TryGetRarity("Common", out rarity);
            }

            for (int i = 0; i < rollsOnHit; i++)
            {
                var baseItem = pieces[NextIndex(pieces.Count, rng)];
                if (baseItem == null || string.IsNullOrWhiteSpace(baseItem.id))
                    continue;

                // Ensure the base item is known to the runtime registry for UI resolution.
                try { registry.RegisterOrUpdateItem(baseItem); } catch { }

                ItemInstance inst;
                try
                {
                    if (rarity != null)
                        inst = LootRollerV2.RollItem(baseItem, rarity, itemLevel: Mathf.Max(1, itemLevel), seed: DeriveSeed(seed, i));
                    else
                        inst = new ItemInstance { baseItemId = baseItem.id, rarityId = rarityId, itemLevel = Mathf.Max(1, itemLevel), baseScalar = 1f };
                }
                catch
                {
                    inst = new ItemInstance { baseItemId = baseItem.id, rarityId = rarityId, itemLevel = Mathf.Max(1, itemLevel), baseScalar = 1f };
                }

                if (inst != null)
                    results.Add(inst);
            }

            if (isBoss && config.bossPityEnabled && pity != null)
            {
                pity.killsSinceLastSetDrop = 0;
                if (forcedByPity && logPity)
                    Debug.Log($"[SetDropPity] Triggered for setId='{config.setId}' thresholdKills={Mathf.Max(1, config.bossPityThresholdKills)}");
            }

            return results;
        }

        private static float Next01(System.Random rng)
        {
            if (rng == null) return UnityEngine.Random.value;
            return (float)rng.NextDouble();
        }

        private static int NextIndex(int count, System.Random rng)
        {
            if (count <= 1) return 0;
            if (rng == null) return UnityEngine.Random.Range(0, count);
            return rng.Next(0, count);
        }

        private static int? DeriveSeed(int? seed, int salt)
        {
            if (!seed.HasValue) return null;
            unchecked
            {
                return seed.Value ^ (0x2C1B3C6D + (salt * 997));
            }
        }
    }
}
