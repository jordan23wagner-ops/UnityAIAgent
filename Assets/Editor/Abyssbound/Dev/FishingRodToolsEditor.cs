#if UNITY_EDITOR
using System;
using Abyss.Items;
using Game.Systems;
using UnityEditor;
using UnityEngine;

namespace Abyssbound.EditorTools.Dev
{
    public static class FishingRodToolsEditor
    {
        private const string FishingRodItemId = ItemIds.FishingRodBasic;

        // Canonical ItemDefinition location (single source of truth).
        private const string RodItemAssetPath = "Assets/Resources/ItemDefinitions/BasicFishingRod.asset";

        [MenuItem("Tools/Abyssbound/Dev/Items/Grant Fishing Rod (Play Mode)")]
        public static void GrantFishingRodPlayMode()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Dev][FishingRod] Enter Play Mode to grant items to the player inventory.");
                return;
            }

            var inv = PlayerInventoryResolver.GetOrFind();
            if (inv == null)
            {
                Debug.LogError("[Dev][FishingRod] No PlayerInventory found.");
                return;
            }

            inv.Add(FishingRodItemId, 1);
            Debug.Log($"[Dev][FishingRod] Granted 1x {FishingRodItemId}");
        }

        [MenuItem("Tools/Abyssbound/Dev/Items/Ensure Fishing Rod ItemDefinition (Editor)")]
        public static void EnsureFishingRodItemDefinition_Editor()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[Dev][FishingRod] Run this in Edit Mode (not Play Mode). It edits assets.");
                return;
            }

            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Resources/ItemDefinitions");

            var rodDef = FindItemDefinitionById(FishingRodItemId);
            if (rodDef == null)
            {
                rodDef = ScriptableObject.CreateInstance<ItemDefinition>();
                rodDef.itemId = FishingRodItemId;
                rodDef.displayName = "Fishing Rod";
                rodDef.description = "Used for fishing.";
                rodDef.itemType = Abyss.Items.ItemType.Skilling;
                rodDef.baseValue = 65;
                rodDef.rarity = Abyss.Items.ItemRarity.Common;

                AssetDatabase.CreateAsset(rodDef, RodItemAssetPath);
                EditorUtility.SetDirty(rodDef);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[Dev][FishingRod] Created ItemDefinition asset at '{RodItemAssetPath}' itemId={FishingRodItemId}");
                return;
            }

            bool changed = false;
            try
            {
                if (!string.Equals(rodDef.itemId, FishingRodItemId, StringComparison.Ordinal))
                {
                    rodDef.itemId = FishingRodItemId;
                    changed = true;
                }
            }
            catch { }

            if (changed)
            {
                EditorUtility.SetDirty(rodDef);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log($"[Dev][FishingRod] ItemDefinition present. itemId={rodDef.itemId} (no shop/merchant systems used)");
        }

        private static ItemDefinition FindItemDefinitionById(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return null;

            try
            {
                var guids = AssetDatabase.FindAssets("t:ItemDefinition");
                foreach (var g in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(g);
                    var def = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                    if (def == null) continue;

                    if (!string.IsNullOrWhiteSpace(def.itemId) &&
                        string.Equals(def.itemId, itemId, StringComparison.OrdinalIgnoreCase))
                        return def;
                }
            }
            catch { }

            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parts = path.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
#endif
