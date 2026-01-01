#if UNITY_EDITOR
using System.IO;
using Abyssbound.Combat.Tiering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abyssbound.EditorTools.Combat.Tiering
{
    /// <summary>
    /// Editor menu helpers to set up distance tiering without touching any UI.
    /// Creates the config asset and a scene DistanceTierService GameObject.
    /// </summary>
    public static class TieringSetupMenu
    {
        private const string DefaultAssetPath = "Assets/Resources/Combat/Tiering/EnemyTierConfig.asset";

        /// <summary>
        /// Creates a default EnemyTierConfigSO asset (if missing) at a known location.
        /// </summary>
        [MenuItem("Abyssbound/Combat/Tiering/Create Default Tier Config Asset")]
        public static void CreateDefaultTierConfigAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<EnemyTierConfigSO>(DefaultAssetPath);
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                Debug.Log($"[TieringSetup] Config already exists at {DefaultAssetPath}.");
                return;
            }

            string dir = Path.GetDirectoryName(DefaultAssetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var asset = ScriptableObject.CreateInstance<EnemyTierConfigSO>();
            AssetDatabase.CreateAsset(asset, DefaultAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            Debug.Log($"[TieringSetup] Created config at {DefaultAssetPath}.");
        }

        /// <summary>
        /// Creates (or selects) a DistanceTierService in the active scene and wires the config if available.
        /// Attempts to assign TownOrigin by finding a GameObject named 'TownOrigin'.
        /// </summary>
        [MenuItem("Abyssbound/Combat/Tiering/Create/Ensure DistanceTierService In Scene")]
        public static void CreateOrEnsureServiceInScene()
        {
            var service = Object.FindFirstObjectByType<DistanceTierService>(FindObjectsInactive.Include);
            if (service == null)
            {
                var go = new GameObject("DistanceTierService");
                service = go.AddComponent<DistanceTierService>();
                Undo.RegisterCreatedObjectUndo(go, "Create DistanceTierService");
                Debug.Log("[TieringSetup] Created DistanceTierService GameObject in scene.");
            }
            else
            {
                Debug.Log("[TieringSetup] DistanceTierService already present in scene.");
            }

            var config = AssetDatabase.LoadAssetAtPath<EnemyTierConfigSO>(DefaultAssetPath);
            if (config == null)
            {
                Debug.LogWarning($"[TieringSetup] No config found at {DefaultAssetPath}. Use 'Create Default Tier Config Asset' first.");
            }

            var townOriginTransform = EnsureTownOriginTransform();

            // Use SerializedObject so we can set private serialized fields safely without changing runtime code.
            var so = new SerializedObject(service);
            if (config != null)
                so.FindProperty("config").objectReferenceValue = config;
            if (townOriginTransform != null)
                so.FindProperty("townOrigin").objectReferenceValue = townOriginTransform;
            so.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeObject = service.gameObject;
            EditorGUIUtility.PingObject(service.gameObject);

            EditorSceneManager.MarkSceneDirty(service.gameObject.scene);
        }

        private static Transform EnsureTownOriginTransform()
        {
            var existing = GameObject.Find("TownOrigin");
            if (existing != null)
                return existing.transform;

            var go = new GameObject("TownOrigin");
            go.transform.position = Vector3.zero;
            Undo.RegisterCreatedObjectUndo(go, "Create TownOrigin");
            Debug.Log("[TieringSetup] Created 'TownOrigin' at world origin (0,0,0). Move it to your desired town center.");
            return go.transform;
        }
    }
}
#endif
