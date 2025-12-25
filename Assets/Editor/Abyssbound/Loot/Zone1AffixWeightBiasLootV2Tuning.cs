#if UNITY_EDITOR
using System;
using Abyssbound.Loot;
using UnityEditor;
using UnityEngine;

public static class Zone1AffixWeightBiasLootV2Tuning
{
    private const string MenuPath = "Tools/Abyssbound/Loot/Tune Zone1 Affix Weights (Early Game)";

    private const string AffixPowerPath = "Assets/Resources/Loot/Affixes/Affix_Power.asset";
    private const string AffixPrecisionPath = "Assets/Resources/Loot/Affixes/Affix_Precision.asset";
    private const string AffixSorceryPath = "Assets/Resources/Loot/Affixes/Affix_Sorcery.asset";
    private const string AffixFuryPath = "Assets/Resources/Loot/Affixes/Affix_Fury.asset";

    private const string AffixBulwarkPath = "Assets/Resources/Loot/Affixes/Affix_Bulwark.asset";
    private const string AffixFortitudePath = "Assets/Resources/Loot/Affixes/Affix_Fortitude.asset";
    private const string AffixSwiftnessPath = "Assets/Resources/Loot/Affixes/Affix_Swiftness.asset";

    [MenuItem(MenuPath)]
    public static void Apply()
    {
        // Only edit affix weights; do not touch pools, tiers, tags, or item-level logic.
        var power = LoadAffixOrWarn(AffixPowerPath);
        var precision = LoadAffixOrWarn(AffixPrecisionPath);
        var sorcery = LoadAffixOrWarn(AffixSorceryPath);
        var fury = LoadAffixOrWarn(AffixFuryPath);

        var bulwark = LoadAffixOrWarn(AffixBulwarkPath);
        var fortitude = LoadAffixOrWarn(AffixFortitudePath);
        var swiftness = LoadAffixOrWarn(AffixSwiftnessPath);

        int changed = 0;
        changed += SetWeightIfDifferent(power, 10);
        changed += SetWeightIfDifferent(precision, 10);
        changed += SetWeightIfDifferent(sorcery, 10);
        changed += SetWeightIfDifferent(fury, 2);

        changed += SetWeightIfDifferent(bulwark, 10);
        changed += SetWeightIfDifferent(fortitude, 6);
        changed += SetWeightIfDifferent(swiftness, 1);

        if (changed > 0)
        {
            AssetDatabase.SaveAssets();
        }

        Debug.Log("[Loot V2] Zone1 affix weights set (changed=" + changed + "): Power 10, Precision 10, Sorcery 10, Fury 2, Bulwark 10, Fortitude 6, Swiftness 1");
    }

    private static AffixDefinitionSO LoadAffixOrWarn(string path)
    {
        var affix = AssetDatabase.LoadAssetAtPath<AffixDefinitionSO>(path);
        if (affix == null)
            Debug.LogWarning("[Loot V2] Missing affix at: " + path);
        return affix;
    }

    private static int SetWeightIfDifferent(AffixDefinitionSO affix, int newWeight)
    {
        if (affix == null) return 0;

        newWeight = Mathf.Max(0, newWeight);
        if (affix.weight == newWeight) return 0;

        int old = affix.weight;
        affix.weight = newWeight;

        EditorUtility.SetDirty(affix);
        Debug.Log("[Loot V2] Affix weight updated: " + SafeId(affix) + " " + old + " -> " + newWeight);
        return 1;
    }

    private static string SafeId(AffixDefinitionSO affix)
    {
        if (affix == null) return "<null>";
        try
        {
            if (!string.IsNullOrWhiteSpace(affix.id)) return affix.id;
        }
        catch
        {
            // ignored
        }

        return affix.name;
    }
}
#endif
