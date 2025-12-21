using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abyss.EditorTools
{
    public static class TownLegacyRootCleaner
    {
        private const string MenuDeleteSelected = "Tools/Abyss/Town/Safely Delete Selected Root";

        [MenuItem(MenuDeleteSelected)]
        public static void SafelyDeleteSelectedRoot()
        {
            var root = Selection.activeGameObject;
            if (root == null)
            {
                Debug.LogWarning("[TownLegacyRootCleaner] Select the old town root GameObject you want to delete (e.g. the broken 'Town/EdgevilleHub_Root').");
                return;
            }

            var scene = root.scene;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning("[TownLegacyRootCleaner] Selected object is not part of a loaded scene.");
                return;
            }

            // Gather all UnityEngine.Objects under the selected root that might be referenced.
            var subtreeIds = new HashSet<int>();
            try
            {
                subtreeIds.Add(root.GetInstanceID());
                subtreeIds.Add(root.transform.GetInstanceID());

                var comps = root.GetComponentsInChildren<Component>(true);
                for (int i = 0; i < comps.Length; i++)
                {
                    var c = comps[i];
                    if (c == null) continue;
                    subtreeIds.Add(c.GetInstanceID());
                    if (c.gameObject != null) subtreeIds.Add(c.gameObject.GetInstanceID());
                }

                var transforms = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    var t = transforms[i];
                    if (t == null) continue;
                    subtreeIds.Add(t.GetInstanceID());
                    if (t.gameObject != null) subtreeIds.Add(t.gameObject.GetInstanceID());
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[TownLegacyRootCleaner] Failed to scan selection subtree: " + e.Message);
                return;
            }

            // Scan all scene components for serialized references into that subtree.
            var refs = new List<SerializedRef>(64);
#if UNITY_2022_2_OR_NEWER
            var allComponents = UnityEngine.Object.FindObjectsByType<Component>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var allComponents = UnityEngine.Object.FindObjectsOfType<Component>(true);
#endif
            for (int i = 0; i < allComponents.Length; i++)
            {
                var comp = allComponents[i];
                if (comp == null) continue;

                // Only consider references from this same scene.
                if (comp.gameObject == null || comp.gameObject.scene != scene)
                    continue;

                // Skip anything under the root (self-references are fine).
                if (comp.transform != null && comp.transform.IsChildOf(root.transform))
                    continue;

                try
                {
                    var so = new SerializedObject(comp);
                    var it = so.GetIterator();
                    bool enterChildren = true;
                    while (it.NextVisible(enterChildren))
                    {
                        enterChildren = false;

                        if (it.propertyType != SerializedPropertyType.ObjectReference)
                            continue;

                        var obj = it.objectReferenceValue;
                        if (obj == null)
                            continue;

                        if (!subtreeIds.Contains(obj.GetInstanceID()))
                            continue;

                        refs.Add(new SerializedRef
                        {
                            Source = comp,
                            PropertyPath = it.propertyPath,
                            Target = obj
                        });

                        // Avoid spamming duplicate properties on huge components.
                        if (refs.Count > 2000)
                            break;
                    }
                }
                catch
                {
                    // Some components may not be serializable; ignore.
                }

                if (refs.Count > 2000)
                    break;
            }

            if (refs.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"[TownLegacyRootCleaner] Found {refs.Count} serialized reference(s) into '{GetScenePath(root)}'.");
                sb.AppendLine("Delete is NOT recommended unless you understand these references:");

                int show = Mathf.Min(refs.Count, 30);
                for (int i = 0; i < show; i++)
                {
                    var r = refs[i];
                    sb.AppendLine($"- {FormatSource(r.Source)} -> {r.PropertyPath} => {FormatTarget(r.Target)}");
                }

                if (refs.Count > show)
                    sb.AppendLine($"(Only showing first {show} refs; see full list in Console by rerunning after narrowing selection.)");

                Debug.LogWarning(sb.ToString());

                int choice = EditorUtility.DisplayDialogComplex(
                    "Safely Delete Selected Root",
                    $"Found {refs.Count} serialized reference(s) pointing into the selected root.\n\n" +
                    "Recommended: do NOT delete until those references are fixed.\n\n" +
                    "What do you want to do?",
                    "Cancel",
                    "Delete Anyway",
                    "Select First Reference");

                if (choice == 2)
                {
                    Selection.activeObject = refs[0].Source;
                    EditorGUIUtility.PingObject(refs[0].Source);
                    return;
                }

                if (choice != 1)
                    return;
            }
            else
            {
                bool ok = EditorUtility.DisplayDialog(
                    "Safely Delete Selected Root",
                    "No serialized references into the selected root were found in this scene.\n\nDelete it now?",
                    "Delete",
                    "Cancel");

                if (!ok)
                    return;
            }

            Undo.DestroyObjectImmediate(root);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[TownLegacyRootCleaner] Deleted '{root.name}' via Undo.");
        }

        private struct SerializedRef
        {
            public Component Source;
            public string PropertyPath;
            public UnityEngine.Object Target;
        }

        private static string FormatSource(Component c)
        {
            if (c == null) return "<null>";
            return $"{c.GetType().Name} on '{GetScenePath(c.gameObject)}'";
        }

        private static string FormatTarget(UnityEngine.Object o)
        {
            if (o == null) return "<null>";

            if (o is Component comp && comp != null)
                return $"{comp.GetType().Name} on '{GetScenePath(comp.gameObject)}'";

            if (o is GameObject go && go != null)
                return $"GameObject '{GetScenePath(go)}'";

            return $"{o.GetType().Name} '{o.name}'";
        }

        private static string GetScenePath(GameObject go)
        {
            if (go == null) return "<null>";
            var t = go.transform;
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }
    }
}
