using UnityEngine;

namespace Abyss.Inventory
{
    public static class InventoryRarityColors
    {
        public static Color GetColor(Abyss.Items.ItemRarity rarity)
        {
            // Centralized mapping for inventory UI (strip + any future highlights).
            // Keep this as the single source of truth to avoid scattered hardcoded colors.
            rarity = Abyss.Items.ItemRarityVisuals.Normalize(rarity);

            return rarity switch
            {
                Abyss.Items.ItemRarity.Common => new Color(0.75f, 0.75f, 0.75f, 1f),
                Abyss.Items.ItemRarity.Uncommon => new Color(0.35f, 0.85f, 0.35f, 1f),
                Abyss.Items.ItemRarity.Magic => new Color(0.35f, 0.65f, 1.00f, 1f),
                Abyss.Items.ItemRarity.Rare => new Color(0.30f, 0.55f, 1.00f, 1f),
                Abyss.Items.ItemRarity.Epic => new Color(0.78f, 0.35f, 0.95f, 1f),
                Abyss.Items.ItemRarity.Legendary => new Color(1.00f, 0.65f, 0.15f, 1f),
                Abyss.Items.ItemRarity.Set => new Color(1.00f, 0.40f, 0.40f, 1f),
                Abyss.Items.ItemRarity.Radiant => new Color(1.00f, 0.95f, 0.45f, 1f),
                _ => new Color(0.75f, 0.75f, 0.75f, 1f),
            };
        }
    }
}
