using System.Collections.Generic;
using Abyss.Items;
using UnityEngine;

namespace Abyssbound.Loot
{
    [CreateAssetMenu(menuName = "Abyssbound/Loot/Affix Definition", fileName = "Affix_")]
    public sealed class AffixDefinitionSO : ScriptableObject
    {
        public string id;
        public string displayName;

        [Header("Constraints")]
        public List<AffixTag> tags = new();
        public List<EquipmentSlot> allowedSlots = new();

        [Header("Roll")]
        public StatType stat;
        public float minRoll = 1f;
        public float maxRoll = 3f;
        public bool percent = false;

        public override string ToString()
        {
            if (!string.IsNullOrWhiteSpace(displayName)) return displayName;
            if (!string.IsNullOrWhiteSpace(id)) return id;
            return name;
        }
    }
}
