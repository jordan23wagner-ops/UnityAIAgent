#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Abyss.Equipment;
using Abyss.Items;
using Abyssbound.Loot;
using Game.Systems;
using UnityEditor;
using UnityEngine;

namespace Abyssbound.QA
{
    public static class EquipStarterLoadoutMenu
    {
        private const string WeaponBronzeSword = "weapon_bronze_sword";
        private const string ArmorBasicChest = "armor_basic_chest";
        private const string ArmorBasicLegs = "armor_basic_legs";
        private const string ArmorBasicHelm = "armor_basic_helm";

        [MenuItem("Tools/Abyssbound/QA/Equip Starter Loadout")]
        private static void EquipStarterLoadout()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[QA] Equip Starter Loadout: Enter Play Mode first.");
                return;
            }

            var inv = PlayerInventoryResolver.GetOrFind();
            if (inv == null)
            {
                Debug.LogWarning("[QA] Equip Starter Loadout: PlayerInventory not found.");
                return;
            }

            var equipment = PlayerEquipmentResolver.GetOrFindOrCreate();
            if (equipment == null)
            {
                Debug.LogWarning("[QA] Equip Starter Loadout: PlayerEquipment not found/created.");
                return;
            }

            var registry = LootRegistryRuntime.GetOrCreate();
            registry.BuildIfNeeded();

            // 1) Clear current equipment (return to inventory to avoid losing items).
            ClearAllEquipmentToInventory(equipment, inv);

            // 2) Remove any existing copies of the starter items from inventory (prevents duplicates).
            PurgeStarterItemsFromInventory(inv, registry);

            // 3) Ensure base item definitions exist in the Loot V2 registry.
            EnsureBaseItemRegistered(registry, WeaponBronzeSword, EquipmentSlot.RightHand);
            EnsureBaseItemRegistered(registry, ArmorBasicChest, EquipmentSlot.Chest);
            EnsureBaseItemRegistered(registry, ArmorBasicLegs, EquipmentSlot.Legs);
            EnsureBaseItemRegistered(registry, ArmorBasicHelm, EquipmentSlot.Helm);

            // 4) Create rolled instances with fixed values (Common, iLvl 1, no affixes).
            var rolledWeapon = CreateRolledInstance(registry, WeaponBronzeSword, rarityId: "Common", itemLevel: 1);
            var rolledChest = CreateRolledInstance(registry, ArmorBasicChest, rarityId: "Common", itemLevel: 1);
            var rolledLegs = CreateRolledInstance(registry, ArmorBasicLegs, rarityId: "Common", itemLevel: 1);
            var rolledHelm = CreateRolledInstance(registry, ArmorBasicHelm, rarityId: "Common", itemLevel: 1);

            // 5) Add to inventory then equip directly (consumes from inventory, no UI clicks).
            EquipRolledId(inv, equipment, rolledWeapon);
            EquipRolledId(inv, equipment, rolledChest);
            EquipRolledId(inv, equipment, rolledLegs);

            // Optional-but-consistent helm: only equip if the slot exists in this equipment component (it does) and the item can resolve.
            EquipRolledId(inv, equipment, rolledHelm, optional: true);

            // Final cleanup: ensure no starter duplicates are left sitting in inventory.
            PurgeStarterItemsFromInventory(inv, registry);

            Debug.Log("Starter Loadout equipped.");
        }

        private static void ClearAllEquipmentToInventory(PlayerEquipment equipment, PlayerInventory inv)
        {
            if (equipment == null || inv == null) return;

            // Resolve function is only needed for legacy 2H detection; rolled instances are handled by id-in-both-hands.
            ItemDefinition ResolveLegacy(string id) => null;

            var slots = new[]
            {
                EquipmentSlot.Helm,
                EquipmentSlot.Chest,
                EquipmentSlot.Legs,
                EquipmentSlot.Belt,
                EquipmentSlot.Gloves,
                EquipmentSlot.Boots,
                EquipmentSlot.Cape,
                EquipmentSlot.Ammo,
                EquipmentSlot.LeftHand,
                EquipmentSlot.RightHand,
                EquipmentSlot.Ring1,
                EquipmentSlot.Ring2,
                EquipmentSlot.Amulet,
                EquipmentSlot.Artifact,
            };

            for (int i = 0; i < slots.Length; i++)
            {
                try { equipment.TryUnequipToInventory(inv, ResolveLegacy, slots[i]); }
                catch { }
            }
        }

        private static void PurgeStarterItemsFromInventory(PlayerInventory inv, LootRegistryRuntime registry)
        {
            if (inv == null) return;

            var starterBaseIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                WeaponBronzeSword,
                ArmorBasicChest,
                ArmorBasicLegs,
                ArmorBasicHelm,
            };

            var snap = inv.GetAllItemsSnapshot();
            foreach (var kvp in snap)
            {
                string id = kvp.Key;
                int count = kvp.Value;
                if (string.IsNullOrWhiteSpace(id) || count <= 0) continue;

                // Remove legacy base IDs directly.
                if (starterBaseIds.Contains(id))
                {
                    try { inv.TryRemove(id, count); } catch { }
                    continue;
                }

                // Remove rolled instances that correspond to starter base IDs.
                if (registry != null && registry.TryGetRolledInstance(id, out var inst) && inst != null)
                {
                    if (!string.IsNullOrWhiteSpace(inst.baseItemId) && starterBaseIds.Contains(inst.baseItemId))
                    {
                        try { inv.TryRemove(id, count); } catch { }
                    }
                }
            }
        }

        private static void EnsureBaseItemRegistered(LootRegistryRuntime registry, string baseItemId, EquipmentSlot slotFallback)
        {
            if (registry == null) return;
            if (string.IsNullOrWhiteSpace(baseItemId)) return;

            if (registry.TryGetItem(baseItemId, out var existing) && existing != null)
                return;

            // Try to find a legacy ItemDefinition asset to promote.
            ItemDefinition legacy = null;
            try
            {
                var defs = Resources.FindObjectsOfTypeAll<ItemDefinition>();
                if (defs != null)
                {
                    for (int i = 0; i < defs.Length; i++)
                    {
                        var d = defs[i];
                        if (d == null) continue;
                        string id = null;
                        try { id = d.itemId; } catch { id = null; }
                        if (string.IsNullOrWhiteSpace(id)) continue;
                        if (string.Equals(id, baseItemId, StringComparison.OrdinalIgnoreCase))
                        {
                            legacy = d;
                            break;
                        }
                    }
                }
            }
            catch { legacy = null; }

            var so = ScriptableObject.CreateInstance<ItemDefinitionSO>();
            so.id = baseItemId;

            if (legacy != null)
            {
                try { so.displayName = string.IsNullOrWhiteSpace(legacy.displayName) ? baseItemId : legacy.displayName; }
                catch { so.displayName = baseItemId; }

                try { so.icon = legacy.icon; } catch { so.icon = null; }

                try { so.slot = legacy.equipmentSlot; }
                catch { so.slot = slotFallback; }

                so.occupiesSlots = new List<EquipmentSlot>(2);
                try
                {
                    if (legacy.weaponHandedness == WeaponHandedness.TwoHanded)
                    {
                        so.occupiesSlots.Add(EquipmentSlot.LeftHand);
                        so.occupiesSlots.Add(EquipmentSlot.RightHand);
                    }
                    else if (legacy.weaponHandedness == WeaponHandedness.Offhand)
                    {
                        so.occupiesSlots.Add(EquipmentSlot.LeftHand);
                    }
                }
                catch { }

                // Do not invent new stats here; preserve authored definition if present.
                so.baseStats = new List<StatMod>(4);
                try
                {
                    if (legacy.DamageBonus != 0)
                        so.baseStats.Add(new StatMod { stat = GuessDamageStatType(baseItemId), value = legacy.DamageBonus, percent = false });
                }
                catch { }

                try
                {
                    if (legacy.DamageReductionFlat != 0)
                        so.baseStats.Add(new StatMod { stat = StatType.Defense, value = legacy.DamageReductionFlat, percent = false });
                }
                catch { }

                try
                {
                    if (legacy.MaxHealthBonus != 0)
                        so.baseStats.Add(new StatMod { stat = StatType.MaxHealth, value = legacy.MaxHealthBonus, percent = false });
                }
                catch { }
            }
            else
            {
                // Minimal fallback: slot only.
                so.displayName = baseItemId;
                so.slot = slotFallback;
                so.occupiesSlots = new List<EquipmentSlot>(0);
                so.baseStats = new List<StatMod>(0);
            }

            registry.RegisterOrUpdateItem(so);
        }

        private static string CreateRolledInstance(LootRegistryRuntime registry, string baseItemId, string rarityId, int itemLevel)
        {
            if (registry == null || string.IsNullOrWhiteSpace(baseItemId))
                return null;

            var inst = new ItemInstance
            {
                baseItemId = baseItemId,
                rarityId = string.IsNullOrWhiteSpace(rarityId) ? "Common" : rarityId,
                itemLevel = Mathf.Max(1, itemLevel),
                baseScalar = 1f,
                affixes = new List<AffixRoll>()
            };

            try { return registry.RegisterRolledInstance(inst); }
            catch { return null; }
        }

        private static void EquipRolledId(PlayerInventory inv, PlayerEquipment equipment, string rolledId, bool optional = false)
        {
            if (inv == null || equipment == null)
                return;

            if (string.IsNullOrWhiteSpace(rolledId))
            {
                if (!optional)
                    Debug.LogWarning("[QA] Equip Starter Loadout: failed to create a required rolled instance.");
                return;
            }

            try { inv.Add(rolledId, 1); } catch { }

            // Resolve not needed for rolled instances.
            ItemDefinition ResolveLegacy(string id) => null;

            try
            {
                if (!equipment.TryEquipFromInventory(inv, ResolveLegacy, rolledId, out var msg))
                {
                    if (!optional)
                        Debug.LogWarning($"[QA] Equip Starter Loadout: equip failed for '{rolledId}': {msg}");
                }
            }
            catch (Exception e)
            {
                if (!optional)
                    Debug.LogWarning($"[QA] Equip Starter Loadout: equip exception for '{rolledId}': {e.Message}");
            }
        }

        private static StatType GuessDamageStatType(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return StatType.MeleeDamage;

            var id = itemId.ToLowerInvariant();
            if (id.Contains("bow") || id.Contains("ranged")) return StatType.RangedDamage;
            if (id.Contains("staff") || id.Contains("wand") || id.Contains("magic")) return StatType.MagicDamage;
            return StatType.MeleeDamage;
        }
    }
}
#endif
