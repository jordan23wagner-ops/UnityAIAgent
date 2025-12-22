using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abyss.Loot
{
    [CreateAssetMenu(menuName = "Abyss/Loot/Affix Pool", fileName = "AffixPool")]
    public sealed class AffixPool : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public AffixDefinition affix;
            [Min(0f)] public float weight;
        }

        public List<Entry> entries = new();

        public AffixDefinition Roll(Abyss.Items.ItemDefinition baseDef, HashSet<AffixDefinition> alreadyUsed = null, System.Random rng = null)
        {
            if (entries == null || entries.Count == 0) return null;

            float total = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                var a = entries[i].affix;
                if (a == null) continue;
                if (!a.IsCompatible(baseDef)) continue;
                if (alreadyUsed != null && alreadyUsed.Contains(a)) continue;

                float w = entries[i].weight > 0f ? entries[i].weight : a.weight;
                if (w <= 0f) continue;
                total += w;
            }

            if (total <= 0f) return null;

            float r = (rng != null ? (float)rng.NextDouble() : UnityEngine.Random.value) * total;

            for (int i = 0; i < entries.Count; i++)
            {
                var a = entries[i].affix;
                if (a == null) continue;
                if (!a.IsCompatible(baseDef)) continue;
                if (alreadyUsed != null && alreadyUsed.Contains(a)) continue;

                float w = entries[i].weight > 0f ? entries[i].weight : a.weight;
                if (w <= 0f) continue;

                r -= w;
                if (r <= 0f)
                    return a;
            }

            return null;
        }
    }
}
