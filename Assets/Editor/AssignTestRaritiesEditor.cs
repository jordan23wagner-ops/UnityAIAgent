using System;
using Abyss.Items;
using UnityEditor;
using UnityEngine;

using AbyssItemRarity = Abyss.Items.ItemRarity;

public static class AssignTestRaritiesEditor
{
    [MenuItem("Abyss/Items/Assign Test Rarities (Non-destructive)")]
    public static void AssignTestRarities()
    {
        var guids = AssetDatabase.FindAssets("t:ItemDefinition");
        int changed = 0;

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var def = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (def == null) continue;

            var desired = TryGetDesiredRarity(def);
            if (!desired.HasValue) continue;

            var normalized = ItemRarityVisuals.Normalize(desired.Value);
            if (def.rarity == normalized) continue;

            Undo.RecordObject(def, "Assign Test Rarities");
            def.rarity = normalized;
            EditorUtility.SetDirty(def);
            changed++;

            Debug.Log($"[AssignTestRarities] {def.name} (itemId='{def.itemId}', displayName='{def.displayName}') => {def.rarity}");
        }

        if (changed > 0)
            AssetDatabase.SaveAssets();

        Debug.Log($"[AssignTestRarities] Updated {changed} ItemDefinition asset(s). (Bronze Sword/Common, Training Bow/Uncommon, Apprentice Staff/Rare)");
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
