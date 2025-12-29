#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abyss.Waypoints.Editor
{
    /*
     * Waypoints v1 one-click setup
     *
     * - Run: Tools/Waypoints/Setup Waypoints System (One-Click) once per project.
     * - Then create waypoints via Tools/Waypoints/Create Waypoint (3D/2D) or drag the prefabs.
     * - Press Play, walk into a waypoint to activate, press F6 to teleport.
     */

    public static class WaypointSetupMenu
    {
        private const string RegistryAssetPath = "Assets/GameData/Waypoints/WaypointRegistry.asset";
        private const string Prefab3DPath = "Assets/Prefabs/Waypoints/WP_Waypoint3D.prefab";
        private const string Prefab2DPath = "Assets/Prefabs/Waypoints/WP_Waypoint2D.prefab";

        [MenuItem("Tools/Waypoints/Setup Waypoints System (One-Click)")]
        public static void Setup()
        {
            EnsureFolders();

            var registry = CreateOrLoadRegistry();
            CreateOrLoadPrefab3D(registry);
            CreateOrLoadPrefab2D(registry);

            EnsureManagerInOpenScene(registry);
            EnsureTownWaypointInScene(registry);

            // Ensure visuals exist for any waypoint instances in the currently open scene.
            try
            {
                var waypoints = UnityEngine.Object.FindObjectsByType<WaypointComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var wp in waypoints)
                    WaypointVisualBuilder.EnsureVisual(wp);
            }
            catch { }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Waypoints] Setup complete. You can now create/place waypoint prefabs and press Play.");
        }

        private static void EnsureFolders()
        {
            EnsureFolderExists("Assets/GameData");
            EnsureFolderExists("Assets/GameData/Waypoints");
            EnsureFolderExists("Assets/Prefabs");
            EnsureFolderExists("Assets/Prefabs/Waypoints");
        }

        private static void EnsureFolderExists(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
                return;

            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolderExists(parent);

            AssetDatabase.CreateFolder(parent, name);
        }

        private static WaypointRegistrySO CreateOrLoadRegistry()
        {
            var existing = AssetDatabase.LoadAssetAtPath<WaypointRegistrySO>(RegistryAssetPath);
            if (existing != null)
                return existing;

            var asset = ScriptableObject.CreateInstance<WaypointRegistrySO>();
            AssetDatabase.CreateAsset(asset, RegistryAssetPath);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static GameObject CreateOrLoadPrefab3D(WaypointRegistrySO registry)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab3DPath);
            if (existing != null)
            {
                EnsureVisualsOnPrefabAsset(Prefab3DPath);
                return existing;
            }

            var go = new GameObject("WP_Waypoint3D");
            try
            {
                var wp = go.AddComponent<WaypointComponent>();
                AssignRegistry(wp, registry);
                SetDisplayNameDefault(wp, "Waypoint");

                go.AddComponent<WaypointTrigger3D>();

                var col = go.GetComponent<Collider>();
                if (col == null)
                    col = go.AddComponent<BoxCollider>();
                col.isTrigger = true;

                var spawn = new GameObject("SpawnPoint");
                spawn.transform.SetParent(go.transform, false);
                spawn.transform.localPosition = Vector3.zero;
                spawn.transform.localRotation = Quaternion.identity;
                spawn.transform.localScale = Vector3.one;

                WaypointVisualBuilder.EnsureVisual(wp);

                var prefab = PrefabUtility.SaveAsPrefabAsset(go, Prefab3DPath);
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static GameObject CreateOrLoadPrefab2D(WaypointRegistrySO registry)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab2DPath);
            if (existing != null)
            {
                EnsureVisualsOnPrefabAsset(Prefab2DPath);
                return existing;
            }

            var go = new GameObject("WP_Waypoint2D");
            try
            {
                var wp = go.AddComponent<WaypointComponent>();
                AssignRegistry(wp, registry);
                SetDisplayNameDefault(wp, "Waypoint");

                go.AddComponent<WaypointTrigger2D>();

                var col = go.GetComponent<Collider2D>();
                if (col == null)
                    col = go.AddComponent<BoxCollider2D>();
                col.isTrigger = true;

                var spawn = new GameObject("SpawnPoint");
                spawn.transform.SetParent(go.transform, false);
                spawn.transform.localPosition = Vector3.zero;
                spawn.transform.localRotation = Quaternion.identity;
                spawn.transform.localScale = Vector3.one;

                WaypointVisualBuilder.EnsureVisual(wp);

                var prefab = PrefabUtility.SaveAsPrefabAsset(go, Prefab2DPath);
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void EnsureManagerInOpenScene(WaypointRegistrySO registry)
        {
            var existing = UnityEngine.Object.FindFirstObjectByType<WaypointManager>();
            if (existing == null)
            {
                var go = new GameObject("[WaypointManager]");
                existing = go.AddComponent<WaypointManager>();
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }

            AssignRegistry(existing, registry);
        }

        private static void EnsureTownWaypointInScene(WaypointRegistrySO registry)
        {
            // Only create if none exists.
            var existingWaypoints = UnityEngine.Object.FindObjectsByType<WaypointComponent>(FindObjectsSortMode.None);
            if (existingWaypoints != null)
            {
                for (int i = 0; i < existingWaypoints.Length; i++)
                {
                    var wp = existingWaypoints[i];
                    if (wp != null && wp.IsTown)
                        return;
                }
            }

            var go = new GameObject("WP_Town");
            go.transform.position = Vector3.zero;

            var town = go.AddComponent<WaypointComponent>();
            AssignRegistry(town, registry);
            SetDisplayNameDefault(town, "Town");
            SetIsTown(town, true);

            town.VisualStyle = WaypointComponent.WaypointVisualStyle.PlatformWithPillars;
            WaypointVisualBuilder.EnsureVisual(town);

            go.AddComponent<WaypointTrigger3D>();
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;

            var spawn = new GameObject("SpawnPoint");
            spawn.transform.SetParent(go.transform, false);
            spawn.transform.localPosition = Vector3.zero;
            spawn.transform.localRotation = Quaternion.identity;
            spawn.transform.localScale = Vector3.one;

            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        private static void EnsureVisualsOnPrefabAsset(string prefabPath)
        {
            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(prefabPath);
                if (root == null)
                    return;

                var wp = root.GetComponent<WaypointComponent>();
                if (wp == null)
                    return;

                WaypointVisualBuilder.EnsureVisual(wp);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            catch { }
            finally
            {
                if (root != null)
                    PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AssignRegistry(UnityEngine.Object target, WaypointRegistrySO registry)
        {
            if (target == null)
                return;

            try
            {
                var so = new SerializedObject(target);
                var prop = so.FindProperty("registry");
                if (prop != null)
                {
                    prop.objectReferenceValue = registry;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            catch { }
        }

        private static void SetDisplayNameDefault(WaypointComponent wp, string displayName)
        {
            if (wp == null)
                return;

            try
            {
                var so = new SerializedObject(wp);
                var prop = so.FindProperty("displayName");
                if (prop != null && string.IsNullOrWhiteSpace(prop.stringValue))
                {
                    prop.stringValue = displayName;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            catch { }
        }

        private static void SetIsTown(WaypointComponent wp, bool isTown)
        {
            if (wp == null)
                return;

            try
            {
                var so = new SerializedObject(wp);
                var prop = so.FindProperty("isTown");
                if (prop != null)
                {
                    prop.boolValue = isTown;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            catch { }
        }
    }
}
#endif
