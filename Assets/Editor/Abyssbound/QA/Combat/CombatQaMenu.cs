#if UNITY_EDITOR
using Abyss.Dev;
using Abyssbound.Combat;
using Abyssbound.Stats;
using Abyssbound.Loot;
using Abyss.Equipment;
using Game.Systems;
using UnityEditor;
using UnityEngine;

namespace Abyssbound.Editor.QA.Combat
{
    public static class CombatQaMenu
    {
        private const string Root = "Tools/Abyssbound/QA/Combat/";

        private const string WeaponMeleePath = "Assets/Resources/Loot/Items/Item_Starter_Sword.asset";
        private const string WeaponRangedPath = "Assets/Resources/Loot/Items/Item_Starter_Bow.asset";
        private const string WeaponMagicPath = "Assets/Resources/Loot/Items/Item_QA_Staff_2H.asset";

        [MenuItem(Root + "Toggle Always Hit (On/Off)")]
        public static void ToggleAlwaysHit()
        {
            CombatQaFlags.AlwaysHit = !CombatQaFlags.AlwaysHit;
            Debug.Log($"[QA][Combat] AlwaysHit={(CombatQaFlags.AlwaysHit ? "ON" : "OFF")}");
        }

        [MenuItem(Root + "Toggle Defence XP From Damage Taken (On/Off)")]
        public static void ToggleDefenceXpFromDamageTaken()
        {
            XpAwardFlags.AwardDefenceXpFromDamageTaken = !XpAwardFlags.AwardDefenceXpFromDamageTaken;
            Debug.Log($"[QA][Combat] AwardDefenceXpFromDamageTaken={(XpAwardFlags.AwardDefenceXpFromDamageTaken ? "ON" : "OFF")}");
        }

        [MenuItem(Root + "Toggle Draw Attack Range Gizmos (On/Off)")]
        public static void ToggleDrawAttackRangeGizmos()
        {
            CombatQaFlags.DrawAttackRanges = !CombatQaFlags.DrawAttackRanges;
            Debug.Log($"[QA][Combat] DrawAttackRanges={(CombatQaFlags.DrawAttackRanges ? "ON" : "OFF")}");
        }

        [MenuItem(Root + "Toggle Projectile Visuals (On/Off)")]
        public static void ToggleProjectileVisuals()
        {
            CombatQaFlags.ProjectileVisualsEnabled = !CombatQaFlags.ProjectileVisualsEnabled;
            Debug.Log($"[QA][Combat] ProjectileVisualsEnabled={(CombatQaFlags.ProjectileVisualsEnabled ? "ON" : "OFF")}");
        }

        [MenuItem(Root + "Toggle Attack Debug Logs (On/Off)")]
        public static void ToggleAttackDebugLogs()
        {
            CombatQaFlags.AttackDebugLogs = !CombatQaFlags.AttackDebugLogs;
            Debug.Log($"[QA][Combat] AttackDebugLogs={(CombatQaFlags.AttackDebugLogs ? "ON" : "OFF")}");
        }

        [MenuItem(Root + "Toggle Projectile Debug (On/Off)")]
        public static void ToggleProjectileDebug()
        {
            CombatQaFlags.ProjectileDebug = !CombatQaFlags.ProjectileDebug;
            Debug.Log($"[QA][Combat] ProjectileDebug={(CombatQaFlags.ProjectileDebug ? "ON" : "OFF")}");
        }

        [MenuItem(Root + "Equip Test Weapon (Melee)")]
        public static void EquipTestWeaponMelee() => EquipTestWeaponFromAsset(WeaponMeleePath, label: "Melee");

        [MenuItem(Root + "Equip Test Weapon (Ranged)")]
        public static void EquipTestWeaponRanged() => EquipTestWeaponFromAsset(WeaponRangedPath, label: "Ranged");

        [MenuItem(Root + "Equip Test Weapon (Magic)")]
        public static void EquipTestWeaponMagic() => EquipTestWeaponFromAsset(WeaponMagicPath, label: "Magic");

        private static void EquipTestWeaponFromAsset(string assetPath, string label)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[QA][Combat] Enter Play Mode first.");
                return;
            }

            var inv = PlayerInventoryResolver.GetOrFind();
            if (inv == null)
            {
                Debug.LogWarning("[QA][Combat] PlayerInventory not found.");
                return;
            }

            var equipment = PlayerEquipmentResolver.GetOrFindOrCreate();
            if (equipment == null)
            {
                Debug.LogWarning("[QA][Combat] PlayerEquipment not found/created.");
                return;
            }

            var baseItem = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(assetPath);
            if (baseItem == null)
            {
                Debug.LogWarning($"[QA][Combat] Missing ItemDefinitionSO at '{assetPath}'.");
                return;
            }

            var registry = LootRegistryRuntime.GetOrCreate();
            registry.BuildIfNeeded();
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
                Debug.LogWarning("[QA][Combat] Failed to register rolled weapon instance.");
                return;
            }

            inv.Add(rolledId, 1);

            if (!equipment.TryEquipFromInventory(inv, resolve: null, itemId: rolledId, out var msg))
            {
                Debug.LogWarning($"[QA][Combat] Equip failed: {msg}");
                return;
            }

            Debug.Log($"[QA][Combat] Equipped test weapon ({label}) baseItem='{baseItem.id}' rolledId='{rolledId}' icon='{(baseItem.icon != null ? baseItem.icon.name : "(null)")}'");
        }

        [MenuItem(Root + "Spawn 1 Test Enemy (Trash)")]
        public static void SpawnOneTestEnemyTrash()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[QA][Combat] Enter Play Mode first.");
                return;
            }

            // Preferred: reuse the same spawn logic as the existing DevCheats hotkeys.
            DevCheats cheats = null;
            try { cheats = DevCheats.Instance; } catch { cheats = null; }

            if (cheats == null)
            {
                try
                {
#if UNITY_2023_1_OR_NEWER
                    cheats = Object.FindAnyObjectByType<DevCheats>(FindObjectsInactive.Exclude);
#else
                    cheats = Object.FindObjectOfType<DevCheats>();
#endif
                }
                catch { cheats = null; }
            }

            if (cheats != null)
            {
                try
                {
                    cheats.SpawnDamageTestEnemy();
                    Debug.Log("[QA][Combat] Spawned test enemy via DevCheats.");
                    return;
                }
                catch
                {
                    // Fall through to dummy spawn.
                }
            }

            // Fallback: spawn the standard dummy enemy prefab.
            var prefab = LoadDummyEnemyPrefab();
            if (prefab == null)
            {
                Debug.LogWarning("[QA][Combat] No DevCheats found and no dummy prefab found. Add DevCheats (with enemyPrefabs) or ensure Enemy_Dummy prefab exists.");
                return;
            }

            var anchor = FindAnchor();
            var basePos = anchor != null ? anchor.position : Vector3.zero;
            var forward = anchor != null ? anchor.forward : Vector3.forward;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;

            var pos = basePos + forward.normalized * 3.5f;
            var go = Object.Instantiate(prefab, pos, Quaternion.identity);
            go.name = "QA_AccuracyTest";

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

            Debug.Log("[QA][Combat] Spawned test enemy (fallback dummy).");
        }

        private static GameObject LoadDummyEnemyPrefab()
        {
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
                var player = Object.FindAnyObjectByType<PlayerHealth>(FindObjectsInactive.Exclude);
#else
                var player = Object.FindObjectOfType<PlayerHealth>();
#endif
                if (player != null) return player.transform;
            }
            catch { }

            if (Camera.main != null) return Camera.main.transform;
            return null;
        }
    }
}
#endif
