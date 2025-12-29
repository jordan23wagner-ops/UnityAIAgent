#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Abyss.Waypoints.Editor
{
    public static class WaypointQuickToolsMenu
    {
        private const string Prefab3DPath = "Assets/Prefabs/Waypoints/WP_Waypoint3D.prefab";
        private const string Prefab2DPath = "Assets/Prefabs/Waypoints/WP_Waypoint2D.prefab";

        [MenuItem("Tools/Waypoints/Create Waypoint (3D)")]
        public static void CreateWaypoint3D()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab3DPath);
            if (prefab == null)
            {
                Debug.LogWarning("[Waypoints] Missing prefab. Run Tools/Waypoints/Setup Waypoints System (One-Click) first.");
                return;
            }

            var go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (go == null)
                return;

            go.name = "WP_Waypoint3D";
            EnsureVisuals(go);
            PlaceInScene(go);
        }

        [MenuItem("Tools/Waypoints/Create Waypoint (2D)")]
        public static void CreateWaypoint2D()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab2DPath);
            if (prefab == null)
            {
                Debug.LogWarning("[Waypoints] Missing prefab. Run Tools/Waypoints/Setup Waypoints System (One-Click) first.");
                return;
            }

            var go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (go == null)
                return;

            go.name = "WP_Waypoint2D";
            EnsureVisuals(go);
            PlaceInScene(go);
        }

        private static void EnsureVisuals(GameObject go)
        {
            if (go == null)
                return;

            try
            {
                var wp = go.GetComponent<WaypointComponent>();
                if (wp == null)
                    return;

                Undo.RegisterFullObjectHierarchyUndo(go, "Refresh Waypoint Visuals");
                WaypointVisualBuilder.EnsureVisual(wp);
            }
            catch { }
        }

        private static void PlaceInScene(GameObject go)
        {
            if (go == null)
                return;

            Undo.RegisterCreatedObjectUndo(go, "Create Waypoint");
            Selection.activeGameObject = go;
            try
            {
                if (SceneView.lastActiveSceneView != null)
                {
                    var cam = SceneView.lastActiveSceneView.camera;
                    if (cam != null)
                    {
                        go.transform.position = cam.transform.position + cam.transform.forward * 5f;
                    }
                }
            }
            catch { }
        }
    }
}
#endif
