#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Abyssbound.EditorTools
{
    public static class MigrateFodderToTrash
    {
        [MenuItem("Tools/Abyssbound/Maintenance/Migrate Fodder -> Trash (Summary)")]
        public static void Run()
        {
            int assetsRenamed = 0;
            var renamed = new List<string>();

            // Rename asset filenames that contain "Fodder" (non-destructive; references preserved).
            var all = AssetDatabase.FindAssets(string.Empty, new[] { "Assets" });
            foreach (var guid in all)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrWhiteSpace(path)) continue;

                var file = System.IO.Path.GetFileNameWithoutExtension(path);
                if (file == null) continue;

                if (file.IndexOf("Fodder", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var newName = ReplaceOrdinalIgnoreCase(file, "Fodder", "Trash");
                if (string.Equals(file, newName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var err = AssetDatabase.RenameAsset(path, newName);
                if (string.IsNullOrEmpty(err))
                {
                    assetsRenamed++;
                    renamed.Add($"- {path} -> {newName}");
                }
            }

            AssetDatabase.SaveAssets();

            Debug.Log(
                "[MigrateFodderToTrash] Completed.\n" +
                "- EnemyTier: Fodder was renamed to Trash (serialized numeric values unchanged).\n" +
                $"- Assets renamed (filename contains 'Fodder'): {assetsRenamed}\n" +
                (renamed.Count > 0 ? string.Join("\n", renamed) : string.Empty)
            );
        }

        private static string ReplaceOrdinalIgnoreCase(string input, string oldValue, string newValue)
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(oldValue)) return input;

            int start = 0;
            while (true)
            {
                int idx = input.IndexOf(oldValue, start, StringComparison.OrdinalIgnoreCase);
                if (idx < 0) break;
                input = input.Substring(0, idx) + newValue + input.Substring(idx + oldValue.Length);
                start = idx + newValue.Length;
            }

            return input;
        }
    }
}
#endif
