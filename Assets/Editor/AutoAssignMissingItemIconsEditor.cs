#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Abyss.Items;
using UnityEditor;
using UnityEngine;

public static class AutoAssignMissingItemIconsEditor
{
    [MenuItem("Tools/UI/Auto-Assign Missing Item Icons (Placeholders)")]
    public static void AutoAssignMissingItemIcons()
    {
        var placeholderByCategory = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase)
        {
            { "Consumable", LoadPlaceholderSprite("Icon_HealthPotion", "Assets/UI/Generated/Icons/Icon_HealthPotion.png") },
            { "Weapon",      LoadPlaceholderSprite("Icon_ManaPotion",   "Assets/UI/Generated/Icons/Icon_ManaPotion.png") },
            { "Equipment",   LoadPlaceholderSprite("Icon_ManaPotion",   "Assets/UI/Generated/Icons/Icon_ManaPotion.png") },
            { "Tool",        LoadPlaceholderSprite("Icon_TownScroll",   "Assets/UI/Generated/Icons/Icon_TownScroll.png") },
            { "Material",    LoadPlaceholderSprite("Icon_TownScroll",   "Assets/UI/Generated/Icons/Icon_TownScroll.png") },
            { "Generic",     LoadPlaceholderSprite("Icon_TownScroll",   "Assets/UI/Generated/Icons/Icon_TownScroll.png") },
        };

        var guids = AssetDatabase.FindAssets("t:ItemDefinition");
        int scanned = 0;
        int assigned = 0;

        var changes = new List<string>(256);

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrWhiteSpace(path))
                continue;

            var def = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (def == null)
                continue;

            scanned++;

            // Requirement: do NOT overwrite existing icons.
            if (def.icon != null)
                continue;

            var category = MapItemTypeToCategory(def.itemType);
            if (!placeholderByCategory.TryGetValue(category, out var placeholder) || placeholder == null)
                placeholderByCategory.TryGetValue("Generic", out placeholder);

            if (placeholder == null)
            {
                Debug.LogWarning($"[Auto-Assign Missing Item Icons] Missing placeholder sprite for itemType={def.itemType} category={category}. Skipping {path} (itemId='{def.itemId}', displayName='{def.displayName}').", def);
                continue;
            }

            def.icon = placeholder;
            EditorUtility.SetDirty(def);
            assigned++;
            changes.Add($"UPDATED: {path} | itemId='{def.itemId}' | itemType={def.itemType} | placeholder={placeholder.name}");
        }

        if (assigned > 0)
            AssetDatabase.SaveAssets();

        var summary = $"[Auto-Assign Missing Item Icons] Scanned={scanned} AutoAssigned={assigned}";
        if (changes.Count == 0)
        {
            Debug.Log(summary);
        }
        else
        {
            Debug.Log(summary + "\n" + string.Join("\n", changes));
        }
    }

    private static string MapItemTypeToCategory(Abyss.Items.ItemType itemType)
    {
        // Map differing enum values to the closest logical category.
        // Current enum is Abyss.Items.ItemType, but keep string-based mapping resilient.
        var name = itemType.ToString();
        if (name.IndexOf("Consum", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Consumable";
        if (name.IndexOf("Weapon", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Weapon";
        if (name.IndexOf("Equip", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Armor", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Equipment";
        if (name.IndexOf("Tool", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Tool";
        if (name.IndexOf("Mat", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Material";

        return "Generic";
    }

    private static Sprite LoadPlaceholderSprite(string spriteName, string preferredAssetPath)
    {
        // 1) Prefer known generated icon paths.
        if (!string.IsNullOrWhiteSpace(preferredAssetPath))
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(preferredAssetPath);
            if (sprite != null)
                return sprite;
        }

        // 2) Fallback: find any Sprite asset by name.
        if (!string.IsNullOrWhiteSpace(spriteName))
        {
            var guids = AssetDatabase.FindAssets($"{spriteName} t:Sprite");
            if (guids != null && guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null)
                    return sprite;
            }
        }

        return null;
    }
}
#endif
