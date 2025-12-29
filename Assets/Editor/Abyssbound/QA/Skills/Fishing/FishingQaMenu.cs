using System.Collections.Generic;
using Abyssbound.Skills.Fishing;
using Abyssbound.Stats;
using Game.Systems;
using UnityEditor;
using UnityEngine;

// NOTE: Legacy/QA editor tools are hidden unless ABYSS_LEGACY_QA_TOOLS is defined.
// Enable via Project Settings > Player > Scripting Define Symbols.

namespace Abyssbound.EditorTools.QA.Skills.Fishing
{
    public static class FishingQaMenu
    {
        private const string ConfigAssetPath = "Assets/Resources/Skills/Fishing/FishingSkillConfig.asset";

        // Old menu path: Tools/Abyssbound/QA/Skills/Fishing/Create Default Config Asset
    #if ABYSS_LEGACY_QA_TOOLS
        [MenuItem("Tools/Legacy QA/Skills/Fishing/Create Default Config Asset")]
        public static void CreateDefaultConfigAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<FishingSkillConfigSO>(ConfigAssetPath);
            if (existing != null)
            {
                EditorGUIUtility.PingObject(existing);
                Selection.activeObject = existing;
                return;
            }

            var cfg = ScriptableObject.CreateInstance<FishingSkillConfigSO>();
            cfg.primarySkill = Abyssbound.Loot.StatType.Fishing;
            cfg.requiredToolItemId = "tool_fishing_rod";
            cfg.potSecondsPerCatch = 5f;
            cfg.potMaxStoredCatches = 12;

            cfg.tiers = new List<FishingSkillConfigSO.FishingTier>
            {
                new FishingSkillConfigSO.FishingTier
                {
                    id = "T1",
                    requiredFishingLevel = 1,
                    actionSeconds = 2.0f,
                    yieldItemId = "fish_raw_shrimp",
                    yieldAmount = 1,
                    actionXp = 5,
                    yieldXp = 0,
                    awardSecondaryXp = false,
                },
                new FishingSkillConfigSO.FishingTier
                {
                    id = "T2",
                    requiredFishingLevel = 5,
                    actionSeconds = 2.5f,
                    yieldItemId = "fish_raw_trout",
                    yieldAmount = 1,
                    actionXp = 8,
                    yieldXp = 0,
                    awardSecondaryXp = false,
                },
            };

            AssetDatabase.CreateAsset(cfg, ConfigAssetPath);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(cfg);
            Selection.activeObject = cfg;
        }

        // Old menu path: Tools/Abyssbound/QA/Skills/Fishing/Spawn Fishing Spot (T1)
        [MenuItem("Tools/Legacy QA/Skills/Fishing/Spawn Fishing Spot (T1)")]
        public static void SpawnFishingSpotT1() => SpawnSpot(0);

        // Old menu path: Tools/Abyssbound/QA/Skills/Fishing/Spawn Fishing Spot (T2)
        [MenuItem("Tools/Legacy QA/Skills/Fishing/Spawn Fishing Spot (T2)")]
        public static void SpawnFishingSpotT2() => SpawnSpot(1);

        // Old menu path: Tools/Abyssbound/QA/Skills/Fishing/Spawn Fishing Pot (T1)
        [MenuItem("Tools/Legacy QA/Skills/Fishing/Spawn Fishing Pot (T1)")]
        public static void SpawnFishingPotT1() => SpawnPot(0);

        // Old menu path: Tools/Abyssbound/QA/Skills/Fishing/Grant Fishing Rod
        [MenuItem("Tools/Legacy QA/Skills/Fishing/Grant Fishing Rod")]
        public static void GrantFishingRod()
        {
            var inv = PlayerInventoryResolver.GetOrFind();
            if (inv == null)
            {
                Debug.LogError("[QA][Fishing] No PlayerInventory found.");
                return;
            }

            inv.Add("tool_fishing_rod", 1);
        }

        // Old menu path: Tools/Abyssbound/QA/Skills/Fishing/Print Fishing Level & XP
        [MenuItem("Tools/Legacy QA/Skills/Fishing/Print Fishing Level & XP")]
        public static void PrintFishingLevel()
        {
            var stats = FindStats();
            if (stats == null)
            {
                Debug.LogError("[QA][Fishing] No PlayerStatsRuntime found.");
                return;
            }

            int lvl = stats.GetLevel(Abyssbound.Loot.StatType.Fishing);
            int xp = stats.GetXp(Abyssbound.Loot.StatType.Fishing);
            Debug.Log($"[QA][Fishing] Fishing: level={lvl} xp={xp}");
        }

        // Old menu path: Tools/Abyssbound/QA/Skills/Fishing/Fill Inventory To Max Slots
        [MenuItem("Tools/Legacy QA/Skills/Fishing/Fill Inventory To Max Slots")]
        public static void FillInventoryToMaxSlots()
        {
            var inv = PlayerInventoryResolver.GetOrFind();
            if (inv == null)
            {
                Debug.LogError("[QA][Fishing] No PlayerInventory found.");
                return;
            }

            int target = inv.GetMaxInventorySlots();
            int cur = inv.GetStackCount();
            int toAdd = Mathf.Max(0, target - cur);

            for (int i = 0; i < toAdd; i++)
                inv.Add($"qa_fill_{System.Guid.NewGuid():N}", 1);

            Debug.Log($"[QA][Fishing] Inventory stacks: {inv.GetStackCount()}/{inv.GetMaxInventorySlots()} (added {toAdd}).");
        }

        private static void SpawnSpot(int tierIndex)
        {
            var cfg = EnsureConfig();
            if (cfg == null) return;

            var go = new GameObject($"FishingSpot_{tierIndex}");
            Undo.RegisterCreatedObjectUndo(go, "Spawn Fishing Spot");

            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 1f, 0f);
            col.size = new Vector3(1.5f, 2f, 1.5f);

            var spot = go.AddComponent<FishingSpot>();
            SetPrivateField(spot, "config", cfg);
            SetPrivateField(spot, "tierIndex", tierIndex);

            PlaceNearSceneView(go);
            Selection.activeObject = go;
        }

        private static void SpawnPot(int tierIndex)
        {
            var cfg = EnsureConfig();
            if (cfg == null) return;

            var go = new GameObject($"FishingPot_{tierIndex}");
            Undo.RegisterCreatedObjectUndo(go, "Spawn Fishing Pot");

            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 0.5f, 0f);
            col.size = new Vector3(1.0f, 1.0f, 1.0f);

            var pot = go.AddComponent<FishingPot>();
            SetPrivateField(pot, "config", cfg);
            SetPrivateField(pot, "tierIndex", tierIndex);

            PlaceNearSceneView(go);
            Selection.activeObject = go;
        }

        private static FishingSkillConfigSO EnsureConfig()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<FishingSkillConfigSO>(ConfigAssetPath);
            if (cfg != null) return cfg;

            CreateDefaultConfigAsset();
            return AssetDatabase.LoadAssetAtPath<FishingSkillConfigSO>(ConfigAssetPath);
        }

        private static PlayerStatsRuntime FindStats()
        {
            try
            {
#if UNITY_2022_2_OR_NEWER
                return Object.FindFirstObjectByType<PlayerStatsRuntime>(FindObjectsInactive.Exclude);
#else
                return Object.FindObjectOfType<PlayerStatsRuntime>();
#endif
            }
            catch
            {
                return null;
            }
        }

        private static void PlaceNearSceneView(GameObject go)
        {
            if (go == null) return;

            var view = SceneView.lastActiveSceneView;
            if (view != null && view.camera != null)
            {
                var t = go.transform;
                t.position = view.camera.transform.position + view.camera.transform.forward * 6f;
                t.position = new Vector3(t.position.x, 0f, t.position.z);
                return;
            }

            go.transform.position = Vector3.zero;
        }

        private static void SetPrivateField(object obj, string fieldName, object value)
        {
            if (obj == null || string.IsNullOrWhiteSpace(fieldName)) return;
            var t = obj.GetType();
            var f = t.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (f == null) return;
            f.SetValue(obj, value);
        }

#endif
    }
}
