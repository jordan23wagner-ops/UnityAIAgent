#if UNITY_EDITOR
using Abyssbound.Skills.Fishing;
using Abyssbound.WorldInteraction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abyssbound.EditorTools.Fishing
{
    public static class FixFishingSpotCollidersSceneMenu
    {
        [MenuItem("Tools/Abyssbound/Fishing/Fix Fishing Spot Colliders (Scene)")]
        public static void FixSceneFishingSpotColliders()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning("[FishingCollider] No active loaded scene.");
                return;
            }

            int changed = 0;

            changed += FixForType<FishingSpotInteractable>(preferChildCollider: true);
            changed += FixForType<FishingSpot>(preferChildCollider: true);

            if (changed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log($"[FishingCollider] Fixed {changed} fishing collider object(s) in scene '{scene.name}'.");
            }
            else
            {
                Debug.Log($"[FishingCollider] No fishing collider changes needed in scene '{scene.name}'.");
            }
        }

        private static int FixForType<T>(bool preferChildCollider) where T : Component
        {
            int changed = 0;

            T[] all;
            try
            {
#if UNITY_2022_2_OR_NEWER
                all = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
                all = Object.FindObjectsOfType<T>(true);
#endif
            }
            catch
            {
                return 0;
            }

            if (all == null || all.Length == 0)
                return 0;

            foreach (var comp in all)
            {
                if (comp == null) continue;
                if (FixOne(comp.gameObject, preferChildCollider))
                    changed++;
            }

            return changed;
        }

        private static bool FixOne(GameObject root, bool preferChildCollider)
        {
            if (root == null) return false;

            var target = FindBestColliderOwner(root, preferChildCollider);
            if (target == null) target = root;

            bool dirty = false;

            // Ensure enforcer is on the collider-owner object.
            var enforcer = target.GetComponent<FishingSpotColliderEnforcer>();
            if (enforcer == null)
            {
                Undo.AddComponent<FishingSpotColliderEnforcer>(target);
                dirty = true;
            }

            // Ensure SphereCollider exists and is enabled trigger.
            var sphere = target.GetComponent<SphereCollider>();
            if (sphere == null)
            {
                sphere = Undo.AddComponent<SphereCollider>(target);
                dirty = true;
            }

            if (sphere != null)
            {
                if (!sphere.enabled) { Undo.RecordObject(sphere, "Enable SphereCollider"); sphere.enabled = true; dirty = true; }
                if (!sphere.isTrigger) { Undo.RecordObject(sphere, "Set SphereCollider Trigger"); sphere.isTrigger = true; dirty = true; }
            }

            // Disable any BoxCollider on the collider owner.
            var box = target.GetComponent<BoxCollider>();
            if (box != null && box.enabled)
            {
                Undo.RecordObject(box, "Disable BoxCollider");
                box.enabled = false;
                dirty = true;
            }

            // If root has a sphere but interaction collider is on a child, disable the root sphere to avoid the parent-sphere bug.
            if (target != root)
            {
                var rootSphere = root.GetComponent<SphereCollider>();
                if (rootSphere != null && rootSphere.enabled)
                {
                    Undo.RecordObject(rootSphere, "Disable Root SphereCollider");
                    rootSphere.enabled = false;
                    dirty = true;
                }
            }

            if (dirty)
                EditorUtility.SetDirty(root);
            if (target != root)
                EditorUtility.SetDirty(target);

            return dirty;
        }

        private static GameObject FindBestColliderOwner(GameObject root, bool preferChildCollider)
        {
            if (root == null) return null;

            SphereCollider bestSphere = null;
            try
            {
                var spheres = root.GetComponentsInChildren<SphereCollider>(true);
                if (spheres != null && spheres.Length > 0)
                {
                    // Prefer an enabled trigger sphere.
                    foreach (var s in spheres)
                    {
                        if (s == null) continue;
                        if (!s.enabled) continue;
                        if (!s.isTrigger) continue;
                        if (preferChildCollider && s.gameObject == root) continue;
                        bestSphere = s;
                        break;
                    }

                    // Otherwise prefer any child sphere.
                    if (bestSphere == null)
                    {
                        foreach (var s in spheres)
                        {
                            if (s == null) continue;
                            if (preferChildCollider && s.gameObject == root) continue;
                            bestSphere = s;
                            break;
                        }
                    }

                    // Otherwise any sphere.
                    if (bestSphere == null)
                        bestSphere = spheres[0];
                }
            }
            catch { bestSphere = null; }

            if (bestSphere != null)
                return bestSphere.gameObject;

            // If there's only a box trigger, use it as the anchor (we'll add a sphere there and disable the box).
            try
            {
                var boxes = root.GetComponentsInChildren<BoxCollider>(true);
                if (boxes != null)
                {
                    foreach (var b in boxes)
                    {
                        if (b == null) continue;
                        if (!b.enabled) continue;
                        if (!b.isTrigger) continue;
                        return b.gameObject;
                    }
                }
            }
            catch { }

            return root;
        }
    }
}
#endif
