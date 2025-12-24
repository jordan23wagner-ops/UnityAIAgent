using System;
using Abyss.Items;
using UnityEditor;
using UnityEngine;

using AbyssItemRarity = Abyss.Items.ItemRarity;

public static class AssignTestRaritiesEditor
{
    [MenuItem("Tools/Abyssbound/QA/Items/Assign Test Rarities (Non-destructive)")]
    public static void AssignTestRarities()
    {
        var guids = AssetDatabase.FindAssets("t:ItemDefinition");
        int changed = 0;
        int scanned = 0;
        const int maxList = 25;
        var changedNames = new System.Collections.Generic.List<string>(maxList);

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var def = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (def == null) continue;
            scanned++;

            var desired = TryGetDesiredRarity(def);
            if (!desired.HasValue) continue;

            var normalized = ItemRarityVisuals.Normalize(desired.Value);
            if (def.rarity == normalized) continue;

            Undo.RecordObject(def, "Assign Test Rarities");
            def.rarity = normalized;
            EditorUtility.SetDirty(def);
            changed++;

            if (changedNames.Count < maxList)
                changedNames.Add(def.name);
        }

        if (changed > 0)
            AssetDatabase.SaveAssets();

        var details = changedNames.Count > 0 ? $" Changed (up to {maxList}): {string.Join(", ", changedNames)}" : string.Empty;
        Debug.Log($"[AssignTestRarities] Scanned {scanned} ItemDefinition asset(s). Updated {changed}. (Bronze Sword/Common, Training Bow/Uncommon, Apprentice Staff/Rare){details}");
    }

    private static AbyssItemRarity? TryGetDesiredRarity(ItemDefinition def)
    {
        string id = def.itemId ?? string.Empty;
        string dn = def.displayName ?? string.Empty;
        string an = def.name ?? string.Empty;

        if (MatchesAny(id, dn, an, "Bronze Sword")) return AbyssItemRarity.Common;
        if (MatchesAny(id, dn, an, "Training Bow")) return AbyssItemRarity.Uncommon;
        if (MatchesAny(id, dn, an, "Apprentice Staff")) return AbyssItemRarity.Rare;

        return null;
    }

    private static bool MatchesAny(string a, string b, string c, string needle)
    {
        return ContainsIgnoreCase(a, needle) || ContainsIgnoreCase(b, needle) || ContainsIgnoreCase(c, needle);
    }

    private static bool ContainsIgnoreCase(string haystack, string needle)
    {
        return haystack?.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
