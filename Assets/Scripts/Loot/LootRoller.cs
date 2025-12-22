using System.Collections.Generic;
using UnityEngine;

namespace Abyss.Loot
{
    public static class LootRoller
    {
        public static LootItemInstance RollInstance(Abyss.Items.ItemDefinition baseDef, AffixPool affixPool, AffixRollRules rules, System.Random rng = null)
        {
            if (baseDef == null) return null;

            var inst = new LootItemInstance
            {
                baseDefinition = baseDef,
                rarity = SafeRarity(baseDef)
            };

            if (affixPool == null || rules == null)
                return inst;

            int count = 0;
            try { count = rules.GetAffixCount(inst.rarity, rng); } catch { count = 0; }
            if (count <= 0) return inst;

            var used = new HashSet<AffixDefinition>();

            for (int i = 0; i < count; i++)
            {
                var a = affixPool.Roll(baseDef, used, rng);
                if (a == null) break;

                int value = 0;
                try { value = a.RollValue(rng); } catch { value = 0; }

                inst.affixes.Add(new LootAffixRoll { affix = a, value = value });
                used.Add(a);
            }

            return inst;
        }

        private static Abyss.Items.ItemRarity SafeRarity(Abyss.Items.ItemDefinition def)
        {
            if (def == null) return Abyss.Items.ItemRarity.Common;
            try { return def.rarity; } catch { return Abyss.Items.ItemRarity.Common; }
        }
    }
}
