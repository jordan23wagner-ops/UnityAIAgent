using UnityEngine;

namespace Abyss.Items
{
    public enum ItemRarity
    {
        // NOTE: We keep the original numeric values for the existing tiers (0-4)
        // and migrate assets via an editor tool to the new ordering that adds Magic.
        // See Tools/Abyss/Items/Migrate Item Rarities (Add Magic Tier).
        Common = 0,
        Uncommon = 1,

        // New tier inserted between Uncommon and Rare.
        Magic = 2,
        Rare = 3,
        Epic = 4,
        Legendary = 5,
        Set = 6,
        Radiant = 7,
    }
}
