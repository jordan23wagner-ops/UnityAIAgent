using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abyss.Loot
{
    public enum AffixStat
    {
        DamageBonus,
        MaxHealthBonus,
        DamageReductionFlat,
    }

    [CreateAssetMenu(menuName = "Abyss/Loot/Affix Definition", fileName = "Affix_")]
    public sealed class AffixDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string affixId;
        public string displayName;

        [Header("Stat")]
        public AffixStat stat = AffixStat.DamageBonus;
        public int minValue = 1;
        public int maxValue = 3;

        [Header("Compatibility (optional)")]
        public List<Abyss.Items.ItemType> allowedItemTypes = new();
        public List<Abyss.Items.EquipmentSlot> allowedSlots = new();

        [Header("Weight")]
        [Min(0f)] public float weight = 1f;

        public bool IsCompatible(Abyss.Items.ItemDefinition def)
        {
            if (def == null) return false;

            try
            {
                if (allowedItemTypes != null && allowedItemTypes.Count > 0)
                {
                    bool ok = false;
                    for (int i = 0; i < allowedItemTypes.Count; i++)
                    {
                        if (Equals(def.itemType, allowedItemTypes[i])) { ok = true; break; }
                    }
                    if (!ok) return false;
                }
            }
            catch { }

            try
            {
                if (allowedSlots != null && allowedSlots.Count > 0)
                {
                    bool ok = false;
                    for (int i = 0; i < allowedSlots.Count; i++)
                    {
                        if (Equals(def.equipmentSlot, allowedSlots[i])) { ok = true; break; }
                    }
                    if (!ok) return false;
                }
            }
            catch { }

            return true;
        }

        public int RollValue(System.Random rng = null)
        {
            int min = Mathf.Min(minValue, maxValue);
            int max = Mathf.Max(minValue, maxValue);
            if (min == max) return min;

            // Inclusive for designer friendliness.
            return rng != null ? rng.Next(min, max + 1) : UnityEngine.Random.Range(min, max + 1);
        }

        public override string ToString()
        {
            if (!string.IsNullOrWhiteSpace(displayName)) return displayName;
            if (!string.IsNullOrWhiteSpace(affixId)) return affixId;
            return name;
        }
    }
}
