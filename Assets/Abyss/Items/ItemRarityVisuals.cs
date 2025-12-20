using UnityEngine;

namespace Abyss.Items
{
    public static class ItemRarityVisuals
    {
        public static ItemRarity Normalize(ItemRarity rarity)
        {
            // Treat any invalid value as Common.
            int v = (int)rarity;
            return v is >= (int)ItemRarity.Common and <= (int)ItemRarity.Radiant
                ? rarity
                : ItemRarity.Common;
        }

        public static string ToDisplayString(ItemRarity rarity)
        {
            rarity = Normalize(rarity);
            return rarity.ToString();
        }

        public static Color GetColor(ItemRarity rarity)
        {
            rarity = Normalize(rarity);

            // Conservative, readable tints.
            return rarity switch
            {
                ItemRarity.Common => new Color(0.92f, 0.92f, 0.92f, 1f),
                ItemRarity.Uncommon => new Color(0.35f, 0.85f, 0.45f, 1f),
                ItemRarity.Magic => new Color(0.35f, 0.55f, 0.95f, 1f),
                ItemRarity.Rare => new Color(0.98f, 0.92f, 0.25f, 1f),
                ItemRarity.Epic => new Color(0.70f, 0.40f, 0.95f, 1f),
                ItemRarity.Legendary => new Color(0.98f, 0.72f, 0.25f, 1f),
                ItemRarity.Set => new Color(0.95f, 0.25f, 0.25f, 1f),
                ItemRarity.Radiant => new Color(0.35f, 0.95f, 0.95f, 1f),
                _ => new Color(0.92f, 0.92f, 0.92f, 1f),
            };
        }
    }
}
