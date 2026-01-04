#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Abyss.Items;
using Abyssbound.WorldInteraction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abyssbound.EditorTools.WorldInteraction
{
    public sealed class WorldInteractionSweepWindow : EditorWindow
    {
        private const string WorldInteractableLayerName = "WorldInteractable";
        private const string Zone1SceneName = "Abyssbound_Zone1";

        private const string BasicFishingRodAssetPath = "Assets/Resources/ItemDefinitions/BasicFishingRod.asset";

        private readonly List<Issue> _issues = new List<Issue>();
        private Vector2 _scroll;
        private int _lastScannedInteractables;

        private struct Issue
        {
            public GameObject go;
            public string message;
            public bool canFix;
            public Action fix;
        }

        [MenuItem("Tools/Abyssbound/World Interaction/World Interaction Sweep...")]
        public static void Open()
        {
            GetWindow<WorldInteractionSweepWindow>(utility: false, title: "World Interaction Sweep");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("World Interaction Sweep (World objects only)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Scans and optionally fixes Mining/Forge/Bonfire/Fishing interactables. Merchants are skipped by design.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button($"Scan Scene ({Zone1SceneName})", GUILayout.Height(28)))
                    ScanZone1();

                if (GUILayout.Button("Auto-Fix Selected", GUILayout.Height(28)))
                    AutoFixSelected();

                if (GUILayout.Button("Auto-Fix All World Interactables", GUILayout.Height(28)))
                    AutoFixAll();
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField($"Last scan: {_lastScannedInteractables} interactables, {_issues.Count} issues", EditorStyles.miniBoldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _issues.Count; i++)
            {
                var issue = _issues[i];
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.ObjectField(issue.go, typeof(GameObject), allowSceneObjects: true);

                        if (GUILayout.Button("Ping", GUILayout.Width(50)))
                        {
                            if (issue.go != null)
                            {
                                EditorGUIUtility.PingObject(issue.go);
                                Selection.activeObject = issue.go;
                            }
                        }

                        using (new EditorGUI.DisabledScope(!issue.canFix || issue.fix == null))
                        {
                            if (GUILayout.Button("Fix", GUILayout.Width(50)))
                            {
                                try { issue.fix?.Invoke(); }
                                catch { }
                                // Re-scan to update issues after a fix.
                                ScanActiveScenes();
                                Repaint();
                                GUIUtility.ExitGUI();
                            }
                        }
                    }

                    EditorGUILayout.LabelField(issue.message, EditorStyles.wordWrappedLabel);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void ScanZone1()
        {
            var scenePath = FindScenePathByName(Zone1SceneName);
            if (!string.IsNullOrWhiteSpace(scenePath))
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;

                var opened = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                if (opened.IsValid())
                {
                    Debug.Log($"[WorldInteractionSweep] Opened scene: {scenePath}");
                }
            }
            else
            {
                Debug.LogWarning($"[WorldInteractionSweep] Could not find a scene asset named '{Zone1SceneName}'. Scanning active scenes instead.");
            }

            ScanActiveScenes();
        }

        private void ScanActiveScenes()
        {
            _issues.Clear();
            _lastScannedInteractables = 0;

            int wiLayer = LayerMask.NameToLayer(WorldInteractableLayerName);
            if (wiLayer < 0)
            {
                _issues.Add(new Issue
                {
                    go = null,
                    message = $"Missing layer '{WorldInteractableLayerName}'. Run World Interaction setup first.",
                    canFix = false,
                    fix = null
                });
                return;
            }

            EnsureToolItemDefsIssue();

            foreach (var interactable in FindWorldInteractablesInLoadedScenes())
            {
                if (interactable == null) continue;
                if (IsMerchantProtected(interactable.gameObject))
                    continue;

                _lastScannedInteractables++;

                // 1) Colliders
                AddColliderIssues(interactable, wiLayer);

                // 2) Highlight renderers
                AddHighlightIssues(interactable, wiLayer);

                // 3) Ensure collider-hit object can resolve interactable (via parent or proxy)
                AddResolveIssues(interactable, wiLayer);
            }
        }

        private void AutoFixSelected()
        {
            int wiLayer = LayerMask.NameToLayer(WorldInteractableLayerName);
            if (wiLayer < 0)
            {
                Debug.LogWarning($"[WorldInteractionSweep] Missing layer '{WorldInteractableLayerName}'.");
                return;
            }

            EnsureBasicFishingRodItemDef();

            var selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0)
                return;

            foreach (var go in selected)
            {
                if (go == null) continue;
                if (IsMerchantProtected(go)) continue;

                var wi = go.GetComponentInParent<WorldInteractable>();
                if (wi == null) continue;

                FixInteractable(wi, wiLayer);
            }

            MarkAllLoadedScenesDirty();
            ScanActiveScenes();
        }

        private void AutoFixAll()
        {
            int wiLayer = LayerMask.NameToLayer(WorldInteractableLayerName);
            if (wiLayer < 0)
            {
                Debug.LogWarning($"[WorldInteractionSweep] Missing layer '{WorldInteractableLayerName}'.");
                return;
            }

            EnsureBasicFishingRodItemDef();

            int fixedCount = 0;
            foreach (var wi in FindWorldInteractablesInLoadedScenes())
            {
                if (wi == null) continue;
                if (IsMerchantProtected(wi.gameObject))
                    continue;

                FixInteractable(wi, wiLayer);
                fixedCount++;
            }

            MarkAllLoadedScenesDirty();
            ScanActiveScenes();
            Debug.Log($"[WorldInteractionSweep] Auto-fix completed on {fixedCount} interactables.");
        }

        private void FixInteractable(WorldInteractable wi, int wiLayer)
        {
            if (wi == null) return;

            var root = wi.gameObject;
            Undo.RegisterFullObjectHierarchyUndo(root, "World Interaction Auto-Fix");

            // Layer
            if (root.layer != wiLayer)
                root.layer = wiLayer;

            var chosen = EnsureSingleTriggerCollider(wi, wiLayer);
            EnsureHighlightRenderers(wi, wiLayer, chosen);
            EnsureResolvableFromCollider(wi, chosen);
        }

        private void AddColliderIssues(WorldInteractable wi, int wiLayer)
        {
            var root = wi.gameObject;
            var triggers = GetEnabledTriggerColliders(root);

            if (root.layer != wiLayer)
            {
                _issues.Add(new Issue
                {
                    go = root,
                    message = "Root is not on WorldInteractable layer.",
                    canFix = true,
                    fix = () => { Undo.RecordObject(root, "Fix layer"); root.layer = wiLayer; }
                });
            }

            if (triggers.Count == 0)
            {
                _issues.Add(new Issue
                {
                    go = root,
                    message = "No enabled trigger collider found (raycast needs exactly one trigger collider).",
                    canFix = true,
                    fix = () => { FixInteractable(wi, wiLayer); }
                });
                return;
            }

            if (triggers.Count > 1)
            {
                _issues.Add(new Issue
                {
                    go = root,
                    message = $"Multiple enabled trigger colliders found ({triggers.Count}). Expected exactly 1.",
                    canFix = true,
                    fix = () => { FixInteractable(wi, wiLayer); }
                });
            }

            // Ensure chosen trigger collider is on WI layer.
            var chosen = ChoosePreferredTrigger(triggers);
            if (chosen != null && chosen.gameObject.layer != wiLayer)
            {
                _issues.Add(new Issue
                {
                    go = chosen.gameObject,
                    message = "Interaction trigger collider is not on WorldInteractable layer.",
                    canFix = true,
                    fix = () => { Undo.RecordObject(chosen.gameObject, "Fix collider layer"); chosen.gameObject.layer = wiLayer; }
                });
            }
        }

        private void AddHighlightIssues(WorldInteractable wi, int wiLayer)
        {
            var root = wi.gameObject;

            var so = new SerializedObject(wi);
            var p = so.FindProperty("highlightRenderers");
            if (p == null)
                return;

            bool empty = !p.isArray || p.arraySize == 0;
            if (!empty)
            {
                // treat all-null as empty
                bool any = false;
                for (int i = 0; i < p.arraySize; i++)
                {
                    var el = p.GetArrayElementAtIndex(i);
                    if (el != null && el.objectReferenceValue != null)
                    {
                        any = true;
                        break;
                    }
                }
                empty = !any;
            }

            if (!empty)
                return;

            _issues.Add(new Issue
            {
                go = root,
                message = "Highlight renderers list is empty. Hover highlight may not show.",
                canFix = true,
                fix = () =>
                {
                    var chosen = ChoosePreferredTrigger(GetEnabledTriggerColliders(root));
                    EnsureHighlightRenderers(wi, wiLayer, chosen);
                }
            });
        }

        private void AddResolveIssues(WorldInteractable wi, int wiLayer)
        {
            var root = wi.gameObject;
            var chosen = ChoosePreferredTrigger(GetEnabledTriggerColliders(root));
            if (chosen == null)
                return;

            var resolved = chosen.GetComponentInParent<WorldInteractable>();
            if (resolved == wi)
                return;

            if (resolved == null)
            {
                _issues.Add(new Issue
                {
                    go = chosen.gameObject,
                    message = "Trigger collider does not resolve to any WorldInteractable via parent chain.",
                    canFix = true,
                    fix = () => { FixInteractable(wi, wiLayer); }
                });
                return;
            }

            // Resolved to a different interactable.
            _issues.Add(new Issue
            {
                go = chosen.gameObject,
                message = $"Trigger collider resolves to a different WorldInteractable ({resolved.GetType().Name}). May cause wrong hover/click.",
                canFix = true,
                fix = () => { EnsureResolvableFromCollider(wi, chosen); }
            });
        }

        private static Collider EnsureSingleTriggerCollider(WorldInteractable wi, int wiLayer)
        {
            var root = wi.gameObject;
            var triggers = GetEnabledTriggerColliders(root);

            Collider chosen = ChoosePreferredTrigger(triggers);

            if (chosen == null)
            {
                var sphere = root.GetComponent<SphereCollider>();
                if (sphere == null)
                    sphere = Undo.AddComponent<SphereCollider>(root);

                sphere.isTrigger = true;
                sphere.center = Vector3.up * 1.0f;

                var b = wi.GetHoverBounds();
                sphere.radius = Mathf.Max(0.5f, Mathf.Max(b.extents.x, b.extents.z));
                chosen = sphere;
            }

            try { chosen.isTrigger = true; } catch { }
            try { chosen.gameObject.layer = wiLayer; } catch { }

            // Disable other trigger colliders.
            var all = root.GetComponentsInChildren<Collider>(includeInactive: true);
            if (all != null)
            {
                for (int i = 0; i < all.Length; i++)
                {
                    var c = all[i];
                    if (c == null || c == chosen) continue;
                    if (!c.isTrigger) continue;
                    try { c.enabled = false; } catch { }
                }
            }

            return chosen;
        }

        private static void EnsureHighlightRenderers(WorldInteractable wi, int wiLayer, Collider chosenCollider)
        {
            if (wi == null) return;

            Renderer[] rs = null;
            try { rs = wi.GetComponentsInChildren<Renderer>(includeInactive: true); }
            catch { rs = null; }

            if (rs == null || rs.Length == 0)
            {
                // Create a minimal proxy renderer.
                var proxy = EnsureHighlightProxy(wi.gameObject, chosenCollider, wiLayer);
                if (proxy != null)
                    rs = new[] { proxy };
            }

            if (rs == null || rs.Length == 0)
                return;

            try
            {
                var so = new SerializedObject(wi);
                var p = so.FindProperty("highlightRenderers");
                if (p == null || !p.isArray)
                    return;

                // Only fill if empty.
                if (p.arraySize > 0)
                {
                    bool any = false;
                    for (int i = 0; i < p.arraySize; i++)
                    {
                        var el = p.GetArrayElementAtIndex(i);
                        if (el != null && el.objectReferenceValue != null) { any = true; break; }
                    }
                    if (any)
                        return;
                }

                p.arraySize = rs.Length;
                for (int i = 0; i < rs.Length; i++)
                    p.GetArrayElementAtIndex(i).objectReferenceValue = rs[i];

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(wi);
            }
            catch { }
        }

        private static Renderer EnsureHighlightProxy(GameObject root, Collider chosenCollider, int wiLayer)
        {
            if (root == null) return null;

            Transform existing = null;
            try { existing = root.transform.Find("HighlightProxy"); } catch { existing = null; }

            GameObject proxyGo;
            if (existing == null)
            {
                proxyGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                proxyGo.name = "HighlightProxy";
                Undo.RegisterCreatedObjectUndo(proxyGo, "Create HighlightProxy");
                proxyGo.transform.SetParent(root.transform, worldPositionStays: false);
            }
            else
            {
                proxyGo = existing.gameObject;
            }

            // Remove collider on the primitive.
            try
            {
                var col = proxyGo.GetComponent<Collider>();
                if (col != null) Undo.DestroyObjectImmediate(col);
            }
            catch { }

            proxyGo.layer = wiLayer;

            var mf = proxyGo.GetComponent<MeshFilter>();
            var mr = proxyGo.GetComponent<MeshRenderer>();
            if (mf == null) mf = proxyGo.AddComponent<MeshFilter>();
            if (mr == null) mr = proxyGo.AddComponent<MeshRenderer>();

            // Place at collider center (local) + slight lift.
            Vector3 worldCenter = root.transform.position;
            try { if (chosenCollider != null) worldCenter = chosenCollider.bounds.center; }
            catch { }

            var local = root.transform.InverseTransformPoint(worldCenter);
            proxyGo.transform.localPosition = local + new Vector3(0f, 0.15f, 0f);
            proxyGo.transform.localRotation = Quaternion.identity;
            proxyGo.transform.localScale = new Vector3(0.4f, 0.08f, 0.4f);

            return mr;
        }

        private static void EnsureResolvableFromCollider(WorldInteractable wi, Collider chosenCollider)
        {
            if (wi == null || chosenCollider == null)
                return;

            var resolved = chosenCollider.GetComponentInParent<WorldInteractable>();
            if (resolved == wi)
                return;

            // Add proxy only when collider chain does not resolve to the correct interactable.
            var go = chosenCollider.gameObject;
            if (go == null) return;

            var proxy = go.GetComponent<WorldInteractableProxy>();
            if (proxy == null)
                proxy = Undo.AddComponent<WorldInteractableProxy>(go);

            proxy.SetTarget(wi);
            EditorUtility.SetDirty(proxy);
        }

        private static List<Collider> GetEnabledTriggerColliders(GameObject root)
        {
            var results = new List<Collider>();
            if (root == null) return results;

            Collider[] all = null;
            try { all = root.GetComponentsInChildren<Collider>(includeInactive: true); }
            catch { all = null; }

            if (all == null) return results;

            for (int i = 0; i < all.Length; i++)
            {
                var c = all[i];
                if (c == null) continue;
                if (!c.enabled) continue;
                if (!c.isTrigger) continue;
                results.Add(c);
            }

            return results;
        }

        private static Collider ChoosePreferredTrigger(List<Collider> triggers)
        {
            if (triggers == null || triggers.Count == 0)
                return null;

            // Prefer Sphere/Capsule. Among those, prefer the smallest bounds volume.
            Collider best = null;
            float bestScore = float.PositiveInfinity;

            for (int i = 0; i < triggers.Count; i++)
            {
                var c = triggers[i];
                if (c == null) continue;

                float typePenalty = 0f;
                if (c is SphereCollider || c is CapsuleCollider) typePenalty = 0f;
                else if (c is BoxCollider) typePenalty = 10f;
                else typePenalty = 25f;

                float vol = 99999f;
                try
                {
                    var s = c.bounds.size;
                    vol = Mathf.Max(0.0001f, s.x * s.y * s.z);
                }
                catch { vol = 99999f; }

                float score = typePenalty + vol;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = c;
                }
            }

            return best;
        }

        private static IEnumerable<WorldInteractable> FindWorldInteractablesInLoadedScenes()
        {
            WorldInteractable[] all = null;
            try
            {
#if UNITY_2023_1_OR_NEWER
                all = UnityEngine.Object.FindObjectsByType<WorldInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
                all = UnityEngine.Object.FindObjectsOfType<WorldInteractable>(true);
#endif
            }
            catch { all = null; }

            if (all == null) yield break;

            for (int i = 0; i < all.Length; i++)
            {
                var wi = all[i];
                if (wi == null) continue;
                if (!wi.gameObject.scene.IsValid()) continue;

                // Limit to known world activities or WI-layer objects.
                if (!LooksLikeTargetWorldActivity(wi))
                    continue;

                yield return wi;
            }
        }

        private static bool LooksLikeTargetWorldActivity(WorldInteractable wi)
        {
            if (wi == null) return false;

            var t = wi.GetType().Name;
            if (string.Equals(t, "MiningNode", StringComparison.Ordinal)) return true;
            if (string.Equals(t, "ForgeStation", StringComparison.Ordinal)) return true;
            if (string.Equals(t, "ForgeInteractable", StringComparison.Ordinal)) return true;
            if (string.Equals(t, "BonfireInteractable", StringComparison.Ordinal)) return true;
            if (string.Equals(t, "FishingSpotInteractable", StringComparison.Ordinal)) return true;

            // Also include anything on the WorldInteractable layer.
            int wiLayer = LayerMask.NameToLayer(WorldInteractableLayerName);
            return wiLayer >= 0 && wi.gameObject.layer == wiLayer;
        }

        private static bool IsMerchantProtected(GameObject go)
        {
            if (go == null)
                return false;

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
                    if (string.Equals(n, "MerchantWorldInteraction", StringComparison.OrdinalIgnoreCase))
                        return true;
                    if (string.Equals(n, "MerchantPill", StringComparison.OrdinalIgnoreCase))
                        return true;
                    if (string.Equals(n, "InteractionPoint", StringComparison.OrdinalIgnoreCase))
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
                            if (tn.IndexOf("MerchantWorldInteraction", StringComparison.OrdinalIgnoreCase) >= 0)
                                return true;
                        }
                    }
                    tf = tf.parent;
                }
            }
            catch { }

            // Layer-based reject (if there is a dedicated Merchant layer).
            try
            {
                int merchantLayer = LayerMask.NameToLayer("Merchant");
                if (merchantLayer >= 0)
                {
                    var tf = go.transform;
                    while (tf != null)
                    {
                        if (tf.gameObject.layer == merchantLayer)
                            return true;
                        tf = tf.parent;
                    }
                }
            }
            catch { }

            return false;
        }

        private void EnsureToolItemDefsIssue()
        {
            // Ensure Basic Fishing Rod item definition exists (editor-time convenience for tool gating).
            var existing = FindItemDefinitionByItemId(ItemIds.FishingRodBasic);
            if (existing != null)
                return;

            _issues.Add(new Issue
            {
                go = null,
                message = $"Missing ItemDefinition for '{ItemIds.FishingRodBasic}'. Fishing tool gating will always fail until created.",
                canFix = true,
                fix = () => { EnsureBasicFishingRodItemDef(); }
            });
        }

        private static ItemDefinition FindItemDefinitionByItemId(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return null;

            try
            {
                var guids = AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/Resources/ItemDefinitions" });
                for (int i = 0; i < guids.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    var def = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                    if (def == null) continue;
                    if (string.Equals(def.itemId, itemId, StringComparison.OrdinalIgnoreCase))
                        return def;
                }
            }
            catch { }

            return null;
        }

        private static void EnsureBasicFishingRodItemDef()
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Resources/ItemDefinitions");

            var def = AssetDatabase.LoadAssetAtPath<ItemDefinition>(BasicFishingRodAssetPath);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<ItemDefinition>();
                AssetDatabase.CreateAsset(def, BasicFishingRodAssetPath);
            }

            def.itemId = ItemIds.FishingRodBasic;
            def.displayName = "Basic Fishing Rod";
            def.description = "A simple fishing rod for fishing.";
            def.itemType = Abyss.Items.ItemType.Skilling;

            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[WorldInteractionSweep] Ensured ItemDefinition: {ItemIds.FishingRodBasic}");
        }

        private static void EnsureFolder(string assetPath)
        {
            assetPath = assetPath.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(assetPath))
                return;

            var parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            var name = Path.GetFileName(assetPath);

            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
                return;

            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, name);
        }

        private static string FindScenePathByName(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return null;

            try
            {
                var guids = AssetDatabase.FindAssets($"{sceneName} t:Scene");
                for (int i = 0; i < guids.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (string.IsNullOrWhiteSpace(path)) continue;

                    var fileName = Path.GetFileNameWithoutExtension(path);
                    if (string.Equals(fileName, sceneName, StringComparison.OrdinalIgnoreCase))
                        return path;
                }
            }
            catch { }

            return null;
        }

        private static void MarkAllLoadedScenesDirty()
        {
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
