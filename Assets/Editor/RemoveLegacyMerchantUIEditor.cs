using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEditor.SceneManagement;

public static class RemoveLegacyMerchantUIEditor
{
    [MenuItem("Tools/Abyssbound/Legacy/Remove Legacy Merchant UIs")] 
    public static void RemoveLegacyMerchantUIs()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("Run this in Edit Mode (not Play Mode).");
            return;
        }

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();
        var candidates = new List<GameObject>();

        foreach (var root in roots)
        {
            Traverse(root, go =>
            {
                if (IsLikelyLegacyMerchantUi(go))
                    candidates.Add(go);
            });
        }

        if (candidates.Count == 0)
        {
            EditorUtility.DisplayDialog("Remove Legacy Merchant UIs", "No legacy Merchant UI candidates found in the active scene.", "OK");
            return;
        }

        // Build preview message
        int maxShow = 12;
        var names = new List<string>();
        for (int i = 0; i < Mathf.Min(candidates.Count, maxShow); i++) names.Add(candidates[i].name);
        string msg = $"Found {candidates.Count} candidate GameObjects to remove:\n- {string.Join("\n- ", names)}";
        if (candidates.Count > maxShow) msg += "\n- ...";
        msg += "\n\nThis will delete these GameObjects from the scene (Undoable). Proceed?";

        if (!EditorUtility.DisplayDialog("Remove Legacy Merchant UIs", msg, "Delete", "Cancel"))
            return;

        int removed = 0;
        foreach (var go in candidates)
        {
            if (go == null) continue;
            // Undoable destroy
            Undo.DestroyObjectImmediate(go);
            removed++;
        }

        if (removed > 0)
        {
            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(scene);
        }

        EditorUtility.DisplayDialog("Remove Legacy Merchant UIs", $"Removed {removed} objects.", "OK");

        // Also offer to remove matching legacy prefabs from project
        var prefabCandidates = FindLegacyPrefabAssets();
        if (prefabCandidates.Count > 0)
        {
            string assetMsg = $"Found {prefabCandidates.Count} prefab assets that look like legacy Merchant UI. Delete them from project?\n\n- {string.Join("\n- ", prefabCandidates)}";
            if (EditorUtility.DisplayDialog("Remove Legacy Merchant UI Prefabs", assetMsg, "Delete", "Keep"))
            {
                int deleted = 0;
                foreach (var p in prefabCandidates)
                {
                    if (AssetDatabase.DeleteAsset(p)) deleted++;
                }
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Remove Legacy Merchant UI Prefabs", $"Deleted {deleted} prefab assets.", "OK");
            }
        }
    }

    private static void Traverse(GameObject root, System.Action<GameObject> visitor)
    {
        var stack = new Stack<GameObject>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var cur = stack.Pop();
            visitor(cur);
            for (int i = 0; i < cur.transform.childCount; i++)
                stack.Push(cur.transform.GetChild(i).gameObject);
        }
    }

    private static bool IsLikelyLegacyMerchantUi(GameObject go)
    {
        if (go == null) return false;
        string name = go.name ?? string.Empty;
        // Exclude the new, correct inspector-driven UI (solid black root + proper scaler)
        if (IsNewInspectorUi(go))
            return false;

        bool nameMatch = name.Contains("MerchantShopUI") || name.Contains("MerchantShopUIRoot") || name.Contains("MerchantShopUICanvas") || name.ToLower().Contains("merchantshop");

        bool hasRowTemplate = go.transform.Find("RowTemplate") != null;
        bool hasItemsScroll = go.transform.Find("ItemsScroll") != null || go.transform.Find("ItemsScrollView") != null;

        var scaler = go.GetComponentInChildren<CanvasScaler>(true);
        bool scalerLegacy = scaler == null || scaler.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize;

        var img = go.GetComponent<Image>();
        bool semiTransparentBg = img != null && img.color.a < 1f;

        // Heuristic: likely legacy if name or children match and scaler is legacy or background is transparent.
        if ((nameMatch || hasRowTemplate || hasItemsScroll) && (scalerLegacy || semiTransparentBg))
            return true;

        // Also consider any object with RowTemplate as legacy
        if (hasRowTemplate) return true;

        return false;
    }

    private static bool IsNewInspectorUi(GameObject go)
    {
        if (go == null) return false;
        // New UI characteristics: root Image is solid black AND CanvasScaler configured to ScaleWithScreenSize (1920x1080, match 0.5)
        var img = go.GetComponent<Image>();
        if (img == null) return false;
        if (img.color != Color.black) return false;

        var scaler = go.GetComponentInChildren<CanvasScaler>(true);
        if (scaler == null) return false;
        if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) return false;
        if (scaler.referenceResolution != new Vector2(1920, 1080)) return false;
        if (Mathf.Abs(scaler.matchWidthOrHeight - 0.5f) > 0.01f) return false;

        return true;
    }

    private static List<string> FindLegacyPrefabAssets()
    {
        var results = new List<string>();
        var guids = AssetDatabase.FindAssets("MerchantShopUI t:prefab");
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;
            // Check same heuristics on prefab root
            if (prefab.transform.Find("RowTemplate") != null) { results.Add(path); continue; }
            var scaler = prefab.GetComponentInChildren<CanvasScaler>(true);
            if (scaler == null || scaler.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize) { results.Add(path); continue; }
        }

        // Also search for prefabs containing RowTemplate anywhere
        var allPrefabs = AssetDatabase.FindAssets("t:prefab");
        foreach (var g in allPrefabs)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            if (results.Contains(path)) continue;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;
            if (prefab.transform.Find("RowTemplate") != null) results.Add(path);
        }

        return results;
    }
}
