using System.IO;
using Abyss.Items;
using UnityEditor;
using UnityEngine;

namespace Abyssbound.EditorTools.Skilling
{
    public static class SkillingItemsSetupMenu
    {
        private const string ResourcesFolder = "Assets/Resources";
        private const string ItemDefsFolder = "Assets/Resources/ItemDefinitions";

        private const string CopperOreAssetPath = "Assets/Resources/ItemDefinitions/CopperOre.asset";
        private const string CopperBarAssetPath = "Assets/Resources/ItemDefinitions/CopperBar.asset";

        [MenuItem("Tools/Abyssbound/Skilling/Setup Copper Items (Ore + Bar)")]
        public static void SetupCopperItems()
        {
            EnsureFolder(ResourcesFolder);
            EnsureFolder(ItemDefsFolder);

            var ore = CreateOrUpdateItem(
                CopperOreAssetPath,
                itemId: "copper_ore",
                displayName: "Copper Ore",
                description: "A chunk of copper-bearing ore.");

            var bar = CreateOrUpdateItem(
                CopperBarAssetPath,
                itemId: "copper_bar",
                displayName: "Copper Bar",
                description: "A bar of smelted copper.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Skilling] Setup complete. Item defs: {(ore != null ? "CopperOre" : "(missing)")}, {(bar != null ? "CopperBar" : "(missing)")}");
        }

        [MenuItem("Tools/Abyssbound/Skilling/Generate Copper Icons")]
        public static void GenerateCopperIcons()
        {
            GenerateSkillingIcons.GenerateCopperIconsAndAssign();
        }

        [MenuItem("Tools/Abyssbound/Skilling/Setup Copper Items + Icons")]
        public static void SetupCopperItemsAndIcons()
        {
            SetupCopperItems();
            GenerateCopperIcons();
        }

        private static ItemDefinition CreateOrUpdateItem(string assetPath, string itemId, string displayName, string description)
        {
            var existing = AssetDatabase.LoadAssetAtPath<ItemDefinition>(assetPath);
            if (existing == null)
            {
                existing = ScriptableObject.CreateInstance<ItemDefinition>();
                AssetDatabase.CreateAsset(existing, assetPath);
            }

            if (!string.Equals(existing.itemId, itemId, System.StringComparison.OrdinalIgnoreCase))
            {
                var before = existing.itemId;
                existing.itemId = itemId;
                Debug.Log($"[Skilling] Updated itemId on '{assetPath}': '{before}' -> '{itemId}'");
            }
            existing.displayName = displayName;
            existing.itemType = Abyss.Items.ItemType.Skilling;
            existing.description = description;

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
