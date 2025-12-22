using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abyssbound.Loot
{
    [Serializable]
    public struct AffixRoll
    {
        public string affixId;
        public float value;
    }

    [Serializable]
    public sealed class ItemInstance
    {
        public string baseItemId;
        public string rarityId;
        public int itemLevel = 1;
        public float baseScalar = 1f;
        public List<AffixRoll> affixes = new();

        public List<StatMod> GetAllStatMods(LootRegistryRuntime registry)
        {
            var mods = new List<StatMod>(16);
            if (registry == null) return mods;

            if (!string.IsNullOrWhiteSpace(baseItemId) && registry.TryGetItem(baseItemId, out var baseItem) && baseItem != null)
            {
                try
                {
                    if (baseItem.baseStats != null)
                    {
                        for (int i = 0; i < baseItem.baseStats.Count; i++)
                        {
                            var m = baseItem.baseStats[i];
                            m.value *= Mathf.Max(0f, baseScalar);
                            mods.Add(m);
                        }
                    }
                }
                catch { }
            }

            if (affixes != null && affixes.Count > 0)
            {
                for (int i = 0; i < affixes.Count; i++)
                {
                    var roll = affixes[i];
                    if (string.IsNullOrWhiteSpace(roll.affixId)) continue;

                    if (!registry.TryGetAffix(roll.affixId, out var affixDef) || affixDef == null)
                        continue;

                    mods.Add(new StatMod
                    {
                        stat = affixDef.stat,
                        value = roll.value,
                        percent = affixDef.percent,
                    });
                }
            }

            return mods;
        }
    }
}
