using System;
using System.Collections.Generic;
using Abyss.Items;
using Game.Systems;
using UnityEngine;

namespace Abyssbound.Loot
{
    public static class LootQaSpawnHelper
    {
        // Inventory UI uses a fixed 4x7 grid (28 slots). If inventory exceeds this,
        // newly added items may not appear in the UI (it truncates display).
        // For QA/dev-cheats we treat this as "inventory full" and drop to world.
        private const int InventoryUiSlots = 28;

        public static readonly string[] AllRarityIds = { "Common", "Uncommon", "Magic", "Rare", "Epic", "Legendary" };
        public static readonly string[] MagicPlusRarityIds = { "Magic", "Rare", "Epic", "Legendary" };

        public static int SpawnSelectedItemForRarityIds(
            UnityEngine.Object selection,
            IReadOnlyList<string> rarityIds,
            int itemLevel,
            bool perItemLogs = false,
            string logPrefix = "[Loot QA]",
            List<string> outRolledIds = null
        )
        {
            if (selection == null) return 0;
            if (rarityIds == null || rarityIds.Count == 0) return 0;

            var baseItem = ResolveOrPromoteBaseItem(selection);
            if (baseItem == null) return 0;

            if (string.IsNullOrWhiteSpace(baseItem.id))
            {
                if (perItemLogs)
                    Debug.LogWarning($"{logPrefix} Spawn aborted: selected base item has no id (name='{baseItem.name}').");
                return 0;
            }

            var registry = LootRegistryRuntime.GetOrCreate();
            registry.BuildIfNeeded();

            // Critical for inventory/UI visibility: ensure the selected base item is registered,
            // even if it doesn't live under Resources and wasn't built into the registry.
            registry.RegisterOrUpdateItem(baseItem);

            int ilvl = Mathf.Max(1, itemLevel);
            var inv = PlayerInventoryResolver.GetOrFind();

            int spawned = 0;
            for (int i = 0; i < rarityIds.Count; i++)
            {
                var rid = rarityIds[i];
                if (string.IsNullOrWhiteSpace(rid)) continue;

                if (!registry.TryGetRarity(rid, out var rarity) || rarity == null)
                    continue;

                var inst = LootRollerV2.RollItem(baseItem, rarity, itemLevel: ilvl);
                if (inst == null) continue;

                // Register early so callers can reference/equip the rolled instance id.
                string rolledId = null;
                try { rolledId = registry.RegisterRolledInstance(inst); } catch { rolledId = null; }
                try { if (!string.IsNullOrWhiteSpace(rolledId)) outRolledIds?.Add(rolledId); } catch { }

                int affixCount = 0;
                try { affixCount = inst.affixes != null ? inst.affixes.Count : 0; } catch { affixCount = 0; }

                bool deliveredToInventory = false;
                bool droppedToWorld = false;

                // Prefer inventory; if UI is effectively "full", drop to world so QA can see it.
                if (inv != null && !WouldExceedInventoryUiCapacity(inv, additionalStacks: 1))
                {
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(rolledId))
                        {
                            inv.Add(rolledId, 1);
                            deliveredToInventory = true;
                        }
                    }
                    catch
                    {
                        deliveredToInventory = false;
                    }
                }

                if (!deliveredToInventory)
                {
                    DropInstanceNearPlayer(inst);
                    droppedToWorld = true;
                }

                if (perItemLogs)
                {
                    string itemName = baseItem != null
                        ? (!string.IsNullOrWhiteSpace(baseItem.displayName) ? baseItem.displayName : baseItem.id)
                        : "<null>";

                    Debug.Log($"{logPrefix} Spawned '{itemName}' rarity={rid} ilvl={ilvl} affixes={affixCount} deliveredToInventory={deliveredToInventory} droppedToWorld={droppedToWorld}");
                }

                spawned++;
            }

            return spawned;
        }

        private static bool WouldExceedInventoryUiCapacity(PlayerInventory inventory, int additionalStacks)
        {
            if (inventory == null) return true;
            additionalStacks = Mathf.Max(0, additionalStacks);

            int current = 0;
            try { current = inventory.GetStackCount(); } catch { current = 0; }

            // If current is already beyond UI capacity, treat it as full.
            return (current + additionalStacks) > InventoryUiSlots;
        }

        private static ItemDefinitionSO ResolveOrPromoteBaseItem(UnityEngine.Object selection)
        {
            if (selection is ItemDefinitionSO so)
                return so;

            if (selection is ItemDefinition legacy)
                return BuildLootV2BaseItemFromLegacy(legacy);

            return null;
        }

        private static ItemDefinitionSO BuildLootV2BaseItemFromLegacy(ItemDefinition legacy)
        {
            if (legacy == null) return null;

            string id = null;
            try { id = legacy.itemId; } catch { id = null; }
            if (string.IsNullOrWhiteSpace(id)) return null;

            var so = ScriptableObject.CreateInstance<ItemDefinitionSO>();
            so.id = id;

            try { so.displayName = string.IsNullOrWhiteSpace(legacy.displayName) ? id : legacy.displayName; }
            catch { so.displayName = id; }

            try { so.icon = legacy.icon; } catch { so.icon = null; }

            try { so.slot = legacy.equipmentSlot; }
            catch { so.slot = EquipmentSlot.None; }

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

            so.baseStats = new List<StatMod>(4);
            try
            {
                if (legacy.DamageBonus != 0)
                    so.baseStats.Add(new StatMod { stat = GuessDamageStatType(id), value = legacy.DamageBonus, percent = false });
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

            so.allowedAffixTags = new List<AffixTag>(2);
            so.allowedAffixTags.Add(GuessAffixTag(id, so.slot));

            var registry = LootRegistryRuntime.GetOrCreate();
            registry.BuildIfNeeded();
            registry.RegisterOrUpdateItem(so);

            return so;
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

        private static AffixTag GuessAffixTag(string itemId, EquipmentSlot slot)
        {
            if (slot == EquipmentSlot.RightHand || slot == EquipmentSlot.LeftHand)
            {
                var dmg = GuessDamageStatType(itemId);
                return dmg switch
                {
                    StatType.RangedDamage => AffixTag.WeaponRanged,
                    StatType.MagicDamage => AffixTag.WeaponMagic,
                    _ => AffixTag.WeaponMelee,
                };
            }

            return slot switch
            {
                EquipmentSlot.Helm => AffixTag.Armor,
                EquipmentSlot.Chest => AffixTag.Armor,
                EquipmentSlot.Legs => AffixTag.Armor,
                EquipmentSlot.Gloves => AffixTag.Armor,
                EquipmentSlot.Boots => AffixTag.Armor,
                EquipmentSlot.Belt => AffixTag.Armor,
                EquipmentSlot.Cape => AffixTag.Armor,
                EquipmentSlot.Ring1 => AffixTag.Jewelry,
                EquipmentSlot.Ring2 => AffixTag.Jewelry,
                EquipmentSlot.Amulet => AffixTag.Jewelry,
                EquipmentSlot.Artifact => AffixTag.Jewelry,
                _ => AffixTag.Any,
            };
        }

        private static void DropInstanceNearPlayer(ItemInstance inst)
        {
            if (inst == null) return;

            Vector3 pos = Vector3.zero;
            try
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    pos = player.transform.position + player.transform.forward * 1.25f;
                }
            }
            catch { }

            try
            {
                var o = UnityEngine.Random.insideUnitCircle * 0.35f;
                pos += new Vector3(o.x, 0f, o.y);
            }
            catch { }

            var go = new GameObject("Pickup_QA_" + (string.IsNullOrWhiteSpace(inst.baseItemId) ? "Item" : inst.baseItemId));
            go.transform.position = pos;

            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.5f;

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var pickup = go.AddComponent<WorldItemPickup>();
            pickup.Initialize(inst);
        }
    }
}
