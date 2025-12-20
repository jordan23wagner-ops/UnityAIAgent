#if UNITY_EDITOR
using Abyss.Dev;
using UnityEditor;
using UnityEngine;

namespace Abyss.Dev.Editor
{
    public static class DevCheatsSetup
    {
        [MenuItem("Tools/Abyss/Dev/Create DevCheats In Scene")]
        private static void CreateDevCheatsInScene()
        {
            var existing = Object.FindFirstObjectByType<DevCheats>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Selection.activeObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
                Debug.Log("[DevCheatsSetup] DevCheats already exists in scene.");
                return;
            }

            var go = new GameObject("DevCheats");
            Undo.RegisterCreatedObjectUndo(go, "Create DevCheats");
            var cheats = go.AddComponent<DevCheats>();

            // Auto-assign the dummy enemy prefab if present.
            var dummy = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy_Dummy/Enemy_Dummy.prefab");
            if (dummy != null)
                cheats.enemyPrefabs.Add(dummy);

            Selection.activeObject = go;
            EditorGUIUtility.PingObject(go);
            Debug.Log("[DevCheatsSetup] Created DevCheats. Hotkeys: F1 GodMode, F2 Spawn, F3 KillSpawned.");
        }
    }
}
#endif
