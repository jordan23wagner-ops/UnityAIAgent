#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abyssbound.EditorTools
{
    [InitializeOnLoad]
    public static class TownLayoutLockEditor
    {
        private const string PrefKey = "ABYSSBOUND_TOWN_LAYOUT_LOCKED";

        private const string MenuLock = "Abyssbound/Layout/Lock Town + Merchants (Prevent Moves)";
        private const string MenuUnlock = "Abyssbound/Layout/Unlock Town + Merchants";
        private const string MenuStatus = "Abyssbound/Layout/Status (Print Lock State)";

        private const string SceneRootName = "Scene_Root";
        private const string TownName = "Town";
        private const string CampsiteHubRootName = "CampsiteHub_Root";
        private const string MerchantWorldInteractionName = "MerchantWorldInteraction";

        private static bool _undoScheduled;
        private static double _lastWarnTime;

        static TownLayoutLockEditor()
        {
            // Auto-apply after domain reload.
            EditorApplication.delayCall += AutoApplyIfLocked;

            // Guard: revert accidental transform modifications.
            Undo.postprocessModifications -= OnPostprocessModifications;
            Undo.postprocessModifications += OnPostprocessModifications;
        }

        [MenuItem(MenuLock)]
        private static void LockTownAndMerchants()
        {
            EditorPrefs.SetBool(PrefKey, true);
            ApplyLockState(true);
            Debug.Log("[Abyssbound] Layout lock enabled (Town + Merchants). Use Unlock to move.");
        }

        [MenuItem(MenuUnlock)]
        private static void UnlockTownAndMerchants()
        {
            EditorPrefs.SetBool(PrefKey, false);
            ApplyLockState(false);
            Debug.Log("[Abyssbound] Layout lock disabled (Town + Merchants).");
        }

        [MenuItem(MenuStatus)]
        private static void PrintLockState()
        {
            bool locked = IsLocked;
            var scene = SceneManager.GetActiveScene();
            var targets = GetTargetsToLock(scene);

            Debug.Log(
                "[Abyssbound] Layout lock status:\n" +
                $"  Locked: {locked}\n" +
                $"  Scene: {(scene.IsValid() ? scene.name : "<invalid>")}\n" +
                $"  Targets detected: {targets.Count}\n" +
                $"  PrefKey: {PrefKey}");
        }

        private static bool IsLocked => EditorPrefs.GetBool(PrefKey, false);

        private static void AutoApplyIfLocked()
        {
            if (!IsLocked)
                return;

            // Delay again so scenes/hierarchy are ready.
            EditorApplication.delayCall += () =>
            {
                if (IsLocked)
                    ApplyLockState(true);
            };
        }

        private static void ApplyLockState(bool locked)
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning("[Abyssbound] No active scene loaded; cannot apply layout lock state.");
                return;
            }

            var targets = GetTargetsToLock(scene);
            if (targets.Count == 0)
            {
                Debug.LogWarning("[Abyssbound] Layout lock: no targets found in scene.");
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(locked ? "Lock Town + Merchants" : "Unlock Town + Merchants");

            try
            {
                int changed = 0;

                for (int i = 0; i < targets.Count; i++)
                {
                    var root = targets[i];
                    if (root == null) continue;

                    var all = root.GetComponentsInChildren<Transform>(true);
                    for (int t = 0; t < all.Length; t++)
                    {
                        var tr = all[t];
                        if (tr == null) continue;
                        var go = tr.gameObject;
                        if (go == null) continue;

                        Undo.RecordObject(go, locked ? "Lock" : "Unlock");

                        if (locked)
                        {
                            go.hideFlags |= HideFlags.NotEditable;
                        }
                        else
                        {
                            // Requirement: clear NotEditable (set HideFlags.None).
                            go.hideFlags = HideFlags.None;
                        }

                        EditorUtility.SetDirty(go);
                        changed++;
                    }
                }

                // Requirement mentions Tools.hidden; this is a global editor state.
                Tools.hidden = locked;

                EditorSceneManager.MarkSceneDirty(scene);

                Debug.Log($"[Abyssbound] Layout lock {(locked ? "applied" : "cleared")}. Objects updated: {changed}");
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        private static List<Transform> GetTargetsToLock(Scene scene)
        {
            var targets = new List<Transform>(16);
            if (!scene.IsValid() || !scene.isLoaded)
                return targets;

            var seen = new HashSet<Transform>();

            void Add(Transform t)
            {
                if (t == null) return;
                if (t.gameObject == null) return;
                if (t.gameObject.scene != scene) return;
                if (seen.Add(t)) targets.Add(t);
            }

            // Town subtree: prefer Scene_Root/Town, else GameObject named Town.
            var town = FindTownTransform(scene);
            if (town != null) Add(town);

            // CampsiteHub_Root anywhere.
            var hubs = FindAllTransformsNamedInScene(scene, CampsiteHubRootName);
            for (int i = 0; i < hubs.Count; i++) Add(hubs[i]);

            // Any GameObject that has MerchantShop in its subtree.
            var tMerchantShop = FindBestTypeByName("MerchantShop");
            if (tMerchantShop != null)
            {
                UnityEngine.Object[] all;
                try { all = Resources.FindObjectsOfTypeAll(tMerchantShop); }
                catch { all = Array.Empty<UnityEngine.Object>(); }

                for (int i = 0; i < all.Length; i++)
                {
                    var c = all[i] as Component;
                    if (c == null) continue;
                    if (c.gameObject == null) continue;
                    if (c.gameObject.scene != scene) continue;

                    Add(c.transform);
                }
            }

            // MerchantWorldInteraction.
            var mwi = FindFirstTransformNamedInScene(scene, MerchantWorldInteractionName);
            if (mwi != null) Add(mwi);

            return targets;
        }

        private static Transform FindTownTransform(Scene scene)
        {
            // Prefer Scene_Root/Town.
            var sceneRoot = FindFirstTransformNamedInScene(scene, SceneRootName);
            if (sceneRoot != null)
            {
                var town = sceneRoot.Find(TownName);
                if (town != null) return town;
            }

            // Else any object named Town.
            return FindFirstTransformNamedInScene(scene, TownName);
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

            return results;
        }

        private static Transform FindFirstTransformNamedInScene(Scene scene, string name)
        {
            var all = FindAllTransformsNamedInScene(scene, name);
            if (all.Count == 0) return null;

            // Deterministic: choose by hierarchy path.
            all.Sort((a, b) => string.CompareOrdinal(GetTransformPath(a), GetTransformPath(b)));
            return all[0];
        }

        private static UndoPropertyModification[] OnPostprocessModifications(UndoPropertyModification[] modifications)
        {
            if (!IsLocked)
                return modifications;

            if (modifications == null || modifications.Length == 0)
                return modifications;

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
                return modifications;

            bool sawLockedTransformMove = false;

            // Detect transform edits under locked targets.
            var targets = GetTargetsToLock(scene);
            for (int i = 0; i < modifications.Length; i++)
            {
                var m = modifications[i];
                var target = m.currentValue.target as UnityEngine.Object;
                if (target == null) continue;

                Transform tr = target as Transform;
                if (tr == null)
                {
                    var comp = target as Component;
                    if (comp != null) tr = comp.transform;
                }

                if (tr == null) continue;

                if (IsUnderAnyTarget(tr, targets))
                {
                    sawLockedTransformMove = true;
                    break;
                }
            }

            if (sawLockedTransformMove)
            {
                WarnLocked();
                ScheduleUndo();
            }

            return modifications;
        }

        private static bool IsUnderAnyTarget(Transform tr, List<Transform> targets)
        {
            if (tr == null || targets == null || targets.Count == 0)
                return false;

            for (int i = 0; i < targets.Count; i++)
            {
                var root = targets[i];
                if (root == null) continue;

                var cur = tr;
                while (cur != null)
                {
                    if (cur == root)
                        return true;
                    cur = cur.parent;
                }
            }

            return false;
        }

        private static void ScheduleUndo()
        {
            if (_undoScheduled)
                return;

            _undoScheduled = true;
            EditorApplication.delayCall += () =>
            {
                _undoScheduled = false;

                if (!IsLocked)
                    return;

                try
                {
                    Undo.PerformUndo();
                }
                catch
                {
                    // If we can't undo, at least keep warning.
                }
            };
        }

        private static void WarnLocked()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - _lastWarnTime < 0.25)
                return;

            _lastWarnTime = now;
            Debug.LogWarning("[Abyssbound] Layout is locked; unlock to move Town/Merchants.");
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
