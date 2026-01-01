#if UNITY_EDITOR
using Abyssbound.Progression;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abyssbound.Progression.Editor
{
    public static class PlayerProgressionTools
    {
        private const string DefaultSlotId = "default";

        [MenuItem("Tools/Progression/Print Player Progression (Debug)")]
        public static void Print()
        {
            var runtime = FindRuntimeInstance();
            string slotId = DefaultSlotId;
            int runtimeMax = -1;

            if (runtime != null)
            {
                slotId = TryGetSlotId(runtime) ?? DefaultSlotId;
                runtimeMax = runtime.MaxInventorySlots;
            }

            string maxKey = PlayerProgression.GetMaxInventorySlotsPrefsKey(slotId);
            string maskKey = PlayerProgression.GetBagUpgradeMaskPrefsKey(slotId);

            bool hasMaxKey = false;
            int persistedMax = PlayerProgression.DefaultMaxInventorySlots;
            bool hasMaskKey = false;
            int persistedMask = 0;
            try
            {
                hasMaxKey = PlayerPrefs.HasKey(maxKey);
                persistedMax = PlayerPrefs.GetInt(maxKey, PlayerProgression.DefaultMaxInventorySlots);
                hasMaskKey = PlayerPrefs.HasKey(maskKey);
                persistedMask = PlayerPrefs.GetInt(maskKey, 0);
            }
            catch
            {
                hasMaxKey = false;
                persistedMax = PlayerProgression.DefaultMaxInventorySlots;
                hasMaskKey = false;
                persistedMask = 0;
            }

            Debug.Log("[Progression] PlayerProgression (Debug)");
            Debug.Log($"[Progression] Runtime instance: {(runtime != null ? "FOUND" : "NONE")}");
            Debug.Log($"[Progression] SlotId: '{slotId}'");
            Debug.Log($"[Progression] MaxSlots PrefsKey: {maxKey}");
            Debug.Log($"[Progression] MaxSlots Persisted: {(hasMaxKey ? persistedMax.ToString() : "<missing>")}");
            Debug.Log($"[Progression] BagUpgradeMask PrefsKey: {maskKey}");
            Debug.Log($"[Progression] BagUpgradeMask Persisted: {(hasMaskKey ? persistedMask.ToString() : "<missing>")}");
            if (runtime != null)
                Debug.Log($"[Progression] Runtime MaxInventorySlots: {runtimeMax}");
            if (runtime != null)
                Debug.Log($"[Progression] Runtime AppliedBagUpgradeMask: {runtime.AppliedBagUpgradeMask}");
        }

        [MenuItem("Tools/Progression/Reset Player Progression (Bag/Inventory)")]
        public static void ResetBagInventory()
        {
            bool ok = EditorUtility.DisplayDialog(
                title: "Reset Player Progression",
                message: "This will reset bag/inventory progression to defaults (MaxInventorySlots=10) by deleting only the PlayerProgression PlayerPrefs keys. Continue?",
                ok: "Reset",
                cancel: "Cancel");

            if (!ok)
                return;

            var runtime = FindRuntimeInstance();
            string slotId = runtime != null ? (TryGetSlotId(runtime) ?? DefaultSlotId) : DefaultSlotId;
            string maxKey = PlayerProgression.GetMaxInventorySlotsPrefsKey(slotId);
            string maskKey = PlayerProgression.GetBagUpgradeMaskPrefsKey(slotId);

            try
            {
                if (PlayerPrefs.HasKey(maxKey))
                    PlayerPrefs.DeleteKey(maxKey);
                if (PlayerPrefs.HasKey(maskKey))
                    PlayerPrefs.DeleteKey(maskKey);
                PlayerPrefs.Save();
            }
            catch
            {
                // Ignore: reset is best-effort.
            }

            if (runtime != null)
            {
                // Do not refactor gameplay logic: use existing APIs.
                runtime.SetSaveSlotId(slotId);
                runtime.Load(); // will pull default after key deletion
                runtime.Save(); // persists default

                // If called while editing a scene, mark dirty because we modified a scene object.
                try
                {
                    if (!Application.isPlaying)
                    {
                        EditorUtility.SetDirty(runtime);
                        EditorSceneManager.MarkSceneDirty(runtime.gameObject.scene);
                    }
                }
                catch { }
            }

            Debug.Log($"[Progression] Reset complete. MaxInventorySlots={PlayerProgression.DefaultMaxInventorySlots}");
        }

        private static PlayerProgression FindRuntimeInstance()
        {
            // In edit-mode, this will find a scene instance if present.
            // In play-mode, it will also find the DontDestroyOnLoad instance.
            try
            {
#if UNITY_2022_2_OR_NEWER
                var inst = Object.FindFirstObjectByType<PlayerProgression>(FindObjectsInactive.Include);
                if (inst != null) return inst;
#else
                var inst = Object.FindObjectOfType<PlayerProgression>();
                if (inst != null) return inst;
#endif
            }
            catch { }

            // Fallback: catches some editor edge cases.
            try
            {
                var all = Resources.FindObjectsOfTypeAll<PlayerProgression>();
                if (all != null && all.Length > 0)
                    return all[0];
            }
            catch { }

            return null;
        }

        private static string TryGetSlotId(PlayerProgression prog)
        {
            if (prog == null) return null;

            // saveSlotId is serialized private; read via reflection to avoid gameplay refactor.
            try
            {
                var fi = typeof(PlayerProgression).GetField("saveSlotId", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (fi == null) return null;
                return fi.GetValue(prog) as string;
            }
            catch
            {
                return null;
            }
        }
    }
}
#endif
