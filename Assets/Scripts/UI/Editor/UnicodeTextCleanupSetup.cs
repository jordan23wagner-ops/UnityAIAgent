#if UNITY_EDITOR
using Abyssbound.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abyssbound.EditorTools.UI
{
    public static class UnicodeTextCleanupSetup
    {
        [MenuItem("Tools/UI/Run Unicode TMP Cleanup Once (Setup)")]
        public static void Setup()
        {
            var (root, createdRoot) = FindOrCreateRoot();
            if (root == null)
            {
                Debug.LogWarning("[UnicodeTextCleanupSetup] Failed to find or create a root GameObject.");
                return;
            }

            var existing = root.GetComponent<UnicodeTextCleanup>();
            bool added = false;
            if (existing == null)
            {
                root.AddComponent<UnicodeTextCleanup>();
                added = true;
            }

            try
            {
                EditorSceneManager.MarkSceneDirty(root.scene);
            }
            catch { }

            var action = added ? "Added" : "Already present";
            var createdNote = createdRoot ? " (created root)" : string.Empty;
            Debug.Log($"[UnicodeTextCleanupSetup] {action} UnicodeTextCleanup on '{root.name}'.{createdNote}");
        }

        private static (GameObject root, bool created) FindOrCreateRoot()
        {
            // Preference order per request / project conventions.
            var bootstrapper = FindSceneObjectByName("_GameBootstrapper");
            if (bootstrapper != null)
                return (bootstrapper, false);

            var systems = FindSceneObjectByName("[SYSTEMS]");
            if (systems != null)
                return (systems, false);

            var ddol = FindSceneObjectByName("DontDestroyOnLoad");
            if (ddol != null)
                return (ddol, false);

            // If none exist, create the minimal root.
            var created = new GameObject("_GameBootstrapper");
            return (created, true);
        }

        private static GameObject FindSceneObjectByName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            // GameObject.Find won't find inactive objects reliably; use Resources.FindObjectsOfTypeAll.
            GameObject[] all;
            try { all = Resources.FindObjectsOfTypeAll<GameObject>(); }
            catch { return null; }

            if (all == null)
                return null;

            for (int i = 0; i < all.Length; i++)
            {
                var go = all[i];
                if (go == null)
                    continue;

                try
                {
                    if (!go.scene.IsValid())
                        continue;
                }
                catch
                {
                    continue;
                }

                if (!string.Equals(go.name, name))
                    continue;

                return go;
            }

            return null;
        }
    }
}
#endif
