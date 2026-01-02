#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abyssbound.EditorTools
{
    public static class MerchantNuclearRestoreEditor
    {
        private const string MenuPath = "Abyssbound/Merchants/NUCLEAR Restore Stable Merchant Campsite";

        private const string SceneRootName = "Scene_Root";
        private const string TownName = "Town";
        private const string CampsiteHubRootName = "CampsiteHub_Root";
        private const string MerchantsContainerName = "Merchants";

        private const string InteractionPointName = "InteractionPoint";
        private const string MerchantPillName = "MerchantPill";
        private const string MerchantPillOrphanName = "MerchantPill_ORPHAN";

        private const string CookingBonfirePreferredName = "Bonfire_CookingStation";

        private const float MerchantRingRadius = 5.5f;

        private struct MerchantSpec
        {
            public string name;
            public Vector3 offset;
        }

        [MenuItem(MenuPath)]
        public static void NuclearRestoreStableMerchantCampsite()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[Abyssbound] No active scene loaded.");
                return;
            }

            int merchantsFound = 0;
            int merchantsCreated = 0;
            int clickTargetsRemoved = 0;
            int collidersDisabled = 0;
            int pillsCreated = 0;
            int pillsReattached = 0;
            int worldInteractionEnsured = 0;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("NUCLEAR Restore Stable Merchant Campsite");

            try
            {
                var tMerchantShop = FindBestTypeByName("MerchantShop");
                var tClickTarget = FindBestTypeByName("MerchantDoorClickTarget");
                var tRaycaster = FindBestTypeByName("MerchantClickRaycaster");
                var tHover = FindBestTypeByName("MerchantDoorHoverHighlighter");

                if (tClickTarget == null)
                {
                    Debug.LogError("[Abyssbound] Could not find type 'MerchantDoorClickTarget'. Ensure scripts compiled.");
                    return;
                }

                // 2) Hub center bonfire
                Vector3 bonfireCenter = Vector3.zero;
                float bonfireY = 0f;
                var bonfireGo = GameObject.Find(CookingBonfirePreferredName);
                if (bonfireGo != null)
                {
                    bonfireCenter = bonfireGo.transform.position;
                    bonfireY = bonfireCenter.y;
                }
                else
                {
                    var bonfireVisualsType = FindBestTypeByName("BonfireVisuals");
                    if (bonfireVisualsType != null)
                    {
                        var any = FindFirstComponentInScene(scene, bonfireVisualsType);
                        if (any != null)
                        {
                            bonfireCenter = any.transform.position;
                            bonfireY = bonfireCenter.y;
                        }
                        else
                        {
                            Debug.LogWarning("[Abyssbound] No BonfireVisuals found in active scene; using Vector3.zero as center.");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[Abyssbound] Could not find type 'BonfireVisuals'; using Vector3.zero as center.");
                    }
                }

                // 3) Find Town root
                var town = FindOrCreateTown(scene);

                // 4) Ensure CampsiteHub_Root + Merchants
                var hub = EnsureChild(town.transform, CampsiteHubRootName, out _);
                var merchantsContainer = EnsureChild(hub.transform, MerchantsContainerName, out _);

                // 7C) Precompute orphan pills (named MerchantPill, not under any MerchantShop chain)
                var orphanPills = FindAllTransformsNamedInScene(scene, MerchantPillName)
                    .Where(t => t != null && !HasComponentInParentChain(t, tMerchantShop))
                    .OrderBy(GetTransformPath)
                    .ToList();

                // 5) Locate 4 merchant roots by name anywhere
                var specs = new[]
                {
                    new MerchantSpec { name = "WeaponsGearMerchant",       offset = new Vector3(+MerchantRingRadius, 0f, 0f) },
                    new MerchantSpec { name = "ConsumablesMerchant",       offset = new Vector3(0f, 0f, +MerchantRingRadius) },
                    new MerchantSpec { name = "SkillingSuppliesMerchant",  offset = new Vector3(-MerchantRingRadius, 0f, 0f) },
                    new MerchantSpec { name = "WorkshopMerchant",          offset = new Vector3(0f, 0f, -MerchantRingRadius) },
                };

                var restoredMerchantRoots = new List<Transform>(specs.Length);

                for (int i = 0; i < specs.Length; i++)
                {
                    var spec = specs[i];

                    var foundCandidates = FindAllTransformsNamedInScene(scene, spec.name);
                    Transform merchantRoot = ChooseDeterministicFirst(foundCandidates);

                    if (merchantRoot == null)
                    {
                        var go = new GameObject(spec.name);
                        Undo.RegisterCreatedObjectUndo(go, "Create Merchant Root");
                        go.transform.SetParent(merchantsContainer.transform, false);
                        go.transform.position = new Vector3(bonfireCenter.x + spec.offset.x, bonfireY, bonfireCenter.z + spec.offset.z);
                        merchantRoot = go.transform;
                        merchantsCreated++;
                    }
                    else
                    {
                        merchantsFound++;
                    }

                    // 6) Reparent under CampsiteHub_Root/Merchants
                    if (merchantRoot.parent != merchantsContainer.transform)
                    {
                        Undo.SetTransformParent(merchantRoot, merchantsContainer.transform, "Reparent Merchant Under Campsite Merchants");
                    }

                    // Ensure MerchantShop component exists (do not destroy)
                    if (tMerchantShop != null)
                    {
                        var existingShop = merchantRoot.GetComponent(tMerchantShop);
                        if (existingShop == null)
                        {
                            Undo.AddComponent(merchantRoot.gameObject, tMerchantShop);
                        }
                    }

                    // 7A) Enforce InteractionPoint
                    var ip = EnsureDirectChild(merchantRoot, InteractionPointName, out _);

                    var sphere = EnsureSphereCollider(ip);
                    Undo.RecordObject(sphere, "Configure Interaction Sphere");
                    sphere.enabled = true;
                    sphere.isTrigger = true;

                    var clickTarget = EnsureComponent(ip, tClickTarget);

                    // Remove other click targets under merchant
                    clickTargetsRemoved += RemoveOtherComponentsUnder(merchantRoot, tClickTarget, exceptOn: ip);

                    // 7B) Disable extra colliders (except sphere)
                    collidersDisabled += DisableAllOtherCollidersUnder(merchantRoot, except: sphere);

                    // 7C) Ensure MerchantPill
                    var pill = FindDirectChildByName(merchantRoot, MerchantPillName);
                    if (pill == null)
                    {
                        var closestOrphan = TakeClosest(orphanPills, merchantRoot.position);
                        if (closestOrphan != null)
                        {
                            Undo.SetTransformParent(closestOrphan, merchantRoot, "Reattach MerchantPill");
                            Undo.RecordObject(closestOrphan, "Rename MerchantPill");
                            closestOrphan.name = MerchantPillName;
                            pill = closestOrphan;
                            pillsReattached++;
                        }
                    }

                    if (pill == null)
                    {
                        pill = CreateMerchantPill(merchantRoot);
                        pillsCreated++;
                    }

                    // 7D) Fit sphere to pill bounds
                    FitInteractionSphereToPill(sphere, ip.transform, pill, merchantRoot.name);

                    // 7E) Assign highlight renderers to pill renderers only
                    AssignHighlightRenderers(clickTarget, pill);

                    restoredMerchantRoots.Add(merchantRoot);
                }

                // Rename remaining orphans
                for (int i = 0; i < orphanPills.Count; i++)
                {
                    var t = orphanPills[i];
                    if (t == null) continue;
                    Undo.RecordObject(t, "Rename leftover orphan pill");
                    t.name = MerchantPillOrphanName;
                }

                // 8) Ensure MerchantWorldInteraction exists
                if (EnsureMerchantWorldInteraction(tRaycaster, tHover))
                    worldInteractionEnsured = 1;

                // 9) Re-layout merchants around bonfire
                for (int i = 0; i < specs.Length; i++)
                {
                    var spec = specs[i];
                    var merchantRoot = restoredMerchantRoots[i];
                    if (merchantRoot == null) continue;

                    var pos = new Vector3(bonfireCenter.x + spec.offset.x, bonfireY, bonfireCenter.z + spec.offset.z);
                    Undo.RecordObject(merchantRoot, "Layout Merchants Around Bonfire");
                    merchantRoot.position = pos;
                }

                EditorSceneManager.MarkSceneDirty(scene);

                Debug.Log(
                    "[Abyssbound] NUCLEAR restore complete.\n" +
                    $"  Scene: {scene.name}\n" +
                    $"  Merchants found: {merchantsFound}\n" +
                    $"  Merchants created: {merchantsCreated}\n" +
                    $"  ClickTargets removed: {clickTargetsRemoved}\n" +
                    $"  Colliders disabled: {collidersDisabled}\n" +
                    $"  Pills created: {pillsCreated}\n" +
                    $"  Pills reattached: {pillsReattached}\n" +
                    $"  MerchantWorldInteraction ensured: {worldInteractionEnsured}");
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        private static GameObject FindOrCreateTown(Scene scene)
        {
            GameObject town = null;

            var sceneRoot = GameObject.Find(SceneRootName);
            if (sceneRoot != null)
            {
                var t = sceneRoot.transform.Find(TownName);
                if (t != null) town = t.gameObject;
            }

            if (town == null)
                town = GameObject.Find(TownName);

            if (town == null)
            {
                town = new GameObject(TownName);
                Undo.RegisterCreatedObjectUndo(town, "Create Town");
            }

            return town;
        }

        private static GameObject EnsureChild(Transform parent, string name, out bool created)
        {
            created = false;
            if (parent == null) return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c != null && c.name == name)
                    return c.gameObject;
            }

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            created = true;
            return go;
        }

        private static GameObject EnsureDirectChild(Transform parent, string name, out bool created)
        {
            created = false;
            if (parent == null) return null;

            Transform first = null;
            bool hasExtras = false;
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c == null) continue;
                if (c.name != name) continue;

                if (first == null) first = c;
                else hasExtras = true;
            }

            if (hasExtras)
                Debug.LogWarning($"[Abyssbound] '{parent.name}' has multiple direct '{name}' children; using first.", parent.gameObject);

            if (first != null)
                return first.gameObject;

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            created = true;
            return go;
        }

        private static Transform FindDirectChildByName(Transform parent, string name)
        {
            if (parent == null) return null;

            Transform first = null;
            bool hasExtras = false;

            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c == null) continue;
                if (c.name != name) continue;

                if (first == null) first = c;
                else hasExtras = true;
            }

            if (hasExtras)
                Debug.LogWarning($"[Abyssbound] '{parent.name}' has multiple direct '{name}' children; using first.", parent.gameObject);

            return first;
        }

        private static SphereCollider EnsureSphereCollider(GameObject interactionPoint)
        {
            var sphere = interactionPoint.GetComponent<SphereCollider>();
            if (sphere == null)
                sphere = Undo.AddComponent<SphereCollider>(interactionPoint);

            return sphere;
        }

        private static Component EnsureComponent(GameObject go, Type t)
        {
            if (go == null || t == null) return null;

            var existing = go.GetComponent(t);
            if (existing != null) return existing;

            return Undo.AddComponent(go, t);
        }

        private static int RemoveOtherComponentsUnder(Transform root, Type t, GameObject exceptOn)
        {
            if (root == null || t == null) return 0;

            int removed = 0;
            var comps = root.GetComponentsInChildren(t, true);
            for (int i = 0; i < comps.Length; i++)
            {
                var c = comps[i] as Component;
                if (c == null) continue;

                if (exceptOn != null && c.gameObject == exceptOn)
                    continue;

                Undo.DestroyObjectImmediate(c);
                removed++;
            }

            return removed;
        }

        private static int DisableAllOtherCollidersUnder(Transform root, Collider except)
        {
            if (root == null) return 0;

            int disabled = 0;
            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                var c = colliders[i];
                if (c == null) continue;
                if (ReferenceEquals(c, except)) continue;

                if (!c.enabled) continue;

                Undo.RecordObject(c, "Disable Merchant Collider");
                c.enabled = false;
                disabled++;
            }

            return disabled;
        }

        private static Transform CreateMerchantPill(Transform merchantRoot)
        {
            var pill = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            pill.name = MerchantPillName;
            Undo.RegisterCreatedObjectUndo(pill, "Create MerchantPill");

            pill.transform.SetParent(merchantRoot, false);
            pill.transform.localPosition = Vector3.zero;
            pill.transform.localRotation = Quaternion.identity;
            pill.transform.localScale = Vector3.one;

            // Remove primitive collider so pill is visual-only.
            var col = pill.GetComponent<Collider>();
            if (col != null)
                Undo.DestroyObjectImmediate(col);

            return pill.transform;
        }

        private static void FitInteractionSphereToPill(SphereCollider sphere, Transform ip, Transform pill, string merchantNameForLog)
        {
            if (sphere == null || ip == null || pill == null)
                return;

            var renderer = pill.GetComponentInChildren<Renderer>(true);
            if (renderer == null)
            {
                Debug.LogWarning($"[Abyssbound] '{merchantNameForLog}': MerchantPill has no Renderer; cannot fit InteractionPoint sphere.", pill.gameObject);
                return;
            }

            Bounds b = renderer.bounds;
            Vector3 worldCenter = b.center;
            Vector3 extents = b.extents;

            float radiusWorld = Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z)) * 1.05f;
            Vector3 localCenter = ip.InverseTransformPoint(worldCenter);

            var ls = ip.lossyScale;
            float scale = Mathf.Max(Mathf.Abs(ls.x), Mathf.Max(Mathf.Abs(ls.y), Mathf.Abs(ls.z)));
            float safeScale = Mathf.Max(scale, 0.0001f);

            Undo.RecordObject(sphere, "Fit Interaction Sphere To Pill");
            sphere.enabled = true;
            sphere.isTrigger = true;
            sphere.center = localCenter;
            sphere.radius = radiusWorld / safeScale;
        }

        private static void AssignHighlightRenderers(Component clickTarget, Transform pill)
        {
            if (clickTarget == null || pill == null)
                return;

            var renderers = pill.GetComponentsInChildren<Renderer>(true);

            try
            {
                var so = new SerializedObject(clickTarget);

                SerializedProperty prop = null;
                prop = so.FindProperty("highlightRenderers") ??
                       so.FindProperty("_highlightRenderers") ??
                       so.FindProperty("HighlightRenderers");

                if (prop == null || !prop.isArray)
                {
                    Debug.LogWarning($"[Abyssbound] Could not find highlight renderer array property on {clickTarget.GetType().Name}.", clickTarget);
                    return;
                }

                Undo.RecordObject(clickTarget, "Assign ClickTarget Highlight Renderers");

                prop.arraySize = renderers != null ? renderers.Length : 0;
                if (renderers != null)
                {
                    for (int i = 0; i < renderers.Length; i++)
                    {
                        var el = prop.GetArrayElementAtIndex(i);
                        el.objectReferenceValue = renderers[i];
                    }
                }

                so.ApplyModifiedProperties();
            }
            catch
            {
                Debug.LogWarning("[Abyssbound] Failed assigning highlight renderers via SerializedObject.", clickTarget);
            }
        }

        private static bool EnsureMerchantWorldInteraction(Type tRaycaster, Type tHover)
        {
            bool ensured = false;

            var root = GameObject.Find("MerchantWorldInteraction");
            if (root == null)
            {
                root = new GameObject("MerchantWorldInteraction");
                Undo.RegisterCreatedObjectUndo(root, "Create MerchantWorldInteraction");
                ensured = true;
            }

            if (tRaycaster != null && root.GetComponent(tRaycaster) == null)
            {
                Undo.AddComponent(root, tRaycaster);
                ensured = true;
            }

            Component hover = null;
            if (tHover != null)
            {
                hover = root.GetComponent(tHover);
                if (hover == null)
                {
                    hover = Undo.AddComponent(root, tHover);
                    ensured = true;
                }
            }

            if (hover != null)
            {
                TrySetSerializedFloat(hover, "switchDistanceEpsilon", 1.0f);
                TrySetSerializedFloat(hover, "lostTargetGraceSeconds", 0.25f);

                TrySetSerializedBool(hover, "diagnosticsEnabled", false);
                TrySetSerializedBool(hover, "showDiagnostics", false);
                TrySetSerializedBool(hover, "verbose", false);
            }

            return ensured;
        }

        private static void TrySetSerializedFloat(UnityEngine.Object obj, string propName, float value)
        {
            if (obj == null) return;
            try
            {
                var so = new SerializedObject(obj);
                var p = so.FindProperty(propName);
                if (p != null && p.propertyType == SerializedPropertyType.Float)
                {
                    Undo.RecordObject(obj, "Set " + propName);
                    p.floatValue = value;
                    so.ApplyModifiedProperties();
                }
            }
            catch { }
        }

        private static void TrySetSerializedBool(UnityEngine.Object obj, string propName, bool value)
        {
            if (obj == null) return;
            try
            {
                var so = new SerializedObject(obj);
                var p = so.FindProperty(propName);
                if (p != null && p.propertyType == SerializedPropertyType.Boolean)
                {
                    Undo.RecordObject(obj, "Set " + propName);
                    p.boolValue = value;
                    so.ApplyModifiedProperties();
                }
            }
            catch { }
        }

        private static Component FindFirstComponentInScene(Scene scene, Type type)
        {
            if (type == null) return null;

            var all = Resources.FindObjectsOfTypeAll(type);
            for (int i = 0; i < all.Length; i++)
            {
                var c = all[i] as Component;
                if (c == null) continue;
                if (c.gameObject == null) continue;
                if (c.gameObject.scene != scene) continue;
                return c;
            }

            return null;
        }

        private static List<Transform> FindAllTransformsNamedInScene(Scene scene, string name)
        {
            var results = new List<Transform>(16);
            if (!scene.IsValid() || !scene.isLoaded)
                return results;

            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (root == null) continue;

                var all = root.GetComponentsInChildren<Transform>(true);
                for (int t = 0; t < all.Length; t++)
                {
                    var tr = all[t];
                    if (tr != null && tr.name == name)
                        results.Add(tr);
                }
            }

            // Deterministic ordering.
            results.Sort((a, b) => string.CompareOrdinal(GetTransformPath(a), GetTransformPath(b)));
            return results;
        }

        private static Transform ChooseDeterministicFirst(List<Transform> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return null;

            candidates.Sort((a, b) => string.CompareOrdinal(GetTransformPath(a), GetTransformPath(b)));
            return candidates[0];
        }

        private static Transform TakeClosest(List<Transform> candidates, Vector3 to)
        {
            if (candidates == null || candidates.Count == 0)
                return null;

            int bestIdx = -1;
            float bestDistSq = float.PositiveInfinity;
            string bestPath = null;

            for (int i = 0; i < candidates.Count; i++)
            {
                var t = candidates[i];
                if (t == null) continue;

                float d = (t.position - to).sqrMagnitude;
                if (d < bestDistSq)
                {
                    bestDistSq = d;
                    bestIdx = i;
                    bestPath = GetTransformPath(t);
                }
                else if (Mathf.Approximately(d, bestDistSq))
                {
                    var p = GetTransformPath(t);
                    if (bestPath == null || string.CompareOrdinal(p, bestPath) < 0)
                    {
                        bestIdx = i;
                        bestPath = p;
                    }
                }
            }

            if (bestIdx < 0)
                return null;

            var chosen = candidates[bestIdx];
            candidates.RemoveAt(bestIdx);
            return chosen;
        }

        private static bool HasComponentInParentChain(Transform t, Type componentType)
        {
            if (componentType == null) return false;

            var cur = t;
            while (cur != null)
            {
                if (cur.GetComponent(componentType) != null)
                    return true;
                cur = cur.parent;
            }
            return false;
        }

        private static string GetTransformPath(Transform t)
        {
            if (t == null) return string.Empty;

            var stack = new Stack<string>();
            var cur = t;
            while (cur != null)
            {
                stack.Push(cur.name);
                cur = cur.parent;
            }
            return string.Join("/", stack);
        }

        private static Type FindBestTypeByName(string typeName)
        {
            Type best = null;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch { continue; }

                for (int i = 0; i < types.Length; i++)
                {
                    var t = types[i];
                    if (t == null) continue;
                    if (!string.Equals(t.Name, typeName, StringComparison.Ordinal))
                        continue;

                    // Prefer the one that looks like Abyss.Shop.* if present, but don’t require it.
                    if (best == null)
                    {
                        best = t;
                        continue;
                    }

                    string full = t.FullName ?? string.Empty;
                    string bestFull = best.FullName ?? string.Empty;

                    bool looksPreferred = full.IndexOf("Abyss.Shop", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool bestLooksPreferred = bestFull.IndexOf("Abyss.Shop", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (looksPreferred && !bestLooksPreferred)
                        best = t;
                }
            }

            return best;
        }
    }
}
#endif
