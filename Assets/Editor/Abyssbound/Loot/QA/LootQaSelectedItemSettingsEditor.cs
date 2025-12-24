#if UNITY_EDITOR
using System;
using System.Reflection;
using Abyssbound.Loot;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class LootQaSelectedItemSettingsEditor
{
    private const string ResourcesFolder = "Assets/Resources";
    private const string AssetPath = "Assets/Resources/LootQaSelectedItemSettings.asset";

    [InitializeOnLoadMethod]
    private static void EnsureAssetExistsOnLoad()
    {
        // Keep it cheap: only create if missing.
        if (AssetDatabase.LoadAssetAtPath<LootQaSelectedItemSettingsSO>(AssetPath) != null)
            return;

        EnsureAssetExists();
    }

    [MenuItem("Tools/Abyssbound/QA/Selected Item/Create Settings Asset")]
    public static void EnsureAssetExists()
    {
        if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            AssetDatabase.CreateFolder("Assets", "Resources");

        var existing = AssetDatabase.LoadAssetAtPath<LootQaSelectedItemSettingsSO>(AssetPath);
        if (existing != null)
        {
            EditorGUIUtility.PingObject(existing);
            Selection.activeObject = existing;
            return;
        }

        var asset = ScriptableObject.CreateInstance<LootQaSelectedItemSettingsSO>();
        AssetDatabase.CreateAsset(asset, AssetPath);
        AssetDatabase.SaveAssets();

        Debug.Log($"[LootQA] Created settings asset at '{AssetPath}'.", asset);
        EditorGUIUtility.PingObject(asset);
        Selection.activeObject = asset;
    }

    [MenuItem("Tools/Abyssbound/QA/Selected Item/Ping Settings Asset")]
    public static void PingSettingsAsset()
    {
        EnsureAssetExists();
        var settings = AssetDatabase.LoadAssetAtPath<LootQaSelectedItemSettingsSO>(AssetPath);
        if (settings == null)
        {
            Debug.LogWarning($"[LootQA] Settings asset missing at '{AssetPath}'. Use: Tools/Abyssbound/QA/Selected Item/Create Settings Asset");
            return;
        }

        Selection.activeObject = settings;
        EditorGUIUtility.PingObject(settings);
    }

    [MenuItem("Tools/Abyssbound/QA/Selected Item/Set Selected From Project Selection")]
    public static void SetSelectedFromProjectSelection()
    {
        EnsureAssetExists();

        var settings = AssetDatabase.LoadAssetAtPath<LootQaSelectedItemSettingsSO>(AssetPath);
        if (settings == null)
            return;

        var selection = Selection.activeObject;
        if (selection == null)
        {
            Debug.LogWarning("[LootQA] Selection is null. Select an ItemDefinitionSO asset in Project, or select a scene object that references one.");
            return;
        }

        if (!TryResolveSupportedItemFromSelection(selection, out var resolved))
        {
            Debug.LogWarning("[LootQA] Unsupported selection. Select an ItemDefinitionSO (asset) OR select a scene object/component that has an ItemDefinitionSO reference.");
            return;
        }

        settings.selectedItemDefinition = resolved;
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();

        Debug.Log($"[LootQA] Selected QA item set to: {resolved.name}", settings);
    }

    [MenuItem("Tools/Abyssbound/QA/Selected Item/Clear Selected")]
    public static void ClearSelected()
    {
        EnsureAssetExists();
        var settings = AssetDatabase.LoadAssetAtPath<LootQaSelectedItemSettingsSO>(AssetPath);
        if (settings == null)
            return;

        settings.selectedItemDefinition = null;
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();

        Debug.Log("[LootQA] Cleared Selected QA item.", settings);
    }

    [MenuItem("Tools/Abyssbound/QA/Selected Item/Set Default From Project Selection")]
    public static void SetDefaultFromProjectSelection()
    {
        EnsureAssetExists();

        var settings = AssetDatabase.LoadAssetAtPath<LootQaSelectedItemSettingsSO>(AssetPath);
        if (settings == null)
            return;

        var selection = Selection.activeObject;
        if (selection == null)
        {
            Debug.LogWarning("[LootQA] Selection is null. Select an ItemDefinitionSO asset in Project, or select a scene object that references one.");
            return;
        }

        if (!TryResolveSupportedItemFromSelection(selection, out var resolved))
        {
            Debug.LogWarning("[LootQA] Unsupported selection. Select an ItemDefinitionSO (asset) OR select a scene object/component that has an ItemDefinitionSO reference.");
            return;
        }

        settings.defaultSelectedItemDefinition = resolved;
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();

        Debug.Log($"[LootQA] Default QA item set to: {resolved.name}", settings);
    }

    [MenuItem("Tools/Abyssbound/QA/Selected Item/Ping Any ItemDefinitionSO")]
    public static void PingAnyItemDefinitionSo()
    {
        var obj = FindFirstAssetByType("t:ItemDefinitionSO");
        if (obj == null)
        {
            Debug.LogWarning("[LootQA] No ItemDefinitionSO assets found. Create one via: Create/Abyssbound/Loot/Item Definition.");
            return;
        }

        Selection.activeObject = obj;
        EditorGUIUtility.PingObject(obj);
        Debug.Log($"[LootQA] Pinged ItemDefinitionSO asset: {obj.name}", obj);
    }

    [MenuItem("Tools/Abyssbound/QA/Selected Item/Auto-Set Selected (First ItemDefinitionSO)")]
    public static void AutoSetSelectedFirstItemDefinitionSo()
    {
        EnsureAssetExists();
        var settings = AssetDatabase.LoadAssetAtPath<LootQaSelectedItemSettingsSO>(AssetPath);
        if (settings == null)
            return;

        var obj = FindFirstAssetByType("t:ItemDefinitionSO") ?? FindFirstAssetByType("t:ItemDefinition");
        if (obj == null)
        {
            Debug.LogWarning("[LootQA] No ItemDefinitionSO or ItemDefinition assets found.");
            return;
        }

        settings.selectedItemDefinition = obj;
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();

        Selection.activeObject = obj;
        EditorGUIUtility.PingObject(obj);
        Debug.Log($"[LootQA] Auto-set Selected QA item to: {obj.name}", settings);
    }

    [MenuItem("Tools/Abyssbound/QA/Selected Item/Auto-Set Selected + Default (First ItemDefinitionSO)")]
    public static void AutoSetSelectedAndDefaultFirstItemDefinitionSo()
    {
        EnsureAssetExists();
        var settings = AssetDatabase.LoadAssetAtPath<LootQaSelectedItemSettingsSO>(AssetPath);
        if (settings == null)
            return;

        var obj = FindFirstAssetByType("t:ItemDefinitionSO") ?? FindFirstAssetByType("t:ItemDefinition");
        if (obj == null)
        {
            Debug.LogWarning("[LootQA] No ItemDefinitionSO or ItemDefinition assets found.");
            return;
        }

        settings.selectedItemDefinition = obj;
        settings.defaultSelectedItemDefinition = obj;
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();

        Selection.activeObject = obj;
        EditorGUIUtility.PingObject(obj);
        Debug.Log($"[LootQA] Auto-set Selected+Default QA item to: {obj.name}", settings);
    }

    [MenuItem("Tools/Abyssbound/QA/Selected Item/Ping Selected Item")]
    public static void PingSelectedItem()
    {
        EnsureAssetExists();
        var settings = AssetDatabase.LoadAssetAtPath<LootQaSelectedItemSettingsSO>(AssetPath);
        if (settings == null)
            return;

        var selected = settings.selectedItemDefinition;
        if (selected == null)
        {
            Debug.LogWarning("[LootQA] Selected QA item is null. Use: Tools/Abyssbound/QA/Selected Item/Set Selected From Project Selection (or Auto-Set Selected...).", settings);
            return;
        }

        Selection.activeObject = selected;
        EditorGUIUtility.PingObject(selected);
        Debug.Log($"[LootQA] Pinged Selected QA item: {selected.name}", selected);
    }

    private static bool IsSupportedSelection(Object o)
    {
        if (o == null) return false;
        return o is ItemDefinitionSO || o.GetType().FullName == "Abyss.Items.ItemDefinition";
    }

    private static bool TryResolveSupportedItemFromSelection(Object selection, out Object resolved)
    {
        resolved = null;
        if (selection == null)
            return false;

        if (IsSupportedSelection(selection))
        {
            resolved = selection;
            return true;
        }

        // Common workflow: user clicked something in the Hierarchy.
        if (selection is GameObject go)
            return TryResolveFromGameObject(go, out resolved);

        if (selection is Component c)
            return TryResolveFromGameObject(c.gameObject, out resolved);

        return false;
    }

    private static bool TryResolveFromGameObject(GameObject go, out Object resolved)
    {
        resolved = null;
        if (go == null)
            return false;

        try
        {
            var comps = go.GetComponents<Component>();
            if (comps == null || comps.Length == 0)
                return false;

            for (int i = 0; i < comps.Length; i++)
            {
                var comp = comps[i];
                if (comp == null) continue;

                if (TryResolveFromObjectFieldsOrProps(comp, out resolved))
                    return true;
            }
        }
        catch { }

        return false;
    }

    private static bool TryResolveFromObjectFieldsOrProps(object obj, out Object resolved)
    {
        resolved = null;
        if (obj == null)
            return false;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var t = obj.GetType();

        try
        {
            var fields = t.GetFields(flags);
            for (int i = 0; i < fields.Length; i++)
            {
                var f = fields[i];
                if (f == null) continue;

                // Skip obviously irrelevant primitives/structs quickly.
                var ft = f.FieldType;
                if (ft == null) continue;

                // Direct supported type.
                if (ft == typeof(ItemDefinitionSO))
                {
                    if (f.GetValue(obj) is Object o && o != null)
                    {
                        resolved = o;
                        return true;
                    }
                }

                // Legacy supported type by FullName check (avoid hard reference).
                if (ft.FullName == "Abyss.Items.ItemDefinition")
                {
                    if (f.GetValue(obj) is Object o && o != null)
                    {
                        resolved = o;
                        return true;
                    }
                }
            }
        }
        catch { }

        // Properties are less common for serialized refs, but try a few safe ones.
        try
        {
            var props = t.GetProperties(flags);
            for (int i = 0; i < props.Length; i++)
            {
                var p = props[i];
                if (p == null) continue;
                if (!p.CanRead) continue;
                if (p.GetIndexParameters() != null && p.GetIndexParameters().Length > 0) continue;

                var pt = p.PropertyType;
                if (pt == typeof(ItemDefinitionSO) || pt.FullName == "Abyss.Items.ItemDefinition")
                {
                    object v = null;
                    try { v = p.GetValue(obj, null); } catch { v = null; }
                    if (v is Object o && o != null)
                    {
                        resolved = o;
                        return true;
                    }
                }
            }
        }
        catch { }

        return false;
    }

    private static Object FindFirstAssetByType(string filter)
    {
        try
        {
            var guids = AssetDatabase.FindAssets(filter);
            if (guids == null || guids.Length == 0)
                return null;

            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                var obj = AssetDatabase.LoadAssetAtPath<Object>(path);
                if (obj != null)
                    return obj;
            }
        }
        catch { }

        return null;
    }
}
#endif
