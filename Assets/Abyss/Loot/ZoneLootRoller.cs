using System;
using System.Collections.Generic;
using Abyss.Items;
using UnityEngine;

using AbyssItemRarity = Abyss.Items.ItemRarity;

namespace Abyss.Loot
{
    public static class ZoneLootRoller
    {
        public static List<ItemDefinition> RollZone(ZoneLootTable table, EnemyTier tier, System.Random rng = null, Action<string> logError = null)
        {
            var results = new List<ItemDefinition>();
            if (table == null) return results;

            int rolls = Mathf.Max(0, table.GetZoneRollsForTier(tier));
            var chances = table.GetChancesForTier(tier);

            RollFromPool(table.zonePool, rolls, chances, results, rng);

            int matRolls = Mathf.Max(0, table.GetMaterialsRollsForTier(tier));
            if (matRolls > 0)
                RollFromPool(table.materialsPool, matRolls, chances, results, rng);

            return results;
        }

        public static void ApplyBossOverrides(ZoneLootTable table, List<ItemDefinition> results, System.Random rng = null, Action<string> logError = null)
        {
            if (table == null || results == null) return;
            if (!table.enableBossOverrides) return;

            int zoneRolls = Mathf.Max(0, table.bossZoneRolls);
            int matsRolls = table.rollMaterials ? Mathf.Max(0, table.bossMaterialsRolls) : 0;
            int uniqueRolls = Mathf.Max(0, table.bossUniqueRolls);

            // Boss chances: use miniBossChances as a reasonable default.
            var chances = table.miniBossChances;

            RollFromPool(table.zonePool, zoneRolls, chances, results, rng);

            if (matsRolls > 0)
                RollFromPool(table.materialsPool, matsRolls, chances, results, rng);

            if (uniqueRolls > 0)
                RollFromPoolNoRarity(table.bossUniquesPool, uniqueRolls, results, rng);

            ApplyGuaranteedMinRarity(table, results, rng, logError);
        }

        private static void ApplyGuaranteedMinRarity(ZoneLootTable table, List<ItemDefinition> results, System.Random rng, Action<string> logError)
        {
            if (table == null || results == null) return;

            bool HasEligibleAlready()
            {
                foreach (var item in results)
                {
                    if (item == null) continue;
                    if ((int)item.rarity >= (int)table.bossGuaranteedMinRarity)
                        return true;
                }
                return false;
            }

            if (HasEligibleAlready()) return;

            // Build eligible pool from zonePool.
            var eligible = new List<ItemDefinition>();
            foreach (var item in table.zonePool)
            {
                if (item == null) continue;
                if ((int)item.rarity >= (int)table.bossGuaranteedMinRarity)
                    eligible.Add(item);
            }

            if (eligible.Count == 0)
            {
                logError?.Invoke($"[ZoneLootRoller] Boss guaranteed min rarity enabled but no eligible items in zonePool for minRarity={table.bossGuaranteedMinRarity} (table='{table.name}').");
                return;
            }

            int attempts = Mathf.Max(1, table.bossGuaranteedAttempts);
            for (int i = 0; i < attempts; i++)
            {
                if (HasEligibleAlready()) return;

                int idx = NextIndex(eligible.Count, rng);
                var picked = eligible[idx];
                if (picked != null)
                    results.Add(picked);
            }
        }

        private static void RollFromPool(
            List<ItemDefinition> pool,
            int rolls,
            ZoneLootTable.RarityChances chances,
            List<ItemDefinition> results,
            System.Random rng)
        {
            if (pool == null || results == null) return;
            if (rolls <= 0) return;

            // Build pools by rarity for fast selection.
            var byRarity = new Dictionary<AbyssItemRarity, List<ItemDefinition>>();
            foreach (AbyssItemRarity r in Enum.GetValues(typeof(AbyssItemRarity)))
                byRarity[r] = new List<ItemDefinition>();

            foreach (var item in pool)
            {
                if (item == null) continue;
                if (!byRarity.TryGetValue(item.rarity, out var list))
                {
                    list = new List<ItemDefinition>();
                    byRarity[item.rarity] = list;
                }
                list.Add(item);
            }

            for (int i = 0; i < rolls; i++)
            {
                var rolledRarity = RollRarity(chances, rng);
                if (rolledRarity == null) continue;

                var rarity = rolledRarity.Value;
                if (byRarity.TryGetValue(rarity, out var list) && list.Count > 0)
                {
                    results.Add(list[NextIndex(list.Count, rng)]);
                    continue;
                }

                // Fallback: if no items exist for that rarity, pick any item from pool.
                if (pool.Count > 0)
                    results.Add(pool[NextIndex(pool.Count, rng)]);
            }
        }

        private static void RollFromPoolNoRarity(List<ItemDefinition> pool, int rolls, List<ItemDefinition> results, System.Random rng)
        {
            if (pool == null || results == null) return;
            if (rolls <= 0) return;
            if (pool.Count == 0) return;

            for (int i = 0; i < rolls; i++)
                results.Add(pool[NextIndex(pool.Count, rng)]);
        }

        private static AbyssItemRarity? RollRarity(ZoneLootTable.RarityChances chances, System.Random rng)
        {
            float total = Mathf.Max(0f, chances.Total);
            if (total <= 0f) return null;

            float r = Next01(rng) * total;

            if ((r -= chances.common) <= 0f) return AbyssItemRarity.Common;
            if ((r -= chances.uncommon) <= 0f) return AbyssItemRarity.Uncommon;
            if ((r -= chances.magic) <= 0f) return AbyssItemRarity.Magic;
            if ((r -= chances.rare) <= 0f) return AbyssItemRarity.Rare;
            if ((r -= chances.epic) <= 0f) return AbyssItemRarity.Epic;
            if ((r -= chances.legendary) <= 0f) return AbyssItemRarity.Legendary;
            if ((r -= chances.set) <= 0f) return AbyssItemRarity.Set;
            if ((r -= chances.radiant) <= 0f) return AbyssItemRarity.Radiant;

            return AbyssItemRarity.Common;
        }

        private static float Next01(System.Random rng)
        {
            return rng != null ? (float)rng.NextDouble() : UnityEngine.Random.value;
        }

        private static int NextIndex(int count, System.Random rng)
        {
            if (count <= 1) return 0;
            return rng != null ? rng.Next(0, count) : UnityEngine.Random.Range(0, count);
        }
    }
}
