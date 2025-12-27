using System;
using System.Collections.Generic;
using Abyss.Items;
using UnityEngine;

namespace Abyssbound.Loot
{
    public static class LootRollerV2
    {
        private const string Zone1PoolRoot = "Loot/AffixPools";

        public static ItemInstance RollItemWithZoneTuning(
            LootTableSO table,
            ZoneLootTuningSO tuning,
            Abyssbound.Loot.SetDrops.LootTier tier,
            int? seed = null,
            string itemLevelSource = null,
            bool logCreation = false)
        {
            if (table == null) return null;
            if (tuning == null)
                return RollItem(table, itemLevel: 1, seed: seed, itemLevelSource: itemLevelSource, logCreation: logCreation);

            var rng = seed.HasValue ? new System.Random(seed.Value) : null;

            var ilvlRange = tuning.GetItemLevelRange(tier);
            int ilvl = RollIntRange(ilvlRange.ClampMin(), ilvlRange.ClampMax(), rng);
            string src = string.IsNullOrWhiteSpace(itemLevelSource) ? "ZoneTuning" : itemLevelSource;

            return RollItem(table, itemLevel: ilvl, seed: seed, itemLevelSource: src, logCreation: logCreation, rarityWeightsOverride: tuning.GetRarityWeights(tier));
        }

        public static ItemInstance RollItem(
            LootTableSO table,
            int itemLevel = 1,
            int? seed = null,
            string itemLevelSource = null,
            bool logCreation = false,
            ZoneLootTuningSO.TierRarityWeights? rarityWeightsOverride = null)
        {
            if (table == null) return null;

            var rng = seed.HasValue ? new System.Random(seed.Value) : null;

            var registry = LootRegistryRuntime.GetOrCreate();
            registry.BuildIfNeeded();

            var baseItem = RollWeighted(table.items, e => e.item, e => e.weight, rng);
            if (baseItem == null || string.IsNullOrWhiteSpace(baseItem.id))
                return null;

            RarityDefinitionSO rarity;
            if (rarityWeightsOverride.HasValue)
            {
                var weights = rarityWeightsOverride.Value;
                // NOTE: WeightedRarityEntry is a struct (non-nullable) so it can never be null; treat "missing" as rarity == null.
                rarity = RollWeighted(table.rarities, e => e.rarity, e => weights.GetWeight(e.rarity != null ? e.rarity.id : null), rng);
                if (rarity == null)
                {
                    // Fallback: keep drops working even if the table is missing expected rarity ids.
                    rarity = RollWeighted(table.rarities, e => e.rarity, e => e.weight, rng);
                }
            }
            else
            {
                rarity = RollWeighted(table.rarities, e => e.rarity, e => e.weight, rng);
            }
            if (rarity == null || string.IsNullOrWhiteSpace(rarity.id))
                return null;

            float scalar = RollRange(rarity.scalarMin, rarity.scalarMax, rng);

            int affixCount = GetDeterministicAffixCountOrFallback(rarity, rng);

            var inst = new ItemInstance
            {
                baseItemId = baseItem.id,
                rarityId = rarity.id,
                itemLevel = Mathf.Max(1, itemLevel),
                baseScalar = scalar,
            };

            if (logCreation)
            {
                var name = string.IsNullOrWhiteSpace(baseItem.displayName) ? baseItem.id : baseItem.displayName;
                var src = string.IsNullOrWhiteSpace(itemLevelSource) ? "Default" : itemLevelSource;
                Debug.Log($"[Loot] Created {name} ilvl={inst.itemLevel} source={src}");
            }

            if (affixCount <= 0)
                return inst;

            var pool = BuildEligibleAffixPool(baseItem, table, registry);
            if (pool.Count == 0)
                return inst;

            var usedStats = new HashSet<StatType>();

            for (int i = 0; i < affixCount; i++)
            {
                var affix = RollWeightedAffix(pool, usedStats, rng);
                if (affix == null) break;
                if (!usedStats.Add(affix.stat)) continue;

                var (min, max) = GetTieredRollRange(affix, inst.itemLevel);
                float value = RollRange(min, max, rng);
                inst.affixes.Add(new AffixRoll { affixId = affix.id, value = value });
            }

            return inst;
        }

        public static ItemInstance RollItem(ItemDefinitionSO baseItem, RarityDefinitionSO rarity, int itemLevel = 1, int? seed = null)
        {
            if (baseItem == null || rarity == null) return null;

            var rng = seed.HasValue ? new System.Random(seed.Value) : null;

            var registry = LootRegistryRuntime.GetOrCreate();
            registry.BuildIfNeeded();

            float scalar = RollRange(rarity.scalarMin, rarity.scalarMax, rng);
            int affixCount = GetDeterministicAffixCountOrFallback(rarity, rng);

            var inst = new ItemInstance
            {
                baseItemId = baseItem.id,
                rarityId = rarity.id,
                itemLevel = Mathf.Max(1, itemLevel),
                baseScalar = scalar,
            };

            if (affixCount <= 0)
                return inst;

            // No table context here, so fall back to registry-wide affixes (still filtered by slot/tags).
            var pool = BuildEligibleAffixPool(baseItem, table: null, registry);
            if (pool.Count == 0)
                return inst;

            var usedStats = new HashSet<StatType>();
            for (int i = 0; i < affixCount; i++)
            {
                var affix = RollWeightedAffix(pool, usedStats, rng);
                if (affix == null) break;
                if (!usedStats.Add(affix.stat)) continue;

                var (min, max) = GetTieredRollRange(affix, inst.itemLevel);
                float value = RollRange(min, max, rng);
                inst.affixes.Add(new AffixRoll { affixId = affix.id, value = value });
            }

            return inst;
        }

        private static List<AffixDefinitionSO> BuildEligibleAffixPool(ItemDefinitionSO baseItem, LootTableSO table, LootRegistryRuntime registry)
        {
            var pool = new List<AffixDefinitionSO>(64);

            List<AffixDefinitionSO> source = null;

            // Highest priority: explicit per-table override list.
            if (table != null && table.affixPoolOverride != null && table.affixPoolOverride.Count > 0)
                source = table.affixPoolOverride;

            // Next: zone pools (currently only Zone1).
            if (source == null)
                source = TryGetZoneAffixPool(baseItem, table);

            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                    TryAddIfEligible(baseItem, source[i], pool);
            }
            else
            {
                foreach (var kv in registry.GetAllAffixes())
                    TryAddIfEligible(baseItem, kv.Value, pool);
            }

            return pool;
        }

        private static List<AffixDefinitionSO> TryGetZoneAffixPool(ItemDefinitionSO baseItem, LootTableSO table)
        {
            if (baseItem == null || table == null) return null;

            // Simple zone detection from table id/name.
            string tableId = string.IsNullOrWhiteSpace(table.id) ? table.name : table.id;
            if (string.IsNullOrWhiteSpace(tableId)) return null;

            bool isZone1 = tableId.StartsWith("Zone1", StringComparison.OrdinalIgnoreCase);
            if (!isZone1) return null;

            string poolName = null;
            var tags = baseItem.allowedAffixTags;

            if (HasTag(tags, AffixTag.WeaponMelee)) poolName = "Zone1_WeaponAffixes_Melee";
            else if (HasTag(tags, AffixTag.WeaponRanged)) poolName = "Zone1_WeaponAffixes_Ranged";
            else if (HasTag(tags, AffixTag.WeaponMagic)) poolName = "Zone1_WeaponAffixes_Magic";
            else if (HasTag(tags, AffixTag.Armor)) poolName = "Zone1_ArmorAffixes";
            else if (HasTag(tags, AffixTag.Jewelry)) poolName = "Zone1_JewelryAffixes";

            if (string.IsNullOrWhiteSpace(poolName)) return null;

            AffixPoolSO pool = null;
            try { pool = Resources.Load<AffixPoolSO>($"{Zone1PoolRoot}/{poolName}"); } catch { pool = null; }
            if (pool == null || pool.affixes == null || pool.affixes.Count == 0) return null;
            return pool.affixes;
        }

        private static bool HasTag(List<AffixTag> tags, AffixTag tag)
        {
            if (tags == null || tags.Count == 0) return false;
            for (int i = 0; i < tags.Count; i++)
                if (tags[i] == tag) return true;
            return false;
        }

        private static int GetDeterministicAffixCountOrFallback(RarityDefinitionSO rarity, System.Random rng)
        {
            if (rarity == null) return 0;
            string id = rarity.id ?? string.Empty;

            // Deterministic mapping (no guessing):
            // Common: 0, Uncommon: 0, Magic: 1, Rare: 2, Epic: 3, Legendary: 4
            if (id.Equals("Common", StringComparison.OrdinalIgnoreCase)) return 0;
            if (id.Equals("Uncommon", StringComparison.OrdinalIgnoreCase)) return 0;
            if (id.Equals("Magic", StringComparison.OrdinalIgnoreCase)) return 1;
            if (id.Equals("Rare", StringComparison.OrdinalIgnoreCase)) return 2;
            if (id.Equals("Epic", StringComparison.OrdinalIgnoreCase)) return 3;
            if (id.Equals("Legendary", StringComparison.OrdinalIgnoreCase)) return 4;

            // For other rarities (Set/Unique/Mythic/Radiant placeholders), fall back to the existing authored values.
            int minAff = Mathf.Max(0, rarity.affixMin);
            int maxAff = Mathf.Max(minAff, rarity.affixMax);
            return RollIntRange(minAff, maxAff, rng);
        }

        private static AffixDefinitionSO RollWeightedAffix(List<AffixDefinitionSO> pool, HashSet<StatType> usedStats, System.Random rng)
        {
            if (pool == null || pool.Count == 0) return null;

            float total = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                var a = pool[i];
                if (a == null) continue;
                if (a.weight <= 0) continue;
                if (usedStats != null && usedStats.Contains(a.stat)) continue;
                total += Mathf.Max(0f, a.weight);
            }
            if (total <= 0f) return null;

            float r = Next01(rng) * total;
            for (int i = 0; i < pool.Count; i++)
            {
                var a = pool[i];
                if (a == null) continue;
                if (a.weight <= 0) continue;
                if (usedStats != null && usedStats.Contains(a.stat)) continue;

                r -= Mathf.Max(0f, a.weight);
                if (r <= 0f)
                    return a;
            }

            return null;
        }

        private static void TryAddIfEligible(ItemDefinitionSO baseItem, AffixDefinitionSO affix, List<AffixDefinitionSO> pool)
        {
            if (baseItem == null || affix == null || pool == null) return;

            // Disabled affixes should never roll.
            if (affix.weight <= 0) return;

            // Slot constraint (if specified)
            if (affix.allowedSlots != null && affix.allowedSlots.Count > 0)
            {
                bool slotOk = false;
                for (int i = 0; i < affix.allowedSlots.Count; i++)
                {
                    if (baseItem.slot == affix.allowedSlots[i]) { slotOk = true; break; }
                }
                if (!slotOk) return;
            }

            // Tag intersection constraint
            if (!HasTagIntersection(baseItem.allowedAffixTags, affix.tags))
                return;

            pool.Add(affix);
        }

        private static bool HasTagIntersection(List<AffixTag> itemTags, List<AffixTag> affixTags)
        {
            if (affixTags == null || affixTags.Count == 0) return true;
            if (itemTags == null || itemTags.Count == 0) return true;

            for (int i = 0; i < itemTags.Count; i++)
            {
                var t = itemTags[i];
                if (t == AffixTag.Any) return true;

                for (int j = 0; j < affixTags.Count; j++)
                {
                    if (affixTags[j] == AffixTag.Any) return true;
                    if (affixTags[j] == t) return true;
                }
            }

            return false;
        }

        private static float Next01(System.Random rng)
        {
            return rng != null ? (float)rng.NextDouble() : UnityEngine.Random.value;
        }

        private static float RollRange(float a, float b, System.Random rng)
        {
            float min = Mathf.Min(a, b);
            float max = Mathf.Max(a, b);
            return Mathf.Lerp(min, max, Next01(rng));
        }

        private static (float min, float max) GetTieredRollRange(AffixDefinitionSO affix, int itemLevel)
        {
            if (affix == null)
                return (0f, 0f);

            int lvl = Mathf.Max(1, itemLevel);

            if (affix.tiers == null || affix.tiers.Count == 0)
                return (affix.minRoll, affix.maxRoll);

            bool found = false;
            AffixDefinitionSO.AffixTier best = default;
            int bestWidth = int.MaxValue;

            for (int i = 0; i < affix.tiers.Count; i++)
            {
                var t = affix.tiers[i];

                int minLvl = Mathf.Max(1, t.minItemLevel);
                int maxLvl = Mathf.Max(1, t.maxItemLevel);
                if (maxLvl < minLvl)
                {
                    var tmp = minLvl;
                    minLvl = maxLvl;
                    maxLvl = tmp;
                }

                if (lvl < minLvl || lvl > maxLvl)
                    continue;

                int width = maxLvl - minLvl;
                if (!found || width < bestWidth || (width == bestWidth && minLvl > best.minItemLevel))
                {
                    best = t;
                    bestWidth = width;
                    found = true;
                }
            }

            if (!found)
                return (affix.minRoll, affix.maxRoll);

            return (best.minRoll, best.maxRoll);
        }

        private static int RollIntRange(int min, int max, System.Random rng)
        {
            if (max <= min) return min;
            return rng != null ? rng.Next(min, max + 1) : UnityEngine.Random.Range(min, max + 1);
        }

        private static T RollWeighted<T, TEntry>(
            List<TEntry> entries,
            Func<TEntry, T> getValue,
            Func<TEntry, float> getWeight,
            System.Random rng,
            HashSet<string> excludeById = null)
            where T : class
        {
            if (entries == null || entries.Count == 0) return null;

            float total = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                var v = getValue(entries[i]);
                if (v == null) continue;

                if (excludeById != null)
                {
                    var idProp = v.GetType().GetField("id");
                    if (idProp != null)
                    {
                        var id = idProp.GetValue(v) as string;
                        if (!string.IsNullOrWhiteSpace(id) && excludeById.Contains(id))
                            continue;
                    }
                }

                float w = Mathf.Max(0f, getWeight(entries[i]));
                if (w <= 0f) continue;
                total += w;
            }

            if (total <= 0f) return null;

            float r = Next01(rng) * total;
            for (int i = 0; i < entries.Count; i++)
            {
                var v = getValue(entries[i]);
                if (v == null) continue;

                if (excludeById != null)
                {
                    var idProp = v.GetType().GetField("id");
                    if (idProp != null)
                    {
                        var id = idProp.GetValue(v) as string;
                        if (!string.IsNullOrWhiteSpace(id) && excludeById.Contains(id))
                            continue;
                    }
                }

                float w = Mathf.Max(0f, getWeight(entries[i]));
                if (w <= 0f) continue;

                r -= w;
                if (r <= 0f)
                    return v;
            }

            return null;
        }
    }
}
