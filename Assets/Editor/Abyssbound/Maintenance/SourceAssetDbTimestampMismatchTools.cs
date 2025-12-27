#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Abyssbound.EditorTools
{
    public static class SourceAssetDbTimestampMismatchTools
    {
        private const string FixOneMenu = "Tools/Abyssbound/Maintenance/Fix SourceAssetDB Timestamp Mismatch";
        private const string FixAllSafeMenu = "Tools/Abyssbound/Maintenance/Fix All SourceAssetDB Timestamp Mismatch (Safe)";

        // Keep this list intentionally small + explicit to avoid long reimports.
        private static readonly string[] SafeAssetPaths =
        {
            "Assets/GameData/Loot/StarterSet/Item_Starter_Legs.asset",
        };

        [MenuItem(FixOneMenu)]
        private static void FixOne()
        {
            FixAsset(SafeAssetPaths[0]);
        }

        [MenuItem(FixAllSafeMenu)]
        private static void FixAllSafe()
        {
            int ok = 0;
            int fail = 0;

            foreach (var assetPath in SafeAssetPaths)
            {
                if (FixAsset(assetPath)) ok++;
                else fail++;
            }

            if (fail == 0)
                Debug.Log($"[SourceAssetDB] Safe fix complete. ok={ok} fail={fail}");
            else
                Debug.LogError($"[SourceAssetDB] Safe fix complete. ok={ok} fail={fail}");
        }

        private static bool FixAsset(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                Debug.LogError("[SourceAssetDB] Invalid asset path (empty)." );
                return false;
            }

            try
            {
                var obj = AssetDatabase.LoadMainAssetAtPath(assetPath);
                if (obj == null)
                {
                    Debug.LogError($"[SourceAssetDB] Asset not found at path: {assetPath}");
                    return false;
                }

                // 1) Force a refresh + reimport of the specific asset.
                AssetDatabase.Refresh();
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

                // 2) Reserialize + reimport (forces on-disk rewrite and import pipeline alignment).
                AssetDatabase.ForceReserializeAssets(new List<string> { assetPath });
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

                // 3) Save + refresh.
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"[SourceAssetDB] Fixed timestamp mismatch (reserialize+reimport): {assetPath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SourceAssetDB] Fix failed for '{assetPath}'. {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }
    }
}
#endif
