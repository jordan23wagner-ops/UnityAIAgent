using System;
using System.Collections.Generic;
using Abyss.Equipment;
using Abyss.Items;

namespace Abyssbound.DeathDrop
{
    public sealed class EquipmentDeathDropProvider : IDeathDropProvider
    {
        private readonly PlayerEquipment _equipment;

        public EquipmentDeathDropProvider(PlayerEquipment equipment)
        {
            _equipment = equipment;
        }

        public IEnumerable<DroppedItem> GetAllItems()
        {
            if (_equipment == null)
                yield break;

            // Mirror EquipmentSetCounter behavior: treat same id in both hands as one piece.
            var items = new List<string>(16)
            {
                _equipment.Get(EquipmentSlot.Helm),
                _equipment.Get(EquipmentSlot.Chest),
                _equipment.Get(EquipmentSlot.Legs),
                _equipment.Get(EquipmentSlot.Belt),
                _equipment.Get(EquipmentSlot.Gloves),
                _equipment.Get(EquipmentSlot.Boots),
                _equipment.Get(EquipmentSlot.Cape),
                _equipment.Get(EquipmentSlot.Ammo),
                _equipment.Get(EquipmentSlot.Ring1),
                _equipment.Get(EquipmentSlot.Ring2),
                _equipment.Get(EquipmentSlot.Amulet),
                _equipment.Get(EquipmentSlot.Artifact),
            };

            var left = _equipment.Get(EquipmentSlot.LeftHand);
            var right = _equipment.Get(EquipmentSlot.RightHand);
            if (!string.IsNullOrWhiteSpace(left) && string.Equals(left, right, StringComparison.Ordinal))
            {
                items.Add(left);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(left)) items.Add(left);
                if (!string.IsNullOrWhiteSpace(right)) items.Add(right);
            }

            // Count duplicates by itemId.
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < items.Count; i++)
            {
                var id = items[i];
                if (string.IsNullOrWhiteSpace(id)) continue;

                if (!counts.TryGetValue(id, out var c)) c = 0;
                counts[id] = c + 1;
            }

            foreach (var kvp in counts)
                yield return new DroppedItem(kvp.Key, kvp.Value);
        }

        public bool Contains(string itemId, int amount)
        {
            if (_equipment == null) return false;
            if (string.IsNullOrWhiteSpace(itemId) || amount <= 0) return false;
            return GetCount(itemId) >= amount;
        }

        public int GetCount(string itemId)
        {
            if (_equipment == null) return 0;
            if (string.IsNullOrWhiteSpace(itemId)) return 0;

            int count = 0;

            // Slots (excluding hands; hands handled with 2H rule below).
            count += SlotEquals(EquipmentSlot.Helm, itemId) ? 1 : 0;
            count += SlotEquals(EquipmentSlot.Chest, itemId) ? 1 : 0;
            count += SlotEquals(EquipmentSlot.Legs, itemId) ? 1 : 0;
            count += SlotEquals(EquipmentSlot.Belt, itemId) ? 1 : 0;
            count += SlotEquals(EquipmentSlot.Gloves, itemId) ? 1 : 0;
            count += SlotEquals(EquipmentSlot.Boots, itemId) ? 1 : 0;
            count += SlotEquals(EquipmentSlot.Cape, itemId) ? 1 : 0;
            count += SlotEquals(EquipmentSlot.Ammo, itemId) ? 1 : 0;
            count += SlotEquals(EquipmentSlot.Ring1, itemId) ? 1 : 0;
            count += SlotEquals(EquipmentSlot.Ring2, itemId) ? 1 : 0;
            count += SlotEquals(EquipmentSlot.Amulet, itemId) ? 1 : 0;
            count += SlotEquals(EquipmentSlot.Artifact, itemId) ? 1 : 0;

            var left = _equipment.Get(EquipmentSlot.LeftHand);
            var right = _equipment.Get(EquipmentSlot.RightHand);

            if (!string.IsNullOrWhiteSpace(left) && string.Equals(left, right, StringComparison.Ordinal))
            {
                if (string.Equals(left, itemId, StringComparison.OrdinalIgnoreCase))
                    count += 1;
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(left) && string.Equals(left, itemId, StringComparison.OrdinalIgnoreCase))
                    count += 1;
                if (!string.IsNullOrWhiteSpace(right) && string.Equals(right, itemId, StringComparison.OrdinalIgnoreCase))
                    count += 1;
            }

            return count;
        }

        public bool TryRemove(string itemId, int amount)
        {
            if (_equipment == null) return false;
            if (string.IsNullOrWhiteSpace(itemId) || amount <= 0) return false;

            int removed = 0;

            // Unequip matching non-hand slots first.
            removed += TryUnequipSlotIfMatches(EquipmentSlot.Helm, itemId) ? 1 : 0;
            if (removed >= amount) return true;

            removed += TryUnequipSlotIfMatches(EquipmentSlot.Chest, itemId) ? 1 : 0;
            if (removed >= amount) return true;

            removed += TryUnequipSlotIfMatches(EquipmentSlot.Legs, itemId) ? 1 : 0;
            if (removed >= amount) return true;

            removed += TryUnequipSlotIfMatches(EquipmentSlot.Belt, itemId) ? 1 : 0;
            if (removed >= amount) return true;

            removed += TryUnequipSlotIfMatches(EquipmentSlot.Gloves, itemId) ? 1 : 0;
            if (removed >= amount) return true;

            removed += TryUnequipSlotIfMatches(EquipmentSlot.Boots, itemId) ? 1 : 0;
            if (removed >= amount) return true;

            removed += TryUnequipSlotIfMatches(EquipmentSlot.Cape, itemId) ? 1 : 0;
            if (removed >= amount) return true;

            removed += TryUnequipSlotIfMatches(EquipmentSlot.Ammo, itemId) ? 1 : 0;
            if (removed >= amount) return true;

            removed += TryUnequipSlotIfMatches(EquipmentSlot.Ring1, itemId) ? 1 : 0;
            if (removed >= amount) return true;

            removed += TryUnequipSlotIfMatches(EquipmentSlot.Ring2, itemId) ? 1 : 0;
            if (removed >= amount) return true;

            removed += TryUnequipSlotIfMatches(EquipmentSlot.Amulet, itemId) ? 1 : 0;
            if (removed >= amount) return true;

            removed += TryUnequipSlotIfMatches(EquipmentSlot.Artifact, itemId) ? 1 : 0;
            if (removed >= amount) return true;

            // Hands last; TryUnequip handles 2H by clearing both if represented as same id in both hands.
            if (TryUnequipSlotIfMatches(EquipmentSlot.LeftHand, itemId))
            {
                removed++;
                if (removed >= amount) return true;
            }

            if (TryUnequipSlotIfMatches(EquipmentSlot.RightHand, itemId))
            {
                removed++;
                if (removed >= amount) return true;
            }

            return removed >= amount;
        }

        private bool SlotEquals(EquipmentSlot slot, string itemId)
        {
            var cur = _equipment.Get(slot);
            return !string.IsNullOrWhiteSpace(cur) && string.Equals(cur, itemId, StringComparison.OrdinalIgnoreCase);
        }

        private bool TryUnequipSlotIfMatches(EquipmentSlot slot, string itemId)
        {
            if (!SlotEquals(slot, itemId))
                return false;

            try
            {
                return _equipment.TryUnequip(resolve: null, slot);
            }
            catch
            {
                return false;
            }
        }
    }
}
