#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Abyss.Equipment;
using Abyss.Items;
using Abyss.Dev;
using Abyssbound.Loot;
using Abyssbound.Stats;
using Game.Systems;
using UnityEditor;
using UnityEngine;

namespace Abyssbound.Editor.QA.Stats
{
    public static class StatsQaMenu
    {
        private const string Root = "Tools/Abyssbound/QA/Stats/";

        private static bool s_warnedNotPlaying;

        [MenuItem(Root + "Print Leveled Stats")]
        public static void PrintLeveledStats()
        {
            if (!EnsurePlayMode())
                return;

            if (!TryFindStats(out var stats))
                return;

            Debug.Log("[QA][Stats] LEVELED\n" + stats.Leveled.ToMultilineString());
        }

        [MenuItem(Root + "Print GearBonus Stats")]
        public static void PrintGearBonusStats()
        {
            if (!EnsurePlayMode())
                return;

            if (!TryFindStats(out var stats))
                return;

            try { stats.RebuildNow(); } catch { }

            Debug.Log("[QA][Stats] GEAR BONUS\n" + stats.GearBonus.ToMultilineString());
        }

        [MenuItem(Root + "Print TotalPrimary Stats")]
        public static void PrintTotalPrimaryStats()
        {
            if (!EnsurePlayMode())
                return;

            if (!TryFindStats(out var stats))
                return;

            try { stats.RebuildNow(); } catch { }

            Debug.Log("[QA][Stats] TOTAL PRIMARY\n" + stats.TotalPrimary.ToMultilineString());
        }

        [MenuItem(Root + "Print Derived Stats")]
        public static void PrintDerivedStats()
        {
            if (!EnsurePlayMode())
                return;

            if (!TryFindStats(out var stats))
                return;

            try { stats.RebuildNow(); } catch { }

            Debug.Log("[QA][Stats] Derived calculator mode: " + StatCalculator.CalculatorMode);
            Debug.Log("[QA][Stats] DERIVED\n" + stats.Derived.ToMultilineString());
        }

        [MenuItem(Root + "Spawn Damage Test Enemy")]
        public static void SpawnDamageTestEnemy()
        {
            if (!EnsurePlayMode())
                return;

            // Preferred: reuse the same spawn logic as the existing DevCheats hotkeys.
            DevCheats cheats = null;
            try { cheats = DevCheats.Instance; } catch { cheats = null; }

            if (cheats == null)
            {
                try
                {
#if UNITY_2023_1_OR_NEWER
                    cheats = UnityEngine.Object.FindAnyObjectByType<DevCheats>(FindObjectsInactive.Exclude);
#else
                    cheats = UnityEngine.Object.FindObjectOfType<DevCheats>();
#endif
                }
                catch { cheats = null; }
            }

            if (cheats != null)
            {
                try
                {
                    cheats.SpawnDamageTestEnemy();
                    Debug.Log("[QA][Stats] Spawned damage test enemy via DevCheats.");
                    return;
                }
                catch
                {
                    // Fall through to dummy spawn.
                }
            }

            // Fallback: spawn the standard dummy enemy prefab (keeps this QA menu item usable
            // even if DevCheats isn't present/configured in the scene).
            var prefab = LoadDummyEnemyPrefab();
            if (prefab == null)
            {
                Debug.LogWarning("[QA][Stats] No DevCheats found and no dummy prefab found. Add DevCheats (with enemyPrefabs) or ensure Enemy_Dummy prefab exists.");
                return;
            }

            var anchor = FindAnchor();
            var basePos = anchor != null ? anchor.position : Vector3.zero;
            var forward = anchor != null ? anchor.forward : Vector3.forward;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;

            var pos = basePos + forward.normalized * 3.5f;
            var go = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);
            go.name = "QA_DamageTest";

            try
            {
                var legacy = go.GetComponentInChildren<DropOnDeath>(true);
                if (legacy != null) legacy.enabled = false;
            }
            catch { }

            try
            {
                var lod = go.GetComponentInChildren<LootDropOnDeath>(true);
                if (lod != null) lod.enabled = false;
            }
            catch { }

            try
            {
                var eh = go.GetComponentInChildren<EnemyHealth>(true);
                if (eh != null)
                    eh.SetMaxHealthForQa(50);
            }
            catch { }

            Debug.Log("[QA][Stats] Spawned damage test dummy enemy (fallback).");
        }

        [MenuItem(Root + "Give + Equip Stat Test Kit (Attack)")]
        public static void GiveEquipAttackKit() => GiveEquipPrimaryKit(
            kitId: "QA_StatKit_Attack",
            displayName: "QA Stat Kit: Attack",
            slot: EquipmentSlot.RightHand,
            occupiesBothHands: true,
            stat: Abyssbound.Loot.StatType.Attack,
            value: 5);

        [MenuItem(Root + "Give + Equip Stat Test Kit (Strength)")]
        public static void GiveEquipStrengthKit() => GiveEquipPrimaryKit(
            kitId: "QA_StatKit_Strength",
            displayName: "QA Stat Kit: Strength",
            slot: EquipmentSlot.Helm,
            occupiesBothHands: false,
            stat: Abyssbound.Loot.StatType.Strength,
            value: 5);

        [MenuItem(Root + "Give + Equip Stat Test Kit (Defence)")]
        public static void GiveEquipDefenceKit() => GiveEquipPrimaryKit(
            kitId: "QA_StatKit_Defence",
            displayName: "QA Stat Kit: Defence",
            slot: EquipmentSlot.Chest,
            occupiesBothHands: false,
            stat: Abyssbound.Loot.StatType.DefenseSkill,
            value: 5);

        [MenuItem(Root + "Give + Equip Stat Test Kit (Ranged)")]
        public static void GiveEquipRangedKit() => GiveEquipPrimaryKit(
            kitId: "QA_StatKit_Ranged",
            displayName: "QA Stat Kit: Ranged",
            slot: EquipmentSlot.Legs,
            occupiesBothHands: false,
            stat: Abyssbound.Loot.StatType.RangedSkill,
            value: 5);

        [MenuItem(Root + "Give + Equip Stat Test Kit (Magic)")]
        public static void GiveEquipMagicKit() => GiveEquipPrimaryKit(
            kitId: "QA_StatKit_Magic",
            displayName: "QA Stat Kit: Magic",
            slot: EquipmentSlot.Belt,
            occupiesBothHands: false,
            stat: Abyssbound.Loot.StatType.MagicSkill,
            value: 5);

        [MenuItem(Root + "Give + Equip Stat Test Kit (Fishing)")]
        public static void GiveEquipFishingKit() => GiveEquipPrimaryKit(
            kitId: "QA_StatKit_Fishing",
            displayName: "QA Stat Kit: Fishing",
            slot: EquipmentSlot.Gloves,
            occupiesBothHands: false,
            stat: Abyssbound.Loot.StatType.Fishing,
            value: 5);

        [MenuItem(Root + "Give + Equip Stat Test Kit (Cooking)")]
        public static void GiveEquipCookingKit() => GiveEquipPrimaryKit(
            kitId: "QA_StatKit_Cooking",
            displayName: "QA Stat Kit: Cooking",
            slot: EquipmentSlot.Boots,
            occupiesBothHands: false,
            stat: Abyssbound.Loot.StatType.Cooking,
            value: 5);

        private static void GiveEquipPrimaryKit(string kitId, string displayName, EquipmentSlot slot, bool occupiesBothHands, StatType stat, int value)
        {
            if (!EnsurePlayMode())
                return;

            if (!TryFindInventory(out var inventory))
                return;

            if (!TryFindEquipment(out var equipment))
                return;

            var registry = LootRegistryRuntime.GetOrCreate();
            registry.BuildIfNeeded();

            // Guard: if bootstrap is missing, equipping rolled instances won’t resolve.
            if (!HasLootBootstrap())
            {
                Debug.LogError("[QA][Stats] Loot bootstrap missing (Resources/Loot/Bootstrap.asset). Run Tools/Abyssbound/Loot/Create Starter Loot Content.");
                return;
            }

            var baseItem = ScriptableObject.CreateInstance<ItemDefinitionSO>();
            baseItem.id = kitId;
            baseItem.displayName = displayName;
            baseItem.slot = slot;
            baseItem.occupiesSlots = new List<EquipmentSlot>(2);
            if (occupiesBothHands)
            {
                baseItem.occupiesSlots.Add(EquipmentSlot.LeftHand);
                baseItem.occupiesSlots.Add(EquipmentSlot.RightHand);
            }

            baseItem.baseStats = new List<StatMod>(1)
            {
                new StatMod { stat = stat, value = Mathf.Max(0, value), percent = false }
            };

            registry.RegisterOrUpdateItem(baseItem);

            var inst = new ItemInstance
            {
                baseItemId = baseItem.id,
                rarityId = "Common",
                itemLevel = 1,
                baseScalar = 1f,
            };

            var rolledId = registry.RegisterRolledInstance(inst);
            if (string.IsNullOrWhiteSpace(rolledId))
            {
                Debug.LogError("[QA][Stats] Failed to register rolled instance.");
                return;
            }

            inventory.Add(rolledId, 1);

            if (!equipment.TryEquipFromInventory(inventory, resolve: null, itemId: rolledId, out var msg))
            {
                Debug.LogError("[QA][Stats] Equip failed: " + msg);
                return;
            }

            Debug.Log($"[QA][Stats] Granted+Equipped {displayName} ({stat} +{Mathf.Max(0, value)}) rolledId={rolledId}");

            // Trigger immediate refresh for convenience.
            if (TryFindStats(out var stats))
            {
                try { stats.MarkDirty(); } catch { }
                try { stats.RebuildNow(); } catch { }
            }
        }

        private static bool EnsurePlayMode()
        {
            if (Application.isPlaying)
                return true;

            if (!s_warnedNotPlaying)
            {
                s_warnedNotPlaying = true;
                Debug.LogWarning("[QA][Stats] Enter Play Mode first.");
            }

            return false;
        }

        private static bool TryFindInventory(out PlayerInventory inventory)
        {
            inventory = null;
            try { inventory = PlayerInventoryResolver.GetOrFind(); } catch { inventory = null; }

            if (inventory != null)
                return true;

            Debug.LogError("[QA][Stats] No active PlayerInventory found.");
            return false;
        }

        private static bool TryFindEquipment(out PlayerEquipment equipment)
        {
            equipment = null;
            try { equipment = PlayerEquipmentResolver.GetOrFindOrCreate(); } catch { equipment = null; }

            if (equipment != null)
                return true;

            Debug.LogError("[QA][Stats] No PlayerEquipment found.");
            return false;
        }

        private static bool TryFindStats(out PlayerStatsRuntime stats)
        {
            stats = null;

            try
            {
#if UNITY_2023_1_OR_NEWER
                stats = UnityEngine.Object.FindAnyObjectByType<PlayerStatsRuntime>(FindObjectsInactive.Exclude);
#else
                stats = UnityEngine.Object.FindObjectOfType<PlayerStatsRuntime>();
#endif
            }
            catch { stats = null; }

            if (stats != null)
                return true;

            // Fallback: if combat stats exists, it will auto-add PlayerStatsRuntime.
            PlayerCombatStats combat = null;
            try
            {
#if UNITY_2023_1_OR_NEWER
                combat = UnityEngine.Object.FindAnyObjectByType<PlayerCombatStats>(FindObjectsInactive.Exclude);
#else
                combat = UnityEngine.Object.FindObjectOfType<PlayerCombatStats>();
#endif
            }
            catch { combat = null; }

            if (combat != null)
            {
                try { stats = combat.GetComponent<PlayerStatsRuntime>(); } catch { stats = null; }
                if (stats == null)
                {
                    try { stats = combat.gameObject.AddComponent<PlayerStatsRuntime>(); } catch { stats = null; }
                }
            }

            if (stats != null)
                return true;

            Debug.LogError("[QA][Stats] No PlayerStatsRuntime found.");
            return false;
        }

        private static bool HasLootBootstrap()
        {
            try
            {
                var bootstrap = Resources.Load<LootRegistryBootstrapSO>("Loot/Bootstrap");
                return bootstrap != null;
            }
            catch
            {
                return false;
            }
        }

        private static GameObject LoadDummyEnemyPrefab()
        {
            // Keep in sync with other QA helpers.
            const string DummyPrefabPathA = "Assets/Prefabs/Enemy_Dummy/Enemy_Dummy.prefab";
            const string DummyPrefabPathB = "Assets/Abyssbound/Prefabs/Actors/Enemies/Enemy_Dummy.prefab";

            try
            {
                var a = AssetDatabase.LoadAssetAtPath<GameObject>(DummyPrefabPathA);
                if (a != null) return a;
            }
            catch { }

            try { return AssetDatabase.LoadAssetAtPath<GameObject>(DummyPrefabPathB); }
            catch { return null; }
        }

        private static Transform FindAnchor()
        {
            try
            {
#if UNITY_2023_1_OR_NEWER
                var playerHealth = UnityEngine.Object.FindAnyObjectByType<PlayerHealth>(FindObjectsInactive.Exclude);
#else
                var playerHealth = UnityEngine.Object.FindObjectOfType<PlayerHealth>();
#endif
                if (playerHealth != null) return playerHealth.transform;
            }
            catch { }

            if (Camera.main != null) return Camera.main.transform;
            return null;
        }
    }
}
#endif
