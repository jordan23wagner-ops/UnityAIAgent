using UnityEngine;

namespace Abyss.Legacy
{
    // Legacy. Do not use for new systems.
    // This exists only to preserve previously-created loot assets that referenced the former
    // global ItemDefinition type. New Abyss systems should use Abyss.Items.ItemDefinition.

    [CreateAssetMenu(menuName = "Abyss/Legacy/Legacy Item Definition", fileName = "NewLegacyItemDefinition")]
    public class LegacyItemDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string itemId;
        public string displayName;

        [Header("Classification")]
        public ItemType itemType = ItemType.None;
        public ItemRarity rarity = ItemRarity.Common;

        [Header("Visuals (optional)")]
        public Sprite icon;
    }
}
