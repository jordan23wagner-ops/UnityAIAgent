using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Abyssbound.Loot;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AbyssboundLootSetupMenu
{
    private const string RootFolder = "Assets/GameData/Loot";
    private const string TierBucketsFolder = "Assets/GameData/Loot/TierBuckets";

    // Runtime auto-load expects this asset to live under a Resources folder.
    private const string TierLootConfigResourcesAssetPath = "Assets/Resources/Loot/TierLootConfig.asset";
    // Legacy location (kept for migration/back-compat).
    private const string TierLootConfigLegacyAssetPath = "Assets/GameData/Loot/TierBuckets/TierLootConfig.asset";
    private const string BucketT1Path = "Assets/GameData/Loot/TierBuckets/TierLootBucket_T1.asset";
    private const string BucketT2Path = "Assets/GameData/Loot/TierBuckets/TierLootBucket_T2.asset";
    private const string BucketT3Path = "Assets/GameData/Loot/TierBuckets/TierLootBucket_T3.asset";
    private const string BucketT4Path = "Assets/GameData/Loot/TierBuckets/TierLootBucket_T4.asset";
    private const string BucketT5Path = "Assets/GameData/Loot/TierBuckets/TierLootBucket_T5.asset";

    [MenuItem("Abyssbound/Loot/Setup Tier Loot (Create + Wire)")]
    public static void SetupTierLootCreateAndWire()
    {
        EnsureFolders();

        var config = LoadOrCreateTierLootConfig();
        var b1 = LoadOrCreateBucket(BucketT1Path, 1);
        var b2 = LoadOrCreateBucket(BucketT2Path, 2);
        var b3 = LoadOrCreateBucket(BucketT3Path, 3);
        var b4 = LoadOrCreateBucket(BucketT4Path, 4);
        var b5 = LoadOrCreateBucket(BucketT5Path, 5);

        bool configChanged = false;
        if (config.tier1 != b1) { config.tier1 = b1; configChanged = true; }
        if (config.tier2 != b2) { config.tier2 = b2; configChanged = true; }
        if (config.tier3 != b3) { config.tier3 = b3; configChanged = true; }
        if (config.tier4 != b4) { config.tier4 = b4; configChanged = true; }
        if (config.tier5 != b5) { config.tier5 = b5; configChanged = true; }

        if (configChanged)
        {
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log($"[LootSetup] Updated TierLootConfig buckets: {TierLootConfigResourcesAssetPath}");
        }

        // Ensure buckets have at least one valid entry so tier-content drops work immediately.
        AutoFillTierBucketsIfEmpty(b1, b2, b3, b4, b5);

        var candidates = FindSceneLootCandidates();
        int wiredCount = 0;
        int bonusWiredCount = 0;

        foreach (var mb in candidates)
        {
            if (mb == null) continue;

            bool wiredConfig = TryAssignTierLootConfig(mb, config, useUndo: true);
            if (wiredConfig) wiredCount++;

            bool wiredBonus = TryAssignEliteBossBonusRolls(mb, prefabPath: null, eliteDefault: 1, bossDefault: 2, useUndo: true);
            if (wiredBonus) bonusWiredCount++;
        }

        int prefabWiredComponents = 0;
        int prefabWiredCount = 0;
        int prefabBonusWiredComponents = 0;
        int prefabBonusWiredCount = 0;
        TryWirePrefabs(config, out prefabWiredCount, out prefabWiredComponents, out prefabBonusWiredCount, out prefabBonusWiredComponents);

        if ((wiredCount + prefabWiredComponents) == 0)
        {
            Debug.LogWarning($"[LootSetup] Could not auto-wire TierLootConfig onto any components (often OK if loot is added at runtime). Runtime auto-load will still use: {TierLootConfigResourcesAssetPath}");
        }

        if ((bonusWiredCount + prefabBonusWiredComponents) == 0)
        {
            Debug.Log("[LootSetup] Elite/Boss bonus rolls not auto-wired (field not found).");
        }

        if (wiredCount > 0 || bonusWiredCount > 0)
        {
            AssetDatabase.SaveAssets();
        }

        Debug.Log($"[LootSetup] Done. sceneCandidates={candidates.Count} sceneWiredTierConfig={wiredCount} sceneWiredBonusRolls={bonusWiredCount} prefabsWired={prefabWiredCount} prefabComponentsWiredTierConfig={prefabWiredComponents} prefabsBonusWired={prefabBonusWiredCount} prefabComponentsWiredBonusRolls={prefabBonusWiredComponents}");
    }

    [MenuItem("Abyssbound/Loot/Tier Buckets/Auto-Fill Missing Entries")]
    public static void AutoFillTierBucketsMenu()
    {
        EnsureFolders();
        var b1 = AssetDatabase.LoadAssetAtPath<TierLootBucketSO>(BucketT1Path);
        var b2 = AssetDatabase.LoadAssetAtPath<TierLootBucketSO>(BucketT2Path);
        var b3 = AssetDatabase.LoadAssetAtPath<TierLootBucketSO>(BucketT3Path);
        var b4 = AssetDatabase.LoadAssetAtPath<TierLootBucketSO>(BucketT4Path);
        var b5 = AssetDatabase.LoadAssetAtPath<TierLootBucketSO>(BucketT5Path);
        AutoFillTierBucketsIfEmpty(b1, b2, b3, b4, b5);
    }

    private static void AutoFillTierBucketsIfEmpty(params TierLootBucketSO[] buckets)
    {
        if (buckets == null || buckets.Length == 0)
            return;

        var items = FindValidItemDefinitions(limit: 64);
        if (items.Count == 0)
        {
            Debug.LogWarning("[LootSetup] No ItemDefinitionSO assets with a non-empty id were found. Tier buckets were not auto-filled.");
            return;
        }

        int filled = 0;
        int modifiedAssets = 0;
        int cursor = 0;

        for (int i = 0; i < buckets.Length; i++)
        {
            var b = buckets[i];
            if (b == null) continue;

            // Only fill buckets that are missing entries or have no valid weighted items.
            bool hasValid = false;
            if (b.entries != null && b.entries.Length > 0)
            {
                for (int e = 0; e < b.entries.Length; e++)
                {
                    var ent = b.entries[e];
                    if (ent.weight <= 0) continue;
                    var item = ent.itemRef as ItemDefinitionSO;
                    if (item == null) continue;
                    if (string.IsNullOrWhiteSpace(item.id)) continue;
                    hasValid = true;
                    break;
                }
            }
            if (hasValid) continue;

            var chosen = items[cursor % items.Count];
            cursor++;

            b.entries = new[]
            {
                new TierLootBucketSO.WeightedEntry
                {
                    itemRef = chosen,
                    weight = 1,
                    minQty = 1,
                    maxQty = 1,
                }
            };

            EditorUtility.SetDirty(b);
            filled++;
            modifiedAssets++;
            Debug.Log($"[LootSetup] Auto-filled TierLootBucket tier={b.tier} with item='{chosen.id}' ({chosen.name})");
        }

        if (modifiedAssets > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log($"[LootSetup] Auto-fill complete. bucketsFilled={filled}");
        }
        else
        {
            Debug.Log("[LootSetup] Auto-fill skipped (all buckets already had valid entries).");
        }
    }

    private static List<ItemDefinitionSO> FindValidItemDefinitions(int limit)
    {
        var list = new List<ItemDefinitionSO>(64);
        string[] guids;
        try { guids = AssetDatabase.FindAssets("t:ItemDefinitionSO"); }
        catch { guids = Array.Empty<string>(); }

        for (int i = 0; i < guids.Length; i++)
        {
            if (list.Count >= Mathf.Max(1, limit)) break;
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrWhiteSpace(path)) continue;

            ItemDefinitionSO item = null;
            try { item = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(path); }
            catch { item = null; }
            if (item == null) continue;
            if (string.IsNullOrWhiteSpace(item.id)) continue;
            list.Add(item);
        }

        return list;
    }

    [MenuItem("Abyssbound/Loot/Setup Tier Loot (Log Candidates)")]
    public static void SetupTierLootLogCandidates()
    {
        var sceneCandidates = FindSceneLootCandidates();
        var prefabCandidates = FindPrefabLootCandidates();
        Debug.Log($"[LootSetup] Candidate loot components found: scene={sceneCandidates.Count} prefabs={prefabCandidates.Count}");

        foreach (var mb in sceneCandidates)
        {
            if (mb == null) continue;
            var t = mb.GetType();

            var fields = GetUnitySerializedFields(t)
                .Select(f => $"{f.FieldType.Name} {f.Name}")
                .OrderBy(s => s)
                .ToArray();

            Debug.Log($"[LootSetup] Candidate: {GetObjectPath(mb.gameObject)} ({t.FullName})\nSerializedFields:\n- {string.Join("\n- ", fields)}", mb);
        }

        foreach (var pc in prefabCandidates)
        {
            if (pc.component == null) continue;
            var t = pc.component.GetType();

            var fields = GetUnitySerializedFields(t)
                .Select(f => $"{f.FieldType.Name} {f.Name}")
                .OrderBy(s => s)
                .ToArray();

            Debug.Log($"[LootSetup] Candidate Prefab: {pc.prefabPath}:{GetObjectPath(pc.component.gameObject)} ({t.FullName})\nSerializedFields:\n- {string.Join("\n- ", fields)}");
        }
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/GameData"))
        {
            if (!AssetDatabase.IsValidFolder("Assets")) return;
            AssetDatabase.CreateFolder("Assets", "GameData");
        }

        if (!AssetDatabase.IsValidFolder(RootFolder))
            AssetDatabase.CreateFolder("Assets/GameData", "Loot");

        if (!AssetDatabase.IsValidFolder(TierBucketsFolder))
            AssetDatabase.CreateFolder(RootFolder, "TierBuckets");

        // Ensure Resources/Loot exists for runtime auto-load.
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Loot"))
            AssetDatabase.CreateFolder("Assets/Resources", "Loot");
    }

    private static TierLootConfigSO LoadOrCreateTierLootConfig()
    {
        var cfg = AssetDatabase.LoadAssetAtPath<TierLootConfigSO>(TierLootConfigResourcesAssetPath);
        if (cfg != null) return cfg;

        // If a legacy config exists, copy it into Resources to preserve any manual edits.
        var legacy = AssetDatabase.LoadAssetAtPath<TierLootConfigSO>(TierLootConfigLegacyAssetPath);
        if (legacy != null)
        {
            try
            {
                if (AssetDatabase.CopyAsset(TierLootConfigLegacyAssetPath, TierLootConfigResourcesAssetPath))
                {
                    AssetDatabase.SaveAssets();
                    cfg = AssetDatabase.LoadAssetAtPath<TierLootConfigSO>(TierLootConfigResourcesAssetPath);
                    if (cfg != null)
                    {
                        Debug.Log($"[LootSetup] Copied legacy TierLootConfig into Resources: {TierLootConfigResourcesAssetPath}");
                        return cfg;
                    }
                }
            }
            catch { }
        }

        cfg = ScriptableObject.CreateInstance<TierLootConfigSO>();
        AssetDatabase.CreateAsset(cfg, TierLootConfigResourcesAssetPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[LootSetup] Created asset: {TierLootConfigResourcesAssetPath}");
        return cfg;
    }

    private static T LoadOrCreateAsset<T>(string assetPath) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (asset != null) return asset;

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, assetPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[LootSetup] Created asset: {assetPath}");
        return asset;
    }

    private static TierLootBucketSO LoadOrCreateBucket(string assetPath, int tier)
    {
        var bucket = LoadOrCreateAsset<TierLootBucketSO>(assetPath);
        if (bucket.tier != tier)
        {
            bucket.tier = tier;
            EditorUtility.SetDirty(bucket);
            AssetDatabase.SaveAssets();
        }
        return bucket;
    }

    private static List<MonoBehaviour> FindSceneLootCandidates()
    {
        var results = new List<MonoBehaviour>(128);

        var all = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
        for (int i = 0; i < all.Length; i++)
        {
            var mb = all[i];
            if (mb == null) continue;
            if (EditorUtility.IsPersistent(mb)) continue;
            if (!mb.gameObject.scene.IsValid()) continue;

            var typeName = mb.GetType().Name;
            if (!IsLootCandidateTypeName(typeName))
                continue;

            results.Add(mb);
        }

        return results;
    }

    private static bool TryAssignTierLootConfig(MonoBehaviour target, TierLootConfigSO config, bool useUndo)
    {
        if (target == null || config == null) return false;

        var t = target.GetType();
        var candidates = GetUnitySerializedFields(t)
            .Where(f => typeof(TierLootConfigSO).IsAssignableFrom(f.FieldType))
            .Where(f => FieldNameMatchesAny(f.Name, new[] { "tierLoot", "TierLoot", "lootTier", "TierLootConfig", "tierConfig" }))
            .ToArray();

        foreach (var field in candidates)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field.Name);
            if (prop == null || prop.propertyType != SerializedPropertyType.ObjectReference)
                continue;

            if (prop.objectReferenceValue == config)
                return true;

            if (useUndo)
                Undo.RecordObject(target, "Assign TierLootConfig");
            prop.objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);

            Debug.Log($"[LootSetup] Wired TierLootConfig on {GetObjectPath(target.gameObject)} ({t.Name}.{field.Name})", target);
            return true;
        }

        var props = t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (var p in props)
        {
            if (!p.CanWrite) continue;
            if (!typeof(TierLootConfigSO).IsAssignableFrom(p.PropertyType)) continue;
            if (!FieldNameMatchesAny(p.Name, new[] { "tierLoot", "TierLoot", "lootTier", "TierLootConfig", "tierConfig" })) continue;

            try
            {
                if (useUndo)
                    Undo.RecordObject(target, "Assign TierLootConfig");
                p.SetValue(target, config);
                EditorUtility.SetDirty(target);
                Debug.Log($"[LootSetup] Wired TierLootConfig via property on {GetObjectPath(target.gameObject)} ({t.Name}.{p.Name})", target);
                return true;
            }
            catch { }
        }

        return false;
    }

    private static bool TryAssignEliteBossBonusRolls(MonoBehaviour target, string prefabPath, int eliteDefault, int bossDefault, bool useUndo)
    {
        if (target == null) return false;

        var t = target.GetType();
        var so = new SerializedObject(target);

        const string ElitePropName = "eliteBonusRolls";
        const string BossPropName = "bossBonusRolls";

        var eliteProp = so.FindProperty(ElitePropName);
        var bossProp = so.FindProperty(BossPropName);

        bool hasElite = eliteProp != null && eliteProp.propertyType == SerializedPropertyType.Integer;
        bool hasBoss = bossProp != null && bossProp.propertyType == SerializedPropertyType.Integer;

        if (!hasElite || !hasBoss)
        {
            var props = new List<string>(32);
            try
            {
                var it = so.GetIterator();
                bool enterChildren = true;
                while (it.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    if (props.Count >= 30) break;
                    props.Add(it.propertyPath);
                }
            }
            catch { }

            string where = string.IsNullOrWhiteSpace(prefabPath)
                ? GetObjectPath(target.gameObject)
                : $"{prefabPath}:{GetObjectPath(target.gameObject)}";

            Debug.LogWarning($"[LootSetup] Bonus-roll fields not found on {where} ({t.Name}). Expected '{ElitePropName}' and '{BossPropName}'. FirstProps=\n- {string.Join("\n- ", props)}", target);
            return false;
        }

        if (useUndo)
            Undo.RecordObject(target, "Assign Elite/Boss Bonus Rolls");

        eliteProp.intValue = eliteDefault;
        bossProp.intValue = bossDefault;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);

        if (!string.IsNullOrWhiteSpace(prefabPath))
        {
            Debug.Log($"[LootSetup] Wired Elite/Boss bonus rolls in prefab {prefabPath}:{GetObjectPath(target.gameObject)} (LootDropOnDeath) elite={eliteDefault} boss={bossDefault}");
        }

        return true;
    }

    private readonly struct PrefabCandidate
    {
        public readonly string prefabPath;
        public readonly MonoBehaviour component;

        public PrefabCandidate(string prefabPath, MonoBehaviour component)
        {
            this.prefabPath = prefabPath;
            this.component = component;
        }
    }

    private static List<PrefabCandidate> FindPrefabLootCandidates()
    {
        var results = new List<PrefabCandidate>(256);
        var guids = AssetDatabase.FindAssets("t:Prefab", GetPrefabSearchFolders());

        for (int i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrWhiteSpace(path)) continue;
            if (ShouldSkipPrefabPath(path)) continue;

            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(path);
                if (root == null) continue;

                var comps = root.GetComponentsInChildren<MonoBehaviour>(true);
                for (int c = 0; c < comps.Length; c++)
                {
                    var mb = comps[c];
                    if (mb == null) continue;
                    if (!IsLootCandidateTypeName(mb.GetType().Name)) continue;
                    results.Add(new PrefabCandidate(path, mb));
                }
            }
            catch { }
            finally
            {
                if (root != null)
                {
                    try { PrefabUtility.UnloadPrefabContents(root); }
                    catch { }
                }
            }
        }

        return results;
    }

    private static void TryWirePrefabs(TierLootConfigSO config, out int prefabsWired, out int prefabComponentsWired, out int prefabsBonusWired, out int prefabBonusComponentsWired)
    {
        prefabsWired = 0;
        prefabComponentsWired = 0;
        prefabsBonusWired = 0;
        prefabBonusComponentsWired = 0;

        if (config == null)
            return;

        var guids = AssetDatabase.FindAssets("t:Prefab", GetPrefabSearchFolders());
        for (int i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrWhiteSpace(path)) continue;
            if (ShouldSkipPrefabPath(path)) continue;

            GameObject root = null;
            bool prefabChanged = false;
            bool prefabBonusChanged = false;

            try
            {
                root = PrefabUtility.LoadPrefabContents(path);
                if (root == null) continue;

                var comps = root.GetComponentsInChildren<MonoBehaviour>(true);
                for (int c = 0; c < comps.Length; c++)
                {
                    var mb = comps[c];
                    if (mb == null) continue;
                    if (!IsLootCandidateTypeName(mb.GetType().Name)) continue;

                    if (TryAssignTierLootConfig(mb, config, useUndo: false))
                    {
                        prefabChanged = true;
                        prefabComponentsWired++;
                        Debug.Log($"[LootSetup] Wired TierLootConfig in prefab {path}:{GetObjectPath(mb.gameObject)} ({mb.GetType().Name})");
                    }

                    if (TryAssignEliteBossBonusRolls(mb, prefabPath: path, eliteDefault: 1, bossDefault: 2, useUndo: false))
                    {
                        prefabBonusChanged = true;
                        prefabBonusComponentsWired++;
                        Debug.Log($"[LootSetup] Set bonus rolls in prefab {path}:{GetObjectPath(mb.gameObject)} ({mb.GetType().Name})");
                    }
                }

                if (prefabChanged)
                    prefabsWired++;
                if (prefabBonusChanged)
                    prefabsBonusWired++;

                if (prefabChanged || prefabBonusChanged)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
            }
            catch { }
            finally
            {
                if (root != null)
                {
                    try { PrefabUtility.UnloadPrefabContents(root); }
                    catch { }
                }
            }
        }
    }

    private static string[] GetPrefabSearchFolders()
    {
        // Prefer scanning likely gameplay prefab locations to avoid importing/loading editor/UI/TMP/broken content.
        var folders = new List<string>(8);
        TryAddFolder("Assets/Prefabs", folders);
        TryAddFolder("Assets/Game", folders);
        TryAddFolder("Assets/Resources", folders);
        TryAddFolder("Assets/Abyssbound", folders);
        TryAddFolder("Assets/Abyss", folders);

        if (folders.Count == 0)
            folders.Add("Assets");

        return folders.Distinct().ToArray();
    }

    private static void TryAddFolder(string folder, List<string> folders)
    {
        if (AssetDatabase.IsValidFolder(folder))
            folders.Add(folder);
    }

    private static bool ShouldSkipPrefabPath(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return true;

        string p = assetPath.Replace('\\', '/');

        // Skip known-problematic or irrelevant content for this workflow.
        if (p.StartsWith("Assets/_Broken/", StringComparison.OrdinalIgnoreCase)) return true;
        if (p.IndexOf("/TextMesh Pro/", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (p.IndexOf("/UI/", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (p.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0) return true;

        return false;
    }

    private static bool IsLootCandidateTypeName(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return false;

        if (!ContainsIgnoreCase(typeName, "Loot"))
            return false;

        return ContainsIgnoreCase(typeName, "Drop")
            || ContainsIgnoreCase(typeName, "Death")
            || ContainsIgnoreCase(typeName, "Manager")
            || ContainsIgnoreCase(typeName, "Registry");
    }

    private static IEnumerable<FieldInfo> GetUnitySerializedFields(Type type)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        for (var t = type; t != null && t != typeof(MonoBehaviour); t = t.BaseType)
        {
            var fields = t.GetFields(flags);
            for (int i = 0; i < fields.Length; i++)
            {
                var f = fields[i];
                if (f.IsStatic) continue;
                if (f.IsDefined(typeof(NonSerializedAttribute), inherit: true)) continue;

                bool isSerialized = f.IsPublic || f.IsDefined(typeof(SerializeField), inherit: true);
                if (!isSerialized) continue;

                if (f.FieldType.IsPointer) continue;

                yield return f;
            }
        }
    }

    private static bool FieldNameMatchesAny(string name, IEnumerable<string> substrings)
    {
        foreach (var s in substrings)
        {
            if (name.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    private static bool ContainsIgnoreCase(string haystack, string needle)
    {
        return haystack != null && needle != null && haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string GetObjectPath(GameObject go)
    {
        if (go == null) return "(null)";

        var parts = new List<string>(16);
        Transform t = go.transform;
        while (t != null)
        {
            parts.Add(t.name);
            t = t.parent;
        }
        parts.Reverse();
        return string.Join("/", parts);
    }
}
