using UnityEngine;
using Abyssbound.Loot;

namespace Abyss.Inventory
{
    public static class InventoryRarityColors
    {
        public static Color GetColor(Abyss.Items.ItemRarity rarity)
        {
            rarity = Abyss.Items.ItemRarityVisuals.Normalize(rarity);

            // Single source of truth.
            return RarityColorMap.GetColorOrDefault(rarity, new Color(0.75f, 0.75f, 0.75f, 1f));
        }
    }
}
