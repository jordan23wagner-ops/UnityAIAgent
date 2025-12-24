using System;
using UnityEngine;

namespace Abyssbound.Loot
{
    /// <summary>
    /// Shared rarity -> color mapping for UI.
    /// Keep this as the single source of truth so tooltip/details/slot visuals match.
    /// </summary>
    public static class RarityColorMap
    {
        // NOTE: Values intentionally match the inventory strip palette so UI is consistent.
        // Tuned to be more readable in the inventory UI (slightly darker / more saturated).
        // Rare is intentionally yellow-ish (distinct from Magic blue).
        private static readonly Color Common = new(0.62f, 0.62f, 0.62f, 1f);
        private static readonly Color Uncommon = new(0.20f, 0.70f, 0.25f, 1f);
        private static readonly Color Magic = new(0.22f, 0.52f, 0.95f, 1f);
        private static readonly Color Rare = new(0.85f, 0.75f, 0.18f, 1f);
        private static readonly Color Epic = new(0.66f, 0.28f, 0.85f, 1f);
        private static readonly Color Legendary = new(0.95f, 0.55f, 0.10f, 1f);
        private static readonly Color Set = new(0.90f, 0.30f, 0.30f, 1f);
        private static readonly Color Radiant = new(0.90f, 0.85f, 0.30f, 1f);

        public static Color GetColorOrDefault(string rarityId, Color defaultColor)
        {
            if (string.IsNullOrWhiteSpace(rarityId))
                return defaultColor;

            // Normalize to canonical ids.
            var key = rarityId.Trim();
            if (key.Equals("Common", StringComparison.OrdinalIgnoreCase)) return Common;
            if (key.Equals("Uncommon", StringComparison.OrdinalIgnoreCase)) return Uncommon;
            if (key.Equals("Magic", StringComparison.OrdinalIgnoreCase)) return Magic;
            if (key.Equals("Rare", StringComparison.OrdinalIgnoreCase)) return Rare;
            if (key.Equals("Epic", StringComparison.OrdinalIgnoreCase)) return Epic;
            if (key.Equals("Legendary", StringComparison.OrdinalIgnoreCase)) return Legendary;
            if (key.Equals("Set", StringComparison.OrdinalIgnoreCase)) return Set;
            if (key.Equals("Radiant", StringComparison.OrdinalIgnoreCase)) return Radiant;

            return defaultColor;
        }

        public static Color GetColorOrDefault(Abyss.Items.ItemRarity rarity, Color defaultColor)
        {
            // Map legacy enum to the same canonical ids.
            return rarity switch
            {
                Abyss.Items.ItemRarity.Common => Common,
                Abyss.Items.ItemRarity.Uncommon => Uncommon,
                Abyss.Items.ItemRarity.Magic => Magic,
                Abyss.Items.ItemRarity.Rare => Rare,
                Abyss.Items.ItemRarity.Epic => Epic,
                Abyss.Items.ItemRarity.Legendary => Legendary,
                Abyss.Items.ItemRarity.Set => Set,
                Abyss.Items.ItemRarity.Radiant => Radiant,
                _ => defaultColor,
            };
        }

        public static string ToHtmlRgb(Color c)
        {
            return ColorUtility.ToHtmlStringRGB(c);
        }
    }
}
