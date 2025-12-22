using System;
using UnityEngine;

namespace Abyss.Loot
{
    [CreateAssetMenu(menuName = "Abyss/Loot/Affix Roll Rules", fileName = "AffixRollRules")]
    public sealed class AffixRollRules : ScriptableObject
    {
        [Serializable]
        public struct RangeInt
        {
            [Min(0)] public int min;
            [Min(0)] public int max;

            public int ClampPick(System.Random rng = null)
            {
                int a = Mathf.Max(0, min);
                int b = Mathf.Max(a, max);
                if (a == b) return a;
                return rng != null ? rng.Next(a, b + 1) : UnityEngine.Random.Range(a, b + 1);
            }
        }

        [Header("Affix Count by Rarity")]
        public RangeInt common = new RangeInt { min = 0, max = 0 };
        public RangeInt uncommon = new RangeInt { min = 0, max = 1 };
        public RangeInt magic = new RangeInt { min = 1, max = 2 };
        public RangeInt rare = new RangeInt { min = 2, max = 3 };
        public RangeInt epic = new RangeInt { min = 3, max = 4 };
        public RangeInt legendary = new RangeInt { min = 4, max = 5 };
        public RangeInt set = new RangeInt { min = 4, max = 6 };
        public RangeInt radiant = new RangeInt { min = 5, max = 7 };

        public int GetAffixCount(Abyss.Items.ItemRarity rarity, System.Random rng = null)
        {
            return rarity switch
            {
                Abyss.Items.ItemRarity.Common => common.ClampPick(rng),
                Abyss.Items.ItemRarity.Uncommon => uncommon.ClampPick(rng),
                Abyss.Items.ItemRarity.Magic => magic.ClampPick(rng),
                Abyss.Items.ItemRarity.Rare => rare.ClampPick(rng),
                Abyss.Items.ItemRarity.Epic => epic.ClampPick(rng),
                Abyss.Items.ItemRarity.Legendary => legendary.ClampPick(rng),
                Abyss.Items.ItemRarity.Set => set.ClampPick(rng),
                Abyss.Items.ItemRarity.Radiant => radiant.ClampPick(rng),
                _ => magic.ClampPick(rng)
            };
        }
    }
}
