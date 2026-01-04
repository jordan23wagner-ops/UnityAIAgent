#if UNITY_EDITOR
using System;
using Abyssbound.WorldInteraction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abyssbound.EditorTools.Fishing
{
    public static class FixFishingSpotsMenu
    {
        private const string MenuPath = "Tools/Abyssbound/Fix Fishing Spots";
        private const string HighlightProxyName = "HighlightProxy";
        private const string HighlightProxyMaterialPath = "Assets/Resources/Materials/WorldInteractionHighlightProxy.mat";

        [MenuItem(MenuPath)]
        public static void FixFishingSpotsInActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning("[FixFishingSpots] No active scene loaded.");
                return;
            }

            int total = 0;
            int spheresAdded = 0;
            int spheresFixed = 0;
            int boxesDisabled = 0;
            int highlightFixed = 0;
            int prefabsPatched = 0;

            var prefabPathsToPatch = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

            FishingSpotInteractable[] spots = null;
            try
            {
#if UNITY_2022_2_OR_NEWER
                spots = UnityEngine.Object.FindObjectsByType<FishingSpotInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
                spots = UnityEngine.Object.FindObjectsOfType<FishingSpotInteractable>();
#endif
            }
            catch { spots = null; }

            if (spots == null || spots.Length == 0)
            {
                Debug.Log("[FixFishingSpots] No FishingSpotInteractable found in active scene.");
                return;
            }

            for (int i = 0; i < spots.Length; i++)
            {
                var spot = spots[i];
                if (spot == null) continue;

                var go = spot.gameObject;
                if (go == null) continue;
                if (go.scene != scene) continue;

                total++;

                try
                {
                    Undo.RegisterFullObjectHierarchyUndo(go, "Fix Fishing Spot");
                }
                catch { }

                // 1) Collider enforcement
                var sphere = go.GetComponent<SphereCollider>();
                if (sphere == null)
                {
                    try
                    {
                        sphere = Undo.AddComponent<SphereCollider>(go);
                        spheresAdded++;
                    }
                    catch
                    {
                        sphere = go.AddComponent<SphereCollider>();
                        spheresAdded++;
                    }
                }

                if (sphere != null)
                {
                    bool changed = false;
                    if (!sphere.enabled) { sphere.enabled = true; changed = true; }
                    if (!sphere.isTrigger) { sphere.isTrigger = true; changed = true; }

                    // Fit the sphere to existing trigger bounds if possible.
                    if (sphere.radius <= 0.01f)
                    {
                        if (TryGetTriggerBounds(go.transform, out var b) || TryGetRendererBounds(go.transform, out b))
                        {
                            try
                            {
                                sphere.center = go.transform.InverseTransformPoint(b.center);
                                sphere.radius = Mathf.Max(0.5f, Mathf.Max(b.extents.x, b.extents.z));
                                changed = true;
                            }
                            catch { }
                        }
                        else
                        {
                            sphere.radius = 1.5f;
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        spheresFixed++;
                        EditorUtility.SetDirty(sphere);
                    }
                }

                // Disable BoxColliders (fishing spots only). Prefer to disable trigger boxes anywhere in the spot hierarchy.
                try
                {
                    var boxes = go.GetComponentsInChildren<BoxCollider>(true);
                    if (boxes != null)
                    {
                        for (int b = 0; b < boxes.Length; b++)
                        {
                            var box = boxes[b];
                            if (box == null) continue;
                            if (!box.enabled) continue;

                            // Only target interaction-style colliders.
                            if (box.isTrigger || box.transform == go.transform)
                            {
                                box.enabled = false;
                                boxesDisabled++;
                                EditorUtility.SetDirty(box);
                            }
                        }
                    }
                }
                catch { }

                // Let the component run its own enforcement as well.
                try { spot.SendMessage("EnsureSingleInteractionTriggerCollider", SendMessageOptions.DontRequireReceiver); } catch { }

                // 2) Highlight enforcement
                try { spot.SendMessage("EnsureHighlightSetup", SendMessageOptions.DontRequireReceiver); } catch { }

                if (!EnsureHighlightRenderersNonEmpty(spot))
                {
                    if (EnsureHighlightProxyAndAssign(spot))
                        highlightFixed++;
                }
                else
                {
                    // Ensure HighlightProxy stays hidden by default.
                    var proxy = go.transform.Find(HighlightProxyName);
                    if (proxy != null)
                    {
                        var mr = proxy.GetComponent<MeshRenderer>();
                        if (mr != null && mr.enabled)
                        {
                            mr.enabled = false;
                            EditorUtility.SetDirty(mr);
                        }

                        RemoveAllColliders(proxy.gameObject);
                    }
                }

                EditorUtility.SetDirty(spot);

                // 3) Persist changes
                try
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(go))
                    {
                        var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
                        if (!string.IsNullOrWhiteSpace(path))
                            prefabPathsToPatch.Add(path);
                    }
                }
                catch
                {
                    // Keep scene overrides.
                }
            }

            // Patch prefab assets directly (safer than applying instance overrides).
            try
            {
                foreach (var path in prefabPathsToPatch)
                {
                    if (TryPatchPrefabAtPath(path))
                        prefabsPatched++;
                }
            }
            catch { }

            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log(
                $"[FixFishingSpots] Done. scene='{scene.name}' total={total} " +
                $"spheresAdded={spheresAdded} spheresFixed={spheresFixed} boxesDisabled={boxesDisabled} " +
                $"highlightFixed={highlightFixed} prefabsPatched={prefabsPatched}");
        }

        private static bool TryPatchPrefabAtPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                if (root == null)
                    return false;

                bool changed = false;

                try
                {
                    var spots = root.GetComponentsInChildren<FishingSpotInteractable>(true);
                    if (spots != null)
                    {
                        for (int i = 0; i < spots.Length; i++)
                        {
                            var spot = spots[i];
                            if (spot == null) continue;

                            // Let the runtime component enforce its own invariants.
                            try { spot.SendMessage("EnsureSingleInteractionTriggerCollider", SendMessageOptions.DontRequireReceiver); } catch { }
                            try { spot.SendMessage("EnsureHighlightSetup", SendMessageOptions.DontRequireReceiver); } catch { }

                            // Enforce root SphereCollider and disable trigger BoxColliders.
                            var go = spot.gameObject;
                            if (go == null) continue;

                            var sphere = go.GetComponent<SphereCollider>();
                            if (sphere == null)
                            {
                                sphere = go.AddComponent<SphereCollider>();
                                changed = true;
                            }

                            if (sphere != null)
                            {
                                if (!sphere.enabled) { sphere.enabled = true; changed = true; }
                                if (!sphere.isTrigger) { sphere.isTrigger = true; changed = true; }
                                if (sphere.radius <= 0.01f) { sphere.radius = 1.5f; changed = true; }
                            }

                            try
                            {
                                var boxes = go.GetComponentsInChildren<BoxCollider>(true);
                                if (boxes != null)
                                {
                                    for (int b = 0; b < boxes.Length; b++)
                                    {
                                        var box = boxes[b];
                                        if (box == null) continue;
                                        if (!box.enabled) continue;

                                        if (box.isTrigger || box.transform == go.transform)
                                        {
                                            box.enabled = false;
                                            changed = true;
                                        }
                                    }
                                }
                            }
                            catch { }

                            if (!EnsureHighlightRenderersNonEmpty(spot))
                            {
                                if (EnsureHighlightProxyAndAssign(spot))
                                    changed = true;
                            }
                        }
                    }
                }
                catch { }

                if (changed)
                    PrefabUtility.SaveAsPrefabAsset(root, path);

                PrefabUtility.UnloadPrefabContents(root);
                return changed;
            }
            catch
            {
                return false;
            }
        }

        private static bool EnsureHighlightRenderersNonEmpty(FishingSpotInteractable spot)
        {
            if (spot == null) return false;
            try
            {
                var rs = spot.HighlightRenderers;
                if (rs == null || rs.Length == 0) return false;
                for (int i = 0; i < rs.Length; i++)
                {
                    if (rs[i] != null) return true;
                }
            }
            catch { }
            return false;
        }

        private static bool EnsureHighlightProxyAndAssign(FishingSpotInteractable spot)
        {
            if (spot == null) return false;

            var go = spot.gameObject;
            if (go == null) return false;

            var proxyTf = go.transform.Find(HighlightProxyName);
            GameObject proxyGo = proxyTf != null ? proxyTf.gameObject : null;

            if (proxyGo == null)
            {
                proxyGo = new GameObject(HighlightProxyName);
                proxyGo.transform.SetParent(go.transform, worldPositionStays: false);
            }

            // Ensure no collider (proxy should never affect raycasts).
            RemoveAllColliders(proxyGo);

            var mf = proxyGo.GetComponent<MeshFilter>();
            if (mf == null) mf = proxyGo.AddComponent<MeshFilter>();

            var mr = proxyGo.GetComponent<MeshRenderer>();
            if (mr == null) mr = proxyGo.AddComponent<MeshRenderer>();

            // Minimal mesh; material is optional (highlighter uses MPB).
            try
            {
                var mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                if (mesh == null) mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                mf.sharedMesh = mesh;
            }
            catch { }

            try
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(HighlightProxyMaterialPath);
                if (mat != null)
                    mr.sharedMaterial = mat;
            }
            catch { }

            mr.enabled = false;

            // Set scale to roughly match the sphere trigger.
            try
            {
                var sphere = go.GetComponent<SphereCollider>();
                if (sphere != null)
                {
                    float d = Mathf.Max(0.5f, sphere.radius * 2f);
                    proxyGo.transform.localPosition = sphere.center + Vector3.up * 0.05f;
                    proxyGo.transform.localRotation = Quaternion.identity;
                    proxyGo.transform.localScale = new Vector3(d, 0.05f, d);
                }
            }
            catch { }

            // Assign highlightRenderers via serialization (field is declared on WorldInteractable base).
            try
            {
                var so = new SerializedObject(spot);
                var p = so.FindProperty("highlightRenderers");
                if (p != null)
                {
                    p.arraySize = 1;
                    p.GetArrayElementAtIndex(0).objectReferenceValue = mr;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(spot);
                }
            }
            catch { }

            EditorUtility.SetDirty(proxyGo);
            EditorUtility.SetDirty(mf);
            EditorUtility.SetDirty(mr);
            return true;
        }

        private static void RemoveAllColliders(GameObject go)
        {
            if (go == null) return;
            try
            {
                var cols = go.GetComponentsInChildren<Collider>(true);
                if (cols == null) return;
                for (int i = 0; i < cols.Length; i++)
                {
                    var c = cols[i];
                    if (c == null) continue;
                    UnityEngine.Object.DestroyImmediate(c);
                }
            }
            catch { }
        }

        private static bool TryGetTriggerBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            bool has = false;

            if (root == null) return false;

            try
            {
                var cols = root.GetComponentsInChildren<Collider>(true);
                if (cols == null || cols.Length == 0) return false;

                for (int i = 0; i < cols.Length; i++)
                {
                    var c = cols[i];
                    if (c == null) continue;
                    if (!c.enabled) continue;
                    if (!c.isTrigger) continue;

                    if (!has)
                    {
                        bounds = c.bounds;
                        has = true;
                    }
                    else
                    {
                        bounds.Encapsulate(c.bounds);
                    }
                }
            }
            catch { has = false; }

            return has;
        }

        private static bool TryGetRendererBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            bool has = false;

            if (root == null) return false;

            try
            {
                var rs = root.GetComponentsInChildren<Renderer>(true);
                if (rs == null || rs.Length == 0) return false;

                for (int i = 0; i < rs.Length; i++)
                {
                    var r = rs[i];
                    if (r == null) continue;

                    if (!has)
                    {
                        bounds = r.bounds;
                        has = true;
                    }
                    else
                    {
                        bounds.Encapsulate(r.bounds);
                    }
                }
            }
            catch { has = false; }

            return has;
        }
    }
}
#endif
