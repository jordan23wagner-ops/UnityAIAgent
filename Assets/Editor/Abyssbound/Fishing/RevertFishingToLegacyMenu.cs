#if UNITY_EDITOR
using System;
using Abyssbound.Skills.Fishing;
using Abyssbound.WorldInteraction;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abyssbound.EditorTools.Fishing
{
    public static class RevertFishingToLegacyMenu
    {
        [MenuItem("Tools/Abyssbound/Fishing/Revert Fishing Spots To Legacy (Remove Click/Hover)")]
        public static void RevertFishingSpotsToLegacy()
        {
            int removed = 0;
            int removedProxy = 0;
            int reenabledColliders = 0;

            foreach (var go in EnumerateSceneGameObjects())
            {
                if (go == null) continue;

                // Only touch objects that look like fishing spots.
                var spot = go.GetComponent<FishingSpot>();
                if (spot == null) continue;

                if (IsMerchantRelated(go))
                    continue;

                // Remove the WorldInteraction wrapper if present.
                var wi = go.GetComponent<FishingSpotInteractable>();
                if (wi != null)
                {
                    Undo.RegisterFullObjectHierarchyUndo(go, "Revert Fishing Spot To Legacy");
                    Undo.DestroyObjectImmediate(wi);
                    removed++;
                }

                // Remove HighlightProxy child if present.
                try
                {
                    var proxy = go.transform.Find("HighlightProxy");
                    if (proxy != null)
                    {
                        Undo.DestroyObjectImmediate(proxy.gameObject);
                        removedProxy++;
                    }
                }
                catch { }

                // Re-enable any disabled trigger colliders in this hierarchy (best-effort).
                try
                {
                    var cols = go.GetComponentsInChildren<Collider>(includeInactive: true);
                    if (cols != null)
                    {
                        for (int i = 0; i < cols.Length; i++)
                        {
                            var c = cols[i];
                            if (c == null) continue;
                            if (!c.isTrigger) continue;
                            if (c.enabled) continue;

                            c.enabled = true;
                            reenabledColliders++;
                        }
                    }
                }
                catch { }
            }

            Debug.Log($"[Fishing] Reverted fishing spots to legacy. removedInteractables={removed} removedHighlightProxy={removedProxy} reenabledTriggerColliders={reenabledColliders}");
        }

        private static bool IsMerchantRelated(GameObject go)
        {
            if (go == null) return false;

            // Keep this conservative: if anything in the hierarchy looks merchant-related, skip.
            try
            {
                for (var t = go.transform; t != null; t = t.parent)
                {
                    var n = (t.name ?? string.Empty).ToLowerInvariant();
                    if (n.Contains("merchant"))
                        return true;

                    var comps = t.GetComponents<Component>();
                    if (comps == null) continue;
                    for (int i = 0; i < comps.Length; i++)
                    {
                        var c = comps[i];
                        if (c == null) continue;
                        var tn = c.GetType().Name;
                        if (tn.IndexOf("Merchant", StringComparison.OrdinalIgnoreCase) >= 0)
                            return true;
                    }
                }
            }
            catch { }

            return false;
        }

        private static System.Collections.Generic.IEnumerable<GameObject> EnumerateSceneGameObjects()
        {
            int sceneCount = SceneManager.sceneCount;
            for (int s = 0; s < sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                GameObject[] roots = null;
                try { roots = scene.GetRootGameObjects(); }
                catch { roots = null; }

                if (roots == null) continue;
                for (int i = 0; i < roots.Length; i++)
                {
                    var r = roots[i];
                    if (r == null) continue;

                    foreach (var child in EnumerateHierarchy(r.transform))
                        yield return child;
                }
            }
        }

        private static System.Collections.Generic.IEnumerable<GameObject> EnumerateHierarchy(Transform root)
        {
            if (root == null) yield break;

            yield return root.gameObject;

            int count = 0;
            try { count = root.childCount; }
            catch { count = 0; }

            for (int i = 0; i < count; i++)
            {
                Transform c = null;
                try { c = root.GetChild(i); }
                catch { c = null; }

                if (c == null) continue;

                foreach (var go in EnumerateHierarchy(c))
                    yield return go;
            }
        }
    }
}
#endif
