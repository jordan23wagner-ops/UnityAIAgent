using System;
using System.IO;
using Abyss.Items;
using Game.Systems;
using UnityEditor;
using UnityEngine;

namespace Abyssbound.EditorTools.Skilling
{
    public static class ToolItemsSetupMenu
    {
        private const string ResourcesFolder = "Assets/Resources";
        private const string ItemDefsFolder = "Assets/Resources/ItemDefinitions";

        private const string BasicPickaxeAssetPath = "Assets/Resources/ItemDefinitions/BasicPickaxe.asset";
        private const string BasicPickaxeId = "pickaxe_basic";

        private const string BasicFishingRodAssetPath = "Assets/Resources/ItemDefinitions/BasicFishingRod.asset";

        [MenuItem("Tools/Abyssbound/Skilling/Setup Basic Pickaxe")]
        public static void SetupBasicPickaxe()
        {
            EnsureFolder(ResourcesFolder);
            EnsureFolder(ItemDefsFolder);

            var def = CreateOrUpdateItem(
                BasicPickaxeAssetPath,
                itemId: BasicPickaxeId,
                displayName: "Basic Pickaxe",
                description: "A simple pickaxe for mining.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(def != null
                ? "[Skilling] Setup complete. Item def: BasicPickaxe"
                : "[Skilling] Setup failed. Item def: (missing)");
        }

        [MenuItem("Tools/Abyssbound/Skilling/Setup Pickaxe + Icon")]
        public static void SetupPickaxeAndIcon()
        {
            SetupBasicPickaxe();
            GenerateToolIcons.GeneratePickaxeIconAndAssign();
        }

        [MenuItem("Tools/Abyssbound/Skilling/Setup Basic Fishing Rod")]
        public static void SetupBasicFishingRod()
        {
            EnsureFolder(ResourcesFolder);
            EnsureFolder(ItemDefsFolder);

            var def = CreateOrUpdateItem(
                BasicFishingRodAssetPath,
                itemId: ItemIds.FishingRodBasic,
                displayName: "Basic Fishing Rod",
                description: "A simple fishing rod for fishing.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(def != null
                ? "[Skilling] Setup complete. Item def: BasicFishingRod"
                : "[Skilling] Setup failed. Item def: (missing)");
        }

        [MenuItem("Tools/Abyssbound/Skilling/Give Player Basic Pickaxe (Play Mode)")]
        public static void GivePlayerBasicPickaxe_PlayMode()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.Log("[Skilling] Give Player Basic Pickaxe requires Play Mode.");
                return;
            }

            var inv = PlayerInventoryResolver.GetOrFind();
            if (inv == null)
            {
                Debug.LogWarning("[Skilling] No PlayerInventory found.");
                return;
            }

            inv.Add(BasicPickaxeId, 1);
            Debug.Log("[Skilling] Gave player 1x Basic Pickaxe.");
        }

        [MenuItem("Tools/Abyssbound/Skilling/Give Player Basic Fishing Rod (Play Mode)")]
        public static void GivePlayerBasicFishingRod_PlayMode()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.Log("[Skilling] Give Player Basic Fishing Rod requires Play Mode.");
                return;
            }

            var inv = PlayerInventoryResolver.GetOrFind();
            if (inv == null)
            {
                Debug.LogWarning("[Skilling] No PlayerInventory found.");
                return;
            }

            inv.Add(ItemIds.FishingRodBasic, 1);
            Debug.Log("[Skilling] Gave player 1x Basic Fishing Rod.");
        }

        private static ItemDefinition CreateOrUpdateItem(string assetPath, string itemId, string displayName, string description)
        {
            var existing = AssetDatabase.LoadAssetAtPath<ItemDefinition>(assetPath);
            if (existing == null)
            {
                existing = ScriptableObject.CreateInstance<ItemDefinition>();
                AssetDatabase.CreateAsset(existing, assetPath);
            }

            if (!string.Equals(existing.itemId, itemId, StringComparison.OrdinalIgnoreCase))
            {
                var before = existing.itemId;
                existing.itemId = itemId;
                Debug.Log($"[Skilling] Updated itemId on '{assetPath}': '{before}' -> '{itemId}'");
            }

            existing.displayName = displayName;
            existing.description = description;
            existing.itemType = Abyss.Items.ItemType.Skilling;

            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static void EnsureFolder(string assetPath)
        {
            assetPath = assetPath.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(assetPath))
                return;

            var parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            var name = Path.GetFileName(assetPath);

            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
                return;

            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
