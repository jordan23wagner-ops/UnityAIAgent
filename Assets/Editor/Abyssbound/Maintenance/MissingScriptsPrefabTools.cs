#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Abyssbound.EditorTools
{
    public static class MissingScriptsPrefabTools
    {
        private const string ScanMenu = "Tools/Abyssbound/Maintenance/Scan Missing Scripts (Prefabs)";
        private const string FixMenu = "Tools/Abyssbound/Maintenance/Fix Missing Scripts (Prefabs)";

        // ProjectHygieneTool uses this helper to append a non-spammy summary.
        public static string ScanPrefabsForMissingScriptsSummary(string[] searchFolders, int maxList)
        {
            var result = ScanPrefabsForMissingScripts(searchFolders);
            return FormatScanSummary(result, maxList);
        }

        [MenuItem(ScanMenu)]
        public static void ScanMenuItem()
        {
            if (Application.isPlaying)
            {
                Debug.Log("[MissingScripts] Run this outside Play Mode.");
                return;
            }

            var result = ScanPrefabsForMissingScripts(DefaultFolders());
            Debug.Log(FormatScanSummary(result, maxList: 50));
        }

        [MenuItem(FixMenu)]
        public static void FixMenuItem()
        {
            if (Application.isPlaying)
            {
                Debug.Log("[MissingScripts] Run this outside Play Mode.");
                return;
            }

            var result = FixMissingScriptsInPrefabs(DefaultFolders());
            Debug.Log(FormatFixSummary(result, maxList: 50));
        }

        private static string[] DefaultFolders()
        {
            // Prefer a narrower scan to keep this fast.
            // Still covers the most likely prefab storage locations.
            return new[] { "Assets/Prefabs", "Assets/GameData" };
        }

        private struct ScanResult
        {
            public int PrefabsScanned;
            public int PrefabsWithMissingScripts;
            public List<string> PrefabPathsWithMissingScripts;
            public int Errors;
            public List<string> ErrorPaths;
        }

        private struct FixResult
        {
            public int PrefabsScanned;
            public int PrefabsFixed;
            public int ComponentsRemoved;
            public List<string> FixedPrefabPaths;
            public int Errors;
            public List<string> ErrorPaths;
        }

        private static ScanResult ScanPrefabsForMissingScripts(string[] searchFolders)
        {
            var result = new ScanResult
            {
                PrefabPathsWithMissingScripts = new List<string>(),
                ErrorPaths = new List<string>()
            };

            string[] folders = NormalizeSearchFolders(searchFolders);
            var guids = AssetDatabase.FindAssets("t:Prefab", folders);

            result.PrefabsScanned = guids.Length;

            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrWhiteSpace(path)) continue;

                GameObject root = null;
                try
                {
                    root = PrefabUtility.LoadPrefabContents(path);
                    if (root == null) continue;

                    int missing = CountMissingScripts(root);
                    if (missing > 0)
                    {
                        result.PrefabsWithMissingScripts++;
                        result.PrefabPathsWithMissingScripts.Add(path.Replace('\\', '/'));
                    }
                }
                catch
                {
                    result.Errors++;
                    if (result.ErrorPaths.Count < 50)
                        result.ErrorPaths.Add(path.Replace('\\', '/'));
                }
                finally
                {
                    if (root != null)
                        PrefabUtility.UnloadPrefabContents(root);
                }
            }

            return result;
        }

        private static FixResult FixMissingScriptsInPrefabs(string[] searchFolders)
        {
            var result = new FixResult
            {
                FixedPrefabPaths = new List<string>(),
                ErrorPaths = new List<string>()
            };

            string[] folders = NormalizeSearchFolders(searchFolders);
            var guids = AssetDatabase.FindAssets("t:Prefab", folders);

            result.PrefabsScanned = guids.Length;

            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrWhiteSpace(path)) continue;

                GameObject root = null;
                try
                {
                    root = PrefabUtility.LoadPrefabContents(path);
                    if (root == null) continue;

                    int before = CountMissingScripts(root);
                    if (before <= 0)
                        continue;

                    int removed = RemoveMissingScripts(root);
                    int after = CountMissingScripts(root);

                    // Only save if we made progress.
                    if (removed > 0 || after < before)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        result.PrefabsFixed++;
                        result.ComponentsRemoved += removed;
                        result.FixedPrefabPaths.Add(path.Replace('\\', '/'));
                    }
                }
                catch
                {
                    result.Errors++;
                    if (result.ErrorPaths.Count < 50)
                        result.ErrorPaths.Add(path.Replace('\\', '/'));
                }
                finally
                {
                    if (root != null)
                        PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return result;
        }

        private static int CountMissingScripts(GameObject root)
        {
            if (root == null) return 0;

            int total = 0;
            var transforms = root.GetComponentsInChildren<Transform>(true);
            if (transforms == null) return 0;

            for (int i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == null) continue;
                total += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            }

            return total;
        }

        private static int RemoveMissingScripts(GameObject root)
        {
            if (root == null) return 0;

            int removed = 0;
            var transforms = root.GetComponentsInChildren<Transform>(true);
            if (transforms == null) return 0;

            for (int i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == null) continue;
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            }

            return removed;
        }

        private static string[] NormalizeSearchFolders(string[] searchFolders)
        {
            if (searchFolders == null || searchFolders.Length == 0)
                return new[] { "Assets" };

            var cleaned = searchFolders
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Replace('\\', '/'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return cleaned.Length > 0 ? cleaned : new[] { "Assets" };
        }

        private static string FormatScanSummary(ScanResult result, int maxList)
        {
            maxList = Mathf.Max(1, maxList);

            var lines = new List<string>(64)
            {
                "[MissingScripts] Prefab scan complete.",
                $"- Prefabs scanned: {result.PrefabsScanned}",
                $"- Prefabs with missing scripts: {result.PrefabsWithMissingScripts}",
            };

            if (result.PrefabsWithMissingScripts > 0)
            {
                var list = result.PrefabPathsWithMissingScripts ?? new List<string>();
                int shown = Mathf.Min(maxList, list.Count);
                lines.Add("- Paths:");
                for (int i = 0; i < shown; i++)
                    lines.Add($"  - {list[i]}");

                if (list.Count > shown)
                    lines.Add($"  - ... and {list.Count - shown} more");
            }

            if (result.Errors > 0)
            {
                lines.Add($"- Errors loading prefabs: {result.Errors} (showing up to 50 paths)");
                if (result.ErrorPaths != null && result.ErrorPaths.Count > 0)
                {
                    for (int i = 0; i < result.ErrorPaths.Count; i++)
                        lines.Add($"  - {result.ErrorPaths[i]}");
                }
            }

            return string.Join("\n", lines);
        }

        private static string FormatFixSummary(FixResult result, int maxList)
        {
            maxList = Mathf.Max(1, maxList);

            var lines = new List<string>(64)
            {
                "[MissingScripts] Prefab fix complete.",
                $"- Prefabs scanned: {result.PrefabsScanned}",
                $"- Prefabs fixed: {result.PrefabsFixed}",
                $"- Missing components removed: {result.ComponentsRemoved}",
            };

            if (result.PrefabsFixed > 0)
            {
                var list = result.FixedPrefabPaths ?? new List<string>();
                int shown = Mathf.Min(maxList, list.Count);
                lines.Add("- Fixed paths:");
                for (int i = 0; i < shown; i++)
                    lines.Add($"  - {list[i]}");

                if (list.Count > shown)
                    lines.Add($"  - ... and {list.Count - shown} more");
            }

            if (result.Errors > 0)
            {
                lines.Add($"- Errors fixing prefabs: {result.Errors} (showing up to 50 paths)");
                if (result.ErrorPaths != null && result.ErrorPaths.Count > 0)
                {
                    for (int i = 0; i < result.ErrorPaths.Count; i++)
                        lines.Add($"  - {result.ErrorPaths[i]}");
                }
            }

            return string.Join("\n", lines);
        }
    }
}
#endif
