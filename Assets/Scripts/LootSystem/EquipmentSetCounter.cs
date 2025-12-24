using System.Collections.Generic;
using Abyss.Equipment;
using Abyss.Items;

namespace Abyssbound.Loot
{
    public static class EquipmentSetCounter
    {
        public static Dictionary<string, int> GetEquippedSetCounts(PlayerEquipment equipment, LootRegistryRuntime lootRegistry)
        {
            var counts = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            if (equipment == null || lootRegistry == null) return counts;

            // Count by slot, but treat "same id in both hands" (2H representation) as one piece.
            var equippedItemIds = new List<string>(16)
            {
                equipment.Get(EquipmentSlot.Helm),
                equipment.Get(EquipmentSlot.Chest),
                equipment.Get(EquipmentSlot.Legs),
                equipment.Get(EquipmentSlot.Belt),
                equipment.Get(EquipmentSlot.Gloves),
                equipment.Get(EquipmentSlot.Boots),
                equipment.Get(EquipmentSlot.Cape),
                equipment.Get(EquipmentSlot.Ammo),
                equipment.Get(EquipmentSlot.Ring1),
                equipment.Get(EquipmentSlot.Ring2),
                equipment.Get(EquipmentSlot.Amulet),
                equipment.Get(EquipmentSlot.Artifact),
            };

            var left = equipment.Get(EquipmentSlot.LeftHand);
            var right = equipment.Get(EquipmentSlot.RightHand);
            if (!string.IsNullOrWhiteSpace(left) && left == right)
            {
                equippedItemIds.Add(left);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(left)) equippedItemIds.Add(left);
                if (!string.IsNullOrWhiteSpace(right)) equippedItemIds.Add(right);
            }

            for (int i = 0; i < equippedItemIds.Count; i++)
            {
                var itemId = equippedItemIds[i];
                if (string.IsNullOrWhiteSpace(itemId)) continue;

                ItemDefinitionSO baseItem = null;

                // Rolled instance -> base item
                if (lootRegistry.TryGetRolledInstance(itemId, out var inst) && inst != null)
                    lootRegistry.TryGetItem(inst.baseItemId, out baseItem);
                else
                    lootRegistry.TryGetItem(itemId, out baseItem);

                if (baseItem == null) continue;
                if (string.IsNullOrWhiteSpace(baseItem.setId)) continue;

                if (!counts.TryGetValue(baseItem.setId, out var c)) c = 0;
                counts[baseItem.setId] = c + 1;
            }

            return counts;
        }
    }
}
