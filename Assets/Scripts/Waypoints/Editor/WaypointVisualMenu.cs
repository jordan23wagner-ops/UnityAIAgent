#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abyss.Waypoints.Editor
{
    public static class WaypointVisualMenu
    {
        [MenuItem("Tools/Waypoints/Refresh Waypoint Visuals")]
        public static void RefreshVisuals()
        {
            var waypoints = Object.FindObjectsByType<WaypointComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int changed = 0;

            foreach (var wp in waypoints)
            {
                if (wp == null)
                    continue;

                WaypointVisualBuilder.EnsureVisual(wp);
                EditorUtility.SetDirty(wp.gameObject);
                changed++;
            }

            if (changed > 0)
                EditorSceneManager.MarkAllScenesDirty();

            Debug.Log($"[Waypoints] Refreshed visuals for {changed} waypoint(s).", null);
        }
    }
}
#endif
