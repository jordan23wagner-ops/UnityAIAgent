using System.Collections.Generic;
using Abyss.Items;
using UnityEngine;

namespace Abyssbound.Loot
{
    [CreateAssetMenu(menuName = "Abyssbound/Loot/Item Definition", fileName = "Item_")]
    public sealed class ItemDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public string id;
        public string displayName;
        [TextArea]
        public string description;
        public Sprite icon;

        [Header("Item")]
        public ItemType itemType = ItemType.Consumable;

        [Tooltip("If true, multiple copies should stack in one inventory slot when stored by base item id.")]
        public bool stackable = true;

        [Tooltip("If true, this item should not be sellable/tradeable.")]
        public bool untradeable;

        [Header("Equipment")]
        public EquipmentSlot slot = EquipmentSlot.None;

        [Tooltip("Optional: when set, this item is considered to occupy these slots (future multi-slot support). If empty, 'slot' is used.")]
        public List<EquipmentSlot> occupiesSlots = new();

        [Header("Stats")]
        public List<StatMod> baseStats = new();

        [Header("Affix Constraints")]
        public List<AffixTag> allowedAffixTags = new();

        [Header("Set (optional)")]
        public ItemSetDefinitionSO set;

        // Back-compat for older data / tools. Prefer using the 'set' reference.
        public string setId;

        public override string ToString()
        {
            if (!string.IsNullOrWhiteSpace(displayName)) return displayName;
            if (!string.IsNullOrWhiteSpace(id)) return id;
            return name;
        }
    }
}
