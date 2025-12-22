using UnityEngine;

namespace Abyssbound.Loot
{
    [CreateAssetMenu(menuName = "Abyssbound/Loot/Rarity Definition", fileName = "Rarity_")]
    public sealed class RarityDefinitionSO : ScriptableObject
    {
        public string id;
        public string displayName;

        [Min(0)] public int sortOrder = 0;

        public bool enabledByDefault = true;

        [Header("Rolls")]
        [Min(0)] public int affixMin = 0;
        [Min(0)] public int affixMax = 0;

        [Header("Scalar (multiplies base stats)")]
        [Min(0f)] public float scalarMin = 1f;
        [Min(0f)] public float scalarMax = 1f;

        [Header("Optional")]
        public bool isSpecial = false;

        public override string ToString()
        {
            if (!string.IsNullOrWhiteSpace(displayName)) return displayName;
            if (!string.IsNullOrWhiteSpace(id)) return id;
            return name;
        }
    }
}
