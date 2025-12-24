#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abyssbound.EditorTools
{
    public static class EnsureFoundationInScene
    {
        private const string DefaultBootstrapperName = "_GameBootstrapper";
        private const string DefaultPlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";

            [MenuItem("Tools/Abyssbound/Dev/Ensure Bootstrapper In Scene")]
        public static void EnsureBootstrapper()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("Run this in Edit Mode (not Play Mode).");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[Foundation] No active scene.");
                return;
            }

            var existing = GameObject.Find(DefaultBootstrapperName);
            if (existing == null)
            {
                existing = new GameObject(DefaultBootstrapperName);
                Undo.RegisterCreatedObjectUndo(existing, "Create GameBootstrapper");
            }

            var bootstrapper = existing.GetComponent<GameBootstrapper>();
            if (bootstrapper == null)
                bootstrapper = Undo.AddComponent<GameBootstrapper>(existing);

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultPlayerPrefabPath);
            if (playerPrefab != null)
            {
                var so = new SerializedObject(bootstrapper);
                var prop = so.FindProperty("playerPrefab");
                if (prop != null)
                {
                    prop.objectReferenceValue = playerPrefab;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            else
            {
                Debug.LogWarning($"[Foundation] Could not load Player prefab at '{DefaultPlayerPrefabPath}'. Bootstrapper will only bind to an existing scene Player.");
            }

            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = existing;
            EditorGUIUtility.PingObject(existing);
        }
    }
}
#endif
