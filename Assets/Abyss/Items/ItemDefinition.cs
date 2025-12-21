using UnityEngine;
using UnityEngine.Serialization;

namespace Abyss.Items
{
    [CreateAssetMenu(menuName = "Abyss/Items/Item Definition")]
    public class ItemDefinition : ScriptableObject
    {
        public string itemId;
        public string displayName;

        [TextArea]
        [FormerlySerializedAs("_description")]
        [FormerlySerializedAs("Description")]
        [FormerlySerializedAs("desc")]
        [FormerlySerializedAs("itemDescription")]
        public string description;

        public Sprite icon;
        public ItemRarity rarity = ItemRarity.Common;
        public ItemType itemType;
        public int baseValue;

        [Header("Equipment (optional)")]
        public EquipmentSlot equipmentSlot = EquipmentSlot.None;
        public WeaponHandedness weaponHandedness = WeaponHandedness.None;

        [Tooltip("Bonus damage applied when this item is equipped as a weapon. Only meaningful for ItemType == Weapon.")]
        public int DamageBonus = 0;

        [Tooltip("Added to player's Max Health when equipped. Used for armor.")]
        public int MaxHealthBonus = 0;

        [Tooltip("Reduces incoming damage by this amount while equipped (flat per hit).")]
        public int DamageReductionFlat = 0;
    }
}
