#if UNITY_EDITOR
using Abyssbound.Progression;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abyssbound.EditorTools.Progression
{
    public static class PlayerProgressionSetupEditor
    {
        private const string MenuPath = "Tools/Progression/Setup Player Progression (One-Click)";

        [MenuItem(MenuPath)]
        public static void Setup()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[PlayerProgression] Run this in Edit Mode (not Play Mode)." );
                return;
            }

            var existing = Object.FindFirstObjectByType<PlayerProgression>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Selection.activeObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
                return;
            }

            var go = new GameObject("[PlayerProgression]");
            Undo.RegisterCreatedObjectUndo(go, "Create PlayerProgression");
            go.AddComponent<PlayerProgression>();

            Selection.activeObject = go;
            EditorGUIUtility.PingObject(go);

            EditorSceneManager.MarkSceneDirty(go.scene);
        }
    }
}
#endif
