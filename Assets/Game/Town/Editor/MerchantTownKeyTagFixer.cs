using UnityEditor;
using UnityEngine;
using Game.Town;

// Editor utility: Fixes missing TownKeyTag on all merchant GameObjects in the current scene and their prefabs.
public static class MerchantTownKeyTagFixer
{
    [MenuItem("Tools/Fix Merchant TownKeyTags in Scene")]
    public static void FixAllMerchantTownKeyTags()
    {
        int fixedCount = 0;
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (!go.activeInHierarchy) continue;
            if (!LooksLikeMerchant(go)) continue;

            // Extract key from name if present
            string key = ExtractKeyFromName(go.name);
            if (string.IsNullOrWhiteSpace(key)) continue;

            var tag = go.GetComponent<TownKeyTag>();
            if (tag == null)
            {
                tag = Undo.AddComponent<TownKeyTag>(go);
                fixedCount++;
            }
            if (tag.Key != key)
            {
                Undo.RecordObject(tag, "Set TownKeyTag Key");
                tag.SetKey(key);
            }

            // If prefab, apply to prefab asset
            var prefab = PrefabUtility.GetCorrespondingObjectFromSource(go);
            if (prefab != null)
            {
                var prefabTag = ((GameObject)prefab).GetComponent<TownKeyTag>();
                if (prefabTag == null)
                {
                    prefabTag = Undo.AddComponent<TownKeyTag>((GameObject)prefab);
                }
                if (prefabTag.Key != key)
                {
                    Undo.RecordObject(prefabTag, "Set TownKeyTag Key (Prefab)");
                    prefabTag.SetKey(key);
                }
                PrefabUtility.ApplyPrefabInstance(go, InteractionMode.AutomatedAction);
            }
        }
        Debug.Log($"[MerchantTownKeyTagFixer] Fixed {fixedCount} merchant(s) in scene.");
    }

    private static bool LooksLikeMerchant(GameObject go)
    {
        var n = go.name ?? "";
        if (n.IndexOf("merchant", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        return false;
    }

    private static string ExtractKeyFromName(string name)
    {
        // Looks for [TownKey:the_key] in the name
        if (string.IsNullOrWhiteSpace(name)) return null;
        int idx = name.IndexOf("[TownKey:");
        if (idx < 0) return null;
        int start = idx + 9;
        int end = name.IndexOf(']', start);
        if (end < 0) return null;
        return name.Substring(start, end - start).Trim();
    }
}
