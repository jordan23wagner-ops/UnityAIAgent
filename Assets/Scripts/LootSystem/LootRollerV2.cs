using System;
using System.Collections.Generic;
using Abyss.Items;
using UnityEngine;

namespace Abyssbound.Loot
{
    public static class LootRollerV2
    {
        public static ItemInstance RollItem(LootTableSO table, int itemLevel = 1, int? seed = null)
        {
            if (table == null) return null;

            var rng = seed.HasValue ? new System.Random(seed.Value) : null;

            var registry = LootRegistryRuntime.GetOrCreate();
            registry.BuildIfNeeded();

            var baseItem = RollWeighted(table.items, e => e.item, e => e.weight, rng);
            if (baseItem == null || string.IsNullOrWhiteSpace(baseItem.id))
                return null;

            var rarity = RollWeighted(table.rarities, e => e.rarity, e => e.weight, rng);
            if (rarity == null || string.IsNullOrWhiteSpace(rarity.id))
                return null;

            float scalar = RollRange(rarity.scalarMin, rarity.scalarMax, rng);

            int minAff = Mathf.Max(0, rarity.affixMin);
            int maxAff = Mathf.Max(minAff, rarity.affixMax);
            int affixCount = RollIntRange(minAff, maxAff, rng);

            var inst = new ItemInstance
            {
                baseItemId = baseItem.id,
                rarityId = rarity.id,
                itemLevel = Mathf.Max(1, itemLevel),
                baseScalar = scalar,
            };

            if (affixCount <= 0)
                return inst;

            var pool = BuildEligibleAffixPool(baseItem, table, registry);
            if (pool.Count == 0)
                return inst;

            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < affixCount; i++)
            {
                var affix = RollWeighted(pool, a => a, a => 1f, rng, used);
                if (affix == null) break;
                if (string.IsNullOrWhiteSpace(affix.id)) continue;
                if (!used.Add(affix.id)) continue;

                float value = RollRange(affix.minRoll, affix.maxRoll, rng);
                inst.affixes.Add(new AffixRoll { affixId = affix.id, value = value });
            }

            return inst;
        }

        private static List<AffixDefinitionSO> BuildEligibleAffixPool(ItemDefinitionSO baseItem, LootTableSO table, LootRegistryRuntime registry)
        {
            var pool = new List<AffixDefinitionSO>(64);

            List<AffixDefinitionSO> source = null;
            if (table != null && table.affixPoolOverride != null && table.affixPoolOverride.Count > 0)
                source = table.affixPoolOverride;

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

        private static void TryAddIfEligible(ItemDefinitionSO baseItem, AffixDefinitionSO affix, List<AffixDefinitionSO> pool)
        {
            if (baseItem == null || affix == null || pool == null) return;

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
