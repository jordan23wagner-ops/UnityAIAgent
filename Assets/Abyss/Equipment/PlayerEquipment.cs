using System;
using Abyss.Items;
using UnityEngine;

namespace Abyss.Equipment
{
    [DisallowMultipleComponent]
    public sealed class PlayerEquipment : MonoBehaviour
    {
        [Header("Equipped Item IDs")]
        [SerializeField] private string helm;
        [SerializeField] private string chest;
        [SerializeField] private string legs;
        [SerializeField] private string belt;
        [SerializeField] private string gloves;
        [SerializeField] private string cape;
        [SerializeField] private string ammo;
        [SerializeField] private string leftHand;
        [SerializeField] private string rightHand;
        [SerializeField] private string ring1;
        [SerializeField] private string ring2;
        [SerializeField] private string amulet;
        [SerializeField] private string artifact;

        public event Action Changed;

        public string Get(EquipmentSlot slot)
        {
            return slot switch
            {
                EquipmentSlot.Helm => helm,
                EquipmentSlot.Chest => chest,
                EquipmentSlot.Legs => legs,
                EquipmentSlot.Belt => belt,
                EquipmentSlot.Gloves => gloves,
                EquipmentSlot.Cape => cape,
                EquipmentSlot.Ammo => ammo,
                EquipmentSlot.LeftHand => leftHand,
                EquipmentSlot.RightHand => rightHand,
                EquipmentSlot.Ring1 => ring1,
                EquipmentSlot.Ring2 => ring2,
                EquipmentSlot.Amulet => amulet,
                EquipmentSlot.Artifact => artifact,
                _ => null,
            };
        }

        public void Set(EquipmentSlot slot, string itemId)
        {
            switch (slot)
            {
                case EquipmentSlot.Helm: helm = itemId; break;
                case EquipmentSlot.Chest: chest = itemId; break;
                case EquipmentSlot.Legs: legs = itemId; break;
                case EquipmentSlot.Belt: belt = itemId; break;
                case EquipmentSlot.Gloves: gloves = itemId; break;
                case EquipmentSlot.Cape: cape = itemId; break;
                case EquipmentSlot.Ammo: ammo = itemId; break;
                case EquipmentSlot.LeftHand: leftHand = itemId; break;
                case EquipmentSlot.RightHand: rightHand = itemId; break;
                case EquipmentSlot.Ring1: ring1 = itemId; break;
                case EquipmentSlot.Ring2: ring2 = itemId; break;
                case EquipmentSlot.Amulet: amulet = itemId; break;
                case EquipmentSlot.Artifact: artifact = itemId; break;
            }
        }

        public bool TryEquipFromInventory(PlayerInventory inventory, Func<string, ItemDefinition> resolve, string itemId, out string message)
        {
            message = string.Empty;
            if (inventory == null)
            {
                message = "No PlayerInventory.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(itemId))
            {
                message = "No item selected.";
                return false;
            }

            if (!inventory.Has(itemId, 1))
            {
                message = $"You don't have '{itemId}'.";
                return false;
            }

            var def = resolve != null ? resolve(itemId) : null;
            if (def == null)
            {
                message = $"No ItemDefinition loaded for '{itemId}'.";
                return false;
            }

            if (def.equipmentSlot == EquipmentSlot.None)
            {
                message = "That item is not equippable.";
                return false;
            }

            // Unequip conflicts first (returns items to inventory).
            if (!TryResolveTargetSlots(def, out var primarySlot, out var secondarySlot))
            {
                message = "Item cannot be equipped (invalid slot config).";
                return false;
            }

            if (secondarySlot.HasValue)
            {
                // Two-handed: clear both hands.
                TryUnequipToInventory(inventory, resolve, EquipmentSlot.LeftHand);
                TryUnequipToInventory(inventory, resolve, EquipmentSlot.RightHand);
            }
            else
            {
                // One-slot: clear that slot first.
                TryUnequipToInventory(inventory, resolve, primarySlot);
            }

            // Rings: if Ring1 requested but Ring1 occupied and Ring2 empty, use Ring2.
            if (!secondarySlot.HasValue && primarySlot == EquipmentSlot.Ring1)
            {
                if (!string.IsNullOrWhiteSpace(ring1) && string.IsNullOrWhiteSpace(ring2))
                    primarySlot = EquipmentSlot.Ring2;
            }
            if (!secondarySlot.HasValue && primarySlot == EquipmentSlot.Ring2)
            {
                if (!string.IsNullOrWhiteSpace(ring2) && string.IsNullOrWhiteSpace(ring1))
                    primarySlot = EquipmentSlot.Ring1;
            }

            // Hands: allow offhand/onehand to use alternate slot when preferred is occupied.
            if (!secondarySlot.HasValue && (primarySlot == EquipmentSlot.RightHand || primarySlot == EquipmentSlot.LeftHand))
            {
                if (!string.IsNullOrWhiteSpace(Get(primarySlot)))
                {
                    var alt = primarySlot == EquipmentSlot.RightHand ? EquipmentSlot.LeftHand : EquipmentSlot.RightHand;
                    if (string.IsNullOrWhiteSpace(Get(alt)))
                        primarySlot = alt;
                    else
                    {
                        // Both occupied; ensure at least one hand is clear.
                        TryUnequipToInventory(inventory, resolve, alt);
                    }
                }
            }

            // Consume item from inventory and equip.
            if (!inventory.TryConsume(itemId, 1))
            {
                message = "Failed to remove item from inventory.";
                return false;
            }

            Set(primarySlot, itemId);
            if (secondarySlot.HasValue)
                Set(secondarySlot.Value, itemId);

            RaiseChanged();
            return true;
        }

        public bool TryUnequipToInventory(PlayerInventory inventory, Func<string, ItemDefinition> resolve, EquipmentSlot slot)
        {
            if (inventory == null) return false;

            string itemId = Get(slot);
            if (string.IsNullOrWhiteSpace(itemId)) return false;

            // If this is a two-handed item, clear both hands.
            bool clearBothHands = false;
            if (slot == EquipmentSlot.LeftHand || slot == EquipmentSlot.RightHand)
            {
                var def = resolve != null ? resolve(itemId) : null;
                if (def != null && def.weaponHandedness == WeaponHandedness.TwoHanded)
                    clearBothHands = true;

                // Also treat "same id in both hands" as two-handed.
                if (!clearBothHands && !string.IsNullOrWhiteSpace(leftHand) && leftHand == rightHand)
                    clearBothHands = true;
            }

            if (clearBothHands)
            {
                Set(EquipmentSlot.LeftHand, null);
                Set(EquipmentSlot.RightHand, null);
            }
            else
            {
                Set(slot, null);
            }

            inventory.Add(itemId, 1);
            RaiseChanged();
            return true;
        }

        private bool TryResolveTargetSlots(ItemDefinition def, out EquipmentSlot primary, out EquipmentSlot? secondary)
        {
            primary = EquipmentSlot.None;
            secondary = null;

            if (def == null) return false;

            // Two-handed always occupies both hands.
            if (def.weaponHandedness == WeaponHandedness.TwoHanded)
            {
                primary = EquipmentSlot.RightHand;
                secondary = EquipmentSlot.LeftHand;
                return true;
            }

            // Offhand defaults to LeftHand.
            if (def.weaponHandedness == WeaponHandedness.Offhand)
            {
                primary = EquipmentSlot.LeftHand;
                return true;
            }

            // One-handed defaults to RightHand.
            if (def.weaponHandedness == WeaponHandedness.OneHanded)
            {
                primary = EquipmentSlot.RightHand;
                return true;
            }

            primary = def.equipmentSlot;
            return primary != EquipmentSlot.None;
        }

        private void RaiseChanged()
        {
            try { Changed?.Invoke(); } catch { }
        }
    }
}
