#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Abyssbound.Loot;
using UnityEditor;
using UnityEngine;

namespace Abyssbound.Editor.QA.Items
{
    public static class ItemsQaMenu
    {
        private const string Root = "Tools/Abyssbound/QA/Items/";

        private const string MeleeWeaponPath = "Assets/Resources/Loot/Items/Item_Starter_Sword.asset";
        private const string RangedWeaponPath = "Assets/Resources/Loot/Items/Item_Starter_Bow.asset";
        private const string MagicWeaponPath = "Assets/Resources/Loot/Items/Item_QA_Staff_2H.asset";

        [MenuItem(Root + "Spawn Test Weapons (Melee/Ranged/Magic)")]
        public static void SpawnTestWeapons_MeleeRangedMagic()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[QA][Items] Enter Play Mode first.");
                return;
            }

            var melee = LoadItem(MeleeWeaponPath);
            var ranged = LoadItem(RangedWeaponPath);
            var magic = LoadItem(MagicWeaponPath);

            var registry = LootRegistryRuntime.GetOrCreate();
            registry.BuildIfNeeded();

            SpawnOneAndLog(melee, registry, label: "Melee");
            SpawnOneAndLog(ranged, registry, label: "Ranged");
            SpawnOneAndLog(magic, registry, label: "Magic");
        }

        private static ItemDefinitionSO LoadItem(string assetPath)
        {
            try
            {
                var so = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(assetPath);
                if (so == null)
                    Debug.LogWarning($"[QA][Items] Missing ItemDefinitionSO at '{assetPath}'.");
                return so;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[QA][Items] Failed to load '{assetPath}': {ex.Message}");
                return null;
            }
        }

        private static void SpawnOneAndLog(ItemDefinitionSO baseItem, LootRegistryRuntime registry, string label)
        {
            if (baseItem == null || registry == null)
                return;

            // Spawn a single Common roll so we exercise the same ItemInstance -> LootRegistryRuntime display path.
            var rolledIds = new List<string>(1);
            LootQaSpawnHelper.SpawnSelectedItemForRarityIds(
                selection: baseItem,
                rarityIds: new[] { "Common" },
                itemLevel: 1,
                perItemLogs: false,
                logPrefix: "[QA][Items]",
                outRolledIds: rolledIds
            );

            string baseItemPath = SafeAssetPath(baseItem);
            string baseIconName = SafeName(baseItem.icon);
            string baseIconPath = SafeAssetPath(baseItem.icon);

            Debug.Log($"[QA][Items] {label} baseItem id='{baseItem.id}' name='{baseItem.name}' asset='{baseItemPath}' icon='{baseIconName}' iconAsset='{baseIconPath}'");

            if (rolledIds.Count > 0)
            {
                var rolledId = rolledIds[0];
                if (registry.TryResolveDisplay(rolledId, out var displayName, out var icon) && icon != null)
                {
                    Debug.Log($"[QA][Items] {label} rolledId='{rolledId}' displayName='{displayName}' resolvedIcon='{SafeName(icon)}' resolvedIconAsset='{SafeAssetPath(icon)}'");
                }
                else
                {
                    Debug.LogWarning($"[QA][Items] {label} rolledId='{rolledId}' display resolve failed (or no icon). Base item likely not registered or icon missing.");
                }
            }
        }

        private static string SafeName(UnityEngine.Object obj)
        {
            try { return obj != null ? obj.name : "(null)"; }
            catch { return "(error)"; }
        }

        private static string SafeAssetPath(UnityEngine.Object obj)
        {
            try { return obj != null ? AssetDatabase.GetAssetPath(obj) : "(null)"; }
            catch { return "(error)"; }
        }
    }
}
#endif
