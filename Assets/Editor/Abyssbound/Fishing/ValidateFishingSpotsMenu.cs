#if UNITY_EDITOR
using System;
using Abyssbound.Skills.Fishing;
using Abyssbound.WorldInteraction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abyssbound.EditorTools.Fishing
{
    public static class ValidateFishingSpotsMenu
    {
        private const string Zone1SceneName = "Abyssbound_Zone1";
        private const string WorldInteractableLayerName = "WorldInteractable";

        [MenuItem("Tools/Abyssbound/Fishing/Validate & Fix Fishing Spots (Zone1)")]
        public static void ValidateAndFixZone1()
        {
            var scenePath = FindScenePathByName(Zone1SceneName);
            if (!string.IsNullOrWhiteSpace(scenePath))
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;

                var opened = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                if (!opened.IsValid())
                {
                    Debug.LogWarning($"[Fishing][Validate] Could not open scene: {scenePath}");
                    return;
                }
            }
            else
            {
                Debug.LogWarning($"[Fishing][Validate] Could not find scene '{Zone1SceneName}'. Validating active scenes instead.");
            }

            int fixedCount = 0;
            int seen = 0;

            foreach (var go in EnumerateSceneGameObjects())
            {
                if (go == null) continue;

                // Only enforce on WorldInteraction fishing spots.
                var interactable = go.GetComponent<FishingSpotInteractable>();
                if (interactable == null) continue;

                // Still useful to assert this is actually a fishing spot.
                var spot = go.GetComponent<FishingSpot>();
                if (spot == null) continue;

                seen++;
                bool changed = false;

                // Permanent policy: fishing spots use SphereCollider triggers; BoxCollider should be disabled.
                var sphere = go.GetComponent<SphereCollider>();
                if (sphere == null)
                {
                    sphere = Undo.AddComponent<SphereCollider>(go);
                    sphere.isTrigger = true;
                    sphere.center = Vector3.up;
                    sphere.radius = 1.5f;
                    changed = true;
                }

                // Ensure triggers.
                if (sphere != null && !sphere.isTrigger) { sphere.isTrigger = true; changed = true; }

                // Ensure enabled.
                if (sphere != null && !sphere.enabled) { sphere.enabled = true; changed = true; }

                // Disable any BoxCollider triggers on this spot.
                try
                {
                    var boxes = go.GetComponentsInChildren<BoxCollider>(true);
                    if (boxes != null)
                    {
                        for (int i = 0; i < boxes.Length; i++)
                        {
                            var b = boxes[i];
                            if (b == null) continue;
                            if (!b.enabled) continue;
                            if (b.isTrigger || b.transform == go.transform)
                            {
                                b.enabled = false;
                                changed = true;
                            }
                        }
                    }
                }
                catch { }

                // Ensure highlight setup is present.
                try { interactable.SendMessage("EnsureSingleInteractionTriggerCollider", SendMessageOptions.DontRequireReceiver); } catch { }
                try { interactable.SendMessage("EnsureHighlightSetup", SendMessageOptions.DontRequireReceiver); } catch { }

                // Ensure layer is WorldInteractable if that layer exists.
                int wiLayer = LayerMask.NameToLayer(WorldInteractableLayerName);
                if (wiLayer >= 0 && go.layer != wiLayer)
                {
                    go.layer = wiLayer;
                    changed = true;
                }

                if (changed)
                {
                    fixedCount++;
                    EditorUtility.SetDirty(go);
                    EditorUtility.SetDirty(interactable);
                }
            }

            Debug.Log($"[Fishing][Validate] Found FishingSpot components: {seen}. Changed: {fixedCount}.");

            // Mark scene dirty so user can save.
            try
            {
                var active = SceneManager.GetActiveScene();
                if (active.IsValid())
                    EditorSceneManager.MarkSceneDirty(active);
            }
            catch { }
        }

        private static string FindScenePathByName(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return null;

            var guids = AssetDatabase.FindAssets($"t:Scene {sceneName}");
            if (guids == null || guids.Length == 0)
                return null;

            for (int i = 0; i < guids.Length; i++)
            {
                var p = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrWhiteSpace(p)) continue;

                var name = System.IO.Path.GetFileNameWithoutExtension(p);
                if (string.Equals(name, sceneName, StringComparison.OrdinalIgnoreCase))
                    return p;
            }

            return null;
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
