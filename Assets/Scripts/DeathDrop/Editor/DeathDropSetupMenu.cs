#if UNITY_EDITOR
using System;
using Abyss.Waypoints;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abyssbound.DeathDrop.Editor
{
    public static class DeathDropSetupMenu
    {
        private const string ScriptsFolder = "Assets/Scripts/DeathDrop";
        private const string ScriptsEditorFolder = "Assets/Scripts/DeathDrop/Editor";
        private const string PrefabsFolder = "Assets/Prefabs/DeathDrop";

        private const string PickupPrefabPath = "Assets/Prefabs/DeathDrop/DeathPilePickup.prefab";

        [MenuItem("Tools/DeathDrop/Setup Death Drop v1 (One-Click)")]
        public static void Setup()
        {
            EnsureFolder(ScriptsFolder);
            EnsureFolder(ScriptsEditorFolder);
            EnsureFolder("Assets/Prefabs");
            EnsureFolder(PrefabsFolder);

            var pickupPrefab = EnsurePickupPrefab();

            var mgr = EnsureManagerInScene(pickupPrefab);
            var watcherAdded = EnsureWatcherOnPlayer();
            var townSpawnCreated = EnsureTownSpawn();

            MarkActiveSceneDirty();

            Debug.Log($"[DeathDrop] Setup complete. Manager={(mgr != null ? "ok" : "missing")} WatcherAdded={(watcherAdded ? "yes" : "no")} TownSpawnCreated={(townSpawnCreated ? "yes" : "no")}");
        }

        private static GameObject EnsurePickupPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PickupPrefabPath);
            if (existing != null)
                return existing;

            GameObject temp = null;
            try
            {
                temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                temp.name = "DeathPilePickup";

                // Collider should be trigger.
                var col = temp.GetComponent<Collider>();
                if (col != null) col.isTrigger = true;

                // Kinematic RB helps trigger reliability.
                var rb = temp.GetComponent<Rigidbody>();
                if (rb == null) rb = temp.AddComponent<Rigidbody>();
                rb.useGravity = false;
                rb.isKinematic = true;

                // Ensure our pickup script exists on prefab.
                if (temp.GetComponent<DeathPilePickup>() == null)
                    temp.AddComponent<DeathPilePickup>();

                var saved = PrefabUtility.SaveAsPrefabAsset(temp, PickupPrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                return saved;
            }
            finally
            {
                if (temp != null)
                    UnityEngine.Object.DestroyImmediate(temp);
            }
        }

        private static DeathDropManager EnsureManagerInScene(GameObject pickupPrefab)
        {
            DeathDropManager mgr = null;
            try
            {
#if UNITY_2022_2_OR_NEWER
                mgr = UnityEngine.Object.FindFirstObjectByType<DeathDropManager>(FindObjectsInactive.Include);
#else
                mgr = UnityEngine.Object.FindObjectOfType<DeathDropManager>();
#endif
            }
            catch { mgr = null; }

            if (mgr == null)
            {
                var go = new GameObject("[DeathDropManager]");
                Undo.RegisterCreatedObjectUndo(go, "Create DeathDropManager");
                mgr = Undo.AddComponent<DeathDropManager>(go);
            }

            if (mgr != null && pickupPrefab != null)
            {
                try
                {
                    var so = new SerializedObject(mgr);
                    var prop = so.FindProperty("pickupPrefab");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = pickupPrefab;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(mgr);
                    }
                }
                catch { }
            }

            return mgr;
        }

        private static bool EnsureWatcherOnPlayer()
        {
            GameObject player = null;

            try { player = GameObject.Find("Player_Hero"); } catch { player = null; }

            if (player == null)
            {
                try { player = GameObject.FindGameObjectWithTag("Player"); } catch { player = null; }
            }

            if (player == null)
                return false;

            if (player.GetComponent<PlayerDeathWatcher>() != null)
                return false;

            Undo.AddComponent<PlayerDeathWatcher>(player);
            EditorUtility.SetDirty(player);
            return true;
        }

        private static bool EnsureTownSpawn()
        {
            // If a TownSpawn already exists (tagged or named), do nothing.
            try
            {
                try
                {
                    var tagged = GameObject.FindGameObjectWithTag("TownSpawn");
                    if (tagged != null) return false;
                }
                catch { }

                var named = GameObject.Find("TownSpawn");
                if (named != null) return false;
            }
            catch { }

            var go = new GameObject("TownSpawn");
            Undo.RegisterCreatedObjectUndo(go, "Create TownSpawn");

            // Attempt to position near an existing town waypoint.
            try
            {
#if UNITY_2022_2_OR_NEWER
                var wps = UnityEngine.Object.FindObjectsByType<WaypointComponent>(FindObjectsSortMode.None);
#else
                var wps = UnityEngine.Object.FindObjectsOfType<WaypointComponent>();
#endif
                if (wps != null)
                {
                    for (int i = 0; i < wps.Length; i++)
                    {
                        var wp = wps[i];
                        if (wp == null) continue;
                        if (!wp.IsTown) continue;

                        var sp = wp.GetSpawnPoint();
                        go.transform.position = sp != null ? sp.position : wp.transform.position;
                        break;
                    }
                }
            }
            catch
            {
                go.transform.position = Vector3.zero;
            }

            // Tag only if it exists (otherwise Unity throws: "Tag ... is not defined").
            try
            {
                bool hasTag = false;
                try
                {
                    var tags = UnityEditorInternal.InternalEditorUtility.tags;
                    if (tags != null)
                    {
                        for (int i = 0; i < tags.Length; i++)
                        {
                            if (string.Equals(tags[i], "TownSpawn", StringComparison.Ordinal))
                            {
                                hasTag = true;
                                break;
                            }
                        }
                    }
                }
                catch { hasTag = false; }

                if (hasTag)
                {
                    go.tag = "TownSpawn";
                }
                else
                {
                    // Not an error; runtime respawn also supports name-based lookup.
                    Debug.LogWarning("[DeathDrop] Tag 'TownSpawn' not defined. Created object named 'TownSpawn' (name-based fallback will be used).", go);
                }
            }
            catch { }

            EditorUtility.SetDirty(go);
            return true;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = System.IO.Path.GetFileName(path);

            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
                return;

            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }

        private static void MarkActiveSceneDirty()
        {
            try
            {
                var scene = SceneManager.GetActiveScene();
                if (scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                }
            }
            catch { }
        }
    }
}
#endif
