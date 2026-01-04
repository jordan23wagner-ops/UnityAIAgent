#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Abyssbound.Skills.Fishing;
using Abyssbound.WorldInteraction;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abyssbound.Editor.WorldInteraction
{
    public static class ConvertWorldActivitiesMenu
    {
        private const string WorldInteractableLayerName = "WorldInteractable";

        [MenuItem("Tools/Abyssbound/World Interaction/Convert Bonfire To Click Interaction")]
        public static void ConvertBonfire()
        {
            int wiLayer = LayerMask.NameToLayer(WorldInteractableLayerName);
            if (wiLayer < 0)
            {
                Debug.LogWarning($"[WorldInteraction] Missing layer '{WorldInteractableLayerName}'. Run the WorldInteraction setup first.");
                return;
            }

            int converted = 0;
            foreach (var go in EnumerateSceneGameObjects())
            {
                if (go == null) continue;
                if (IsMerchantRelated(go)) continue;

                if (!LooksLikeBonfire(go))
                    continue;

                // Prefer the CookingStation root if present.
                var station = SafeGetComponent(go, "Abyssbound.Cooking.CookingStation");
                if (station != null)
                {
                    ConvertSingleBonfire(station.gameObject, wiLayer);
                    converted++;
                    continue;
                }

                ConvertSingleBonfire(go, wiLayer);
                converted++;
            }

            if (converted > 0)
            {
                MarkScenesDirty();
                Debug.Log($"[WorldInteraction] Converted bonfires: {converted}");
            }
            else
            {
                Debug.LogWarning("[WorldInteraction] No bonfire candidates found to convert.");
            }
        }

        [MenuItem("Tools/Abyssbound/World Interaction/Convert Fishing Spots To Click Interaction")]
        public static void ConvertFishingSpots()
        {
            int wiLayer = LayerMask.NameToLayer(WorldInteractableLayerName);
            if (wiLayer < 0)
            {
                Debug.LogWarning($"[WorldInteraction] Missing layer '{WorldInteractableLayerName}'. Run the WorldInteraction setup first.");
                return;
            }

            int converted = 0;
            foreach (var go in EnumerateSceneGameObjects())
            {
                if (go == null) continue;
                if (IsMerchantRelated(go)) continue;

                var spot = go.GetComponent<FishingSpot>();
                if (spot == null && !LooksLikeFishingSpot(go))
                    continue;

                ConvertSingleFishingSpot(go, wiLayer);
                converted++;
            }

            if (converted > 0)
            {
                MarkScenesDirty();
                Debug.Log($"[WorldInteraction] Converted fishing spots: {converted}");
            }
            else
            {
                Debug.LogWarning("[WorldInteraction] No fishing spot candidates found to convert.");
            }
        }

        private static void ConvertSingleBonfire(GameObject root, int wiLayer)
        {
            if (root == null) return;
            if (IsMerchantRelated(root)) return;

            Undo.RegisterFullObjectHierarchyUndo(root, "Convert Bonfire To WorldInteractable");

            // Ensure component.
            var interactable = root.GetComponent<BonfireInteractable>();
            if (interactable == null)
                interactable = Undo.AddComponent<BonfireInteractable>(root);

            // Ensure layer (root must be WorldInteractable).
            if (root.layer != wiLayer)
                root.layer = wiLayer;

            EnsureSingleTriggerCollider(root, wiLayer);
            EnsureHighlightRenderers(interactable, root);
            EnsureDisplayName(interactable, "Bonfire");
            EnsureRange(interactable, 3f);
        }

        private static void ConvertSingleFishingSpot(GameObject root, int wiLayer)
        {
            if (root == null) return;
            if (IsMerchantRelated(root)) return;

            Undo.RegisterFullObjectHierarchyUndo(root, "Convert Fishing Spot To WorldInteractable");

            // Ensure component.
            var interactable = root.GetComponent<FishingSpotInteractable>();
            if (interactable == null)
                interactable = Undo.AddComponent<FishingSpotInteractable>(root);

            // Ensure layer (root must be WorldInteractable).
            if (root.layer != wiLayer)
                root.layer = wiLayer;

            EnsureSingleTriggerCollider(root, wiLayer);
            EnsureHighlightRenderers(interactable, root);
            EnsureRange(interactable, 3f);

            // Infer spot type from FishingSpot data/name.
            var spotType = InferFishingSpotType(root);
            if (!string.IsNullOrWhiteSpace(spotType))
                interactable.SetSpotType(spotType);
        }

        private static void EnsureSingleTriggerCollider(GameObject root, int wiLayer)
        {
            // Goal:
            // - Exactly ONE enabled trigger collider in this hierarchy
            // - It lives on the WorldInteractable layer so the raycaster hits it
            // - Other trigger colliders are disabled (to avoid ambiguity)

            if (root == null) return;

            var renderBounds = TryGetRendererBounds(root, out var rb) ? rb : new Bounds(root.transform.position, Vector3.one);

            var colliders = root.GetComponentsInChildren<Collider>(includeInactive: true);
            if (colliders == null) colliders = Array.Empty<Collider>();

            Collider chosen = null;
            float bestScore = float.PositiveInfinity;

            // Prefer triggers first.
            for (int pass = 0; pass < 2 && chosen == null; pass++)
            {
                bool wantTrigger = pass == 0;

                for (int i = 0; i < colliders.Length; i++)
                {
                    var c = colliders[i];
                    if (c == null) continue;
                    if (!c.enabled) continue;

                    if (wantTrigger && !c.isTrigger) continue;
                    if (!wantTrigger && c.isTrigger) continue;

                    // Skip obvious non-interaction colliders on debug primitives.
                    if (c.gameObject.name.StartsWith("__", StringComparison.Ordinal))
                        continue;

                    float score = ScoreCollider(c, renderBounds);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        chosen = c;
                    }
                }
            }

            if (chosen == null)
            {
                // Add a safe fallback trigger collider on the root.
                var sphere = root.GetComponent<SphereCollider>();
                if (sphere == null)
                    sphere = Undo.AddComponent<SphereCollider>(root);

                sphere.isTrigger = true;

                float radius = 1.0f;
                try
                {
                    radius = Mathf.Max(0.5f, Mathf.Max(renderBounds.extents.x, renderBounds.extents.z));
                }
                catch { radius = 1.0f; }

                sphere.radius = radius;
                sphere.center = Vector3.up * 1.0f;
                chosen = sphere;
            }

            // Make sure chosen is a trigger and on the WI layer.
            try { chosen.isTrigger = true; } catch { }
            try { chosen.gameObject.layer = wiLayer; } catch { }

            // Disable all other trigger colliders to satisfy "exactly one trigger collider".
            for (int i = 0; i < colliders.Length; i++)
            {
                var c = colliders[i];
                if (c == null) continue;
                if (c == chosen) continue;

                if (!c.isTrigger)
                    continue;

                try { c.enabled = false; }
                catch { }
            }
        }

        private static float ScoreCollider(Collider c, Bounds rendererBounds)
        {
            // Simple heuristic: prioritize colliders near renderer center and similar size.
            float centerDist = 0f;
            float sizeDelta = 0f;
            try { centerDist = Vector3.Distance(c.bounds.center, rendererBounds.center); }
            catch { centerDist = 1000f; }

            try { sizeDelta = Mathf.Abs(c.bounds.size.magnitude - rendererBounds.size.magnitude); }
            catch { sizeDelta = 1000f; }

            // Prefer larger colliders slightly (avoid tiny triggers).
            float volumePenalty = 0f;
            try
            {
                var s = c.bounds.size;
                var vol = Mathf.Max(0.0001f, s.x * s.y * s.z);
                volumePenalty = 1f / vol;
            }
            catch { volumePenalty = 0f; }

            return centerDist * 2.0f + sizeDelta + volumePenalty;
        }

        private static void EnsureHighlightRenderers(WorldInteractable interactable, GameObject root)
        {
            if (interactable == null || root == null) return;

            try
            {
                var rs = root.GetComponentsInChildren<Renderer>(includeInactive: true);
                if (rs == null || rs.Length == 0)
                    return;

                var so = new SerializedObject(interactable);
                var p = so.FindProperty("highlightRenderers");
                if (p == null)
                    return;

                // Only set if empty (don’t stomp hand-tuned assignments).
                if (p.isArray && p.arraySize > 0)
                    return;

                p.arraySize = rs.Length;
                for (int i = 0; i < rs.Length; i++)
                    p.GetArrayElementAtIndex(i).objectReferenceValue = rs[i];

                so.ApplyModifiedPropertiesWithoutUndo();
            }
            catch { }
        }

        private static void EnsureDisplayName(WorldInteractable interactable, string name)
        {
            if (interactable == null) return;

            try
            {
                var so = new SerializedObject(interactable);
                var p = so.FindProperty("displayName");
                if (p == null) return;
                if (!string.Equals(p.stringValue, name, StringComparison.Ordinal))
                    p.stringValue = name;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            catch { }
        }

        private static void EnsureRange(WorldInteractable interactable, float range)
        {
            if (interactable == null) return;

            try
            {
                var so = new SerializedObject(interactable);

                var requires = so.FindProperty("requiresRange");
                if (requires != null)
                    requires.boolValue = true;

                var r = so.FindProperty("interactionRange");
                if (r != null)
                    r.floatValue = Mathf.Max(0.1f, range);

                so.ApplyModifiedPropertiesWithoutUndo();
            }
            catch { }
        }

        private static string InferFishingSpotType(GameObject go)
        {
            if (go == null) return null;

            // 1) Try yield item id from FishingSpot data.
            try
            {
                var spot = go.GetComponent<FishingSpot>();
                if (spot != null && spot.TryGetFishingAction(out _, out _, out var yieldItemId, out _))
                {
                    var inferred = InferFromString(yieldItemId);
                    if (!string.IsNullOrWhiteSpace(inferred))
                        return inferred;
                }
            }
            catch { }

            // 2) Infer from object name.
            try
            {
                var inferred = InferFromString(go.name);
                if (!string.IsNullOrWhiteSpace(inferred))
                    return inferred;
            }
            catch { }

            return "Fishing Spot";
        }

        private static string InferFromString(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;

            var n = s.ToLowerInvariant();
            if (n.Contains("shrimp")) return "Shrimp Spot";
            if (n.Contains("anchovy")) return "Anchovy Spot";
            if (n.Contains("sardine")) return "Sardine Spot";
            if (n.Contains("trout")) return "Trout Spot";
            if (n.Contains("salmon")) return "Salmon Spot";
            if (n.Contains("lobster")) return "Lobster Spot";
            if (n.Contains("tuna")) return "Tuna Spot";
            if (n.Contains("sword")) return "Swordfish Spot";

            return null;
        }

        private static bool LooksLikeBonfire(GameObject go)
        {
            if (go == null) return false;

            var n = go.name ?? string.Empty;
            if (n.IndexOf("bonfire", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (n.IndexOf("cooking", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            // Bonfire visuals script lives under Assets/Scripts/Cooking
            if (SafeGetComponent(go, "BonfireVisuals") != null)
                return true;

            // Cooking station typically sits on the bonfire object.
            if (SafeGetComponent(go, "Abyssbound.Cooking.CookingStation") != null)
                return true;

            return false;
        }

        private static bool LooksLikeFishingSpot(GameObject go)
        {
            if (go == null) return false;
            var n = go.name ?? string.Empty;
            return n.IndexOf("fish", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("fishing", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("spot", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Component SafeGetComponent(GameObject go, string typeName)
        {
            if (go == null || string.IsNullOrWhiteSpace(typeName)) return null;
            try
            {
                var t = FindTypeByName(typeName);
                if (t == null) return null;
                return go.GetComponent(t);
            }
            catch { return null; }
        }

        private static Type FindTypeByName(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)) return null;

            // First try full name.
            var t = Type.GetType(typeName);
            if (t != null) return t;

            // Search loaded assemblies.
            try
            {
                var asms = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < asms.Length; i++)
                {
                    var a = asms[i];
                    if (a == null) continue;
                    try
                    {
                        var tt = a.GetType(typeName);
                        if (tt != null) return tt;
                    }
                    catch { }
                }
            }
            catch { }

            // Fallback: match by short name.
            try
            {
                var asms = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < asms.Length; i++)
                {
                    var a = asms[i];
                    if (a == null) continue;
                    Type[] types = null;
                    try { types = a.GetTypes(); } catch { types = null; }
                    if (types == null) continue;

                    for (int j = 0; j < types.Length; j++)
                    {
                        var tt = types[j];
                        if (tt == null) continue;
                        if (string.Equals(tt.Name, typeName, StringComparison.Ordinal))
                            return tt;
                    }
                }
            }
            catch { }

            return null;
        }

        private static bool IsMerchantRelated(GameObject go)
        {
            if (go == null) return false;

            // Name-based quick reject.
            try
            {
                var tf = go.transform;
                while (tf != null)
                {
                    var n = tf.name ?? string.Empty;
                    if (n.IndexOf("merchant", StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                    if (n.IndexOf("merchants", StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                    if (n.IndexOf("shop", StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                    tf = tf.parent;
                }
            }
            catch { }

            // Component-type based reject.
            try
            {
                var tf = go.transform;
                while (tf != null)
                {
                    var comps = tf.GetComponents<Component>();
                    if (comps != null)
                    {
                        for (int i = 0; i < comps.Length; i++)
                        {
                            var c = comps[i];
                            if (c == null) continue;
                            var tn = c.GetType().Name;
                            if (tn.IndexOf("Merchant", StringComparison.OrdinalIgnoreCase) >= 0)
                                return true;
                        }
                    }
                    tf = tf.parent;
                }
            }
            catch { }

            return false;
        }

        private static bool TryGetRendererBounds(GameObject root, out Bounds b)
        {
            b = default;
            bool has = false;

            if (root == null) return false;

            Renderer[] rs = null;
            try { rs = root.GetComponentsInChildren<Renderer>(includeInactive: true); }
            catch { rs = null; }

            if (rs == null || rs.Length == 0)
                return false;

            for (int i = 0; i < rs.Length; i++)
            {
                var r = rs[i];
                if (r == null) continue;

                if (!has)
                {
                    b = r.bounds;
                    has = true;
                }
                else
                {
                    b.Encapsulate(r.bounds);
                }
            }

            return has;
        }

        private static IEnumerable<GameObject> EnumerateSceneGameObjects()
        {
            // Supports: open scenes + prefab stage.
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.scene.IsValid())
            {
                var roots = stage.scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    foreach (var go in EnumerateHierarchy(roots[i]))
                        yield return go;
                }
                yield break;
            }

            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                var roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    foreach (var go in EnumerateHierarchy(roots[i]))
                        yield return go;
                }
            }
        }

        private static IEnumerable<GameObject> EnumerateHierarchy(GameObject root)
        {
            if (root == null) yield break;

            var stack = new Stack<Transform>();
            stack.Push(root.transform);

            while (stack.Count > 0)
            {
                var tf = stack.Pop();
                if (tf == null) continue;

                var go = tf.gameObject;
                if (go != null)
                    yield return go;

                for (int i = 0; i < tf.childCount; i++)
                    stack.Push(tf.GetChild(i));
            }
        }

        private static void MarkScenesDirty()
        {
            try
            {
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage != null)
                {
                    EditorSceneManager.MarkSceneDirty(stage.scene);
                    return;
                }
            }
            catch { }

            try
            {
                for (int s = 0; s < SceneManager.sceneCount; s++)
                {
                    var scene = SceneManager.GetSceneAt(s);
                    if (scene.IsValid() && scene.isLoaded)
                        EditorSceneManager.MarkSceneDirty(scene);
                }
            }
            catch { }
        }
    }
}
#endif
