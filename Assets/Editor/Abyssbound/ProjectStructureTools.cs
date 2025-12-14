#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Abyssbound.EditorTools
{
    public static class ProjectStructureTools
    {
        private const string Root = "Assets/Abyssbound";

        [MenuItem("Abyssbound/Tools/Create Abyssbound Folder Structure")]
        public static void CreateFolderStructure()
        {
            EnsureFolder(Root);

            // Scenes
            EnsureFolder(PathCombine(Root, "Scenes"));

            // Prefabs
            EnsureFolder(PathCombine(Root, "Prefabs"));
            EnsureFolder(PathCombine(Root, "Prefabs/Actors"));
            EnsureFolder(PathCombine(Root, "Prefabs/Actors/Bosses"));
            EnsureFolder(PathCombine(Root, "Prefabs/Actors/Enemies"));

            // ScriptableObjects
            EnsureFolder(PathCombine(Root, "ScriptableObjects"));
            EnsureFolder(PathCombine(Root, "ScriptableObjects/Items"));
            EnsureFolder(PathCombine(Root, "ScriptableObjects/DropTables"));
            EnsureFolder(PathCombine(Root, "ScriptableObjects/Gates"));
            EnsureFolder(PathCombine(Root, "ScriptableObjects/Merchants"));

            AssetDatabase.Refresh();
            Debug.Log("[ProjectStructureTools] Ensured folder structure under Assets/Abyssbound");
        }

        [MenuItem("Abyssbound/Tools/Move Known Assets Into Structure (Safe)")]
        public static void MoveKnownAssetsSafe()
        {
            CreateFolderStructure();

            var log = new StringBuilder();
            int moved = 0;
            int skipped = 0;
            int failed = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                MoveScenes(log, ref moved, ref skipped, ref failed);

                MovePrefabsByNameContains("Boss", PathCombine(Root, "Prefabs/Actors/Bosses"), log, ref moved, ref skipped, ref failed);
                MovePrefabsByNameContains("Enemy", PathCombine(Root, "Prefabs/Actors/Enemies"), log, ref moved, ref skipped, ref failed);

                MoveByTypeName("ItemDefinition", PathCombine(Root, "ScriptableObjects/Items"), log, ref moved, ref skipped, ref failed);
                MoveByTypeName("DropTable", PathCombine(Root, "ScriptableObjects/DropTables"), log, ref moved, ref skipped, ref failed);
                MoveByTypeName("GateDefinition", PathCombine(Root, "ScriptableObjects/Gates"), log, ref moved, ref skipped, ref failed);
                MoveByTypeName("MerchantDefinition", PathCombine(Root, "ScriptableObjects/Merchants"), log, ref moved, ref skipped, ref failed);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            log.Insert(0, $"[ProjectStructureTools] Move complete. moved={moved} skipped={skipped} failed={failed}\n");
            Debug.Log(log.ToString());
        }

        private static void MoveScenes(StringBuilder log, ref int moved, ref int skipped, ref int failed)
        {
            var guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
            foreach (var guid in guids)
            {
                var srcPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrWhiteSpace(srcPath)) continue;
                if (!srcPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)) continue;

                // Skip scenes already under our root.
                if (srcPath.Replace('\\', '/').StartsWith(Root + "/Scenes/", StringComparison.OrdinalIgnoreCase))
                {
                    skipped++;
                    continue;
                }

                var sceneName = Path.GetFileNameWithoutExtension(srcPath);
                var group = GetSceneNameGroup(sceneName);
                var destFolder = PathCombine(Root, "Scenes", group);
                EnsureFolder(destFolder);

                var destPath = PathCombine(destFolder, Path.GetFileName(srcPath));
                TryMoveAsset(srcPath, destPath, "Scene", log, ref moved, ref skipped, ref failed);
            }
        }

        private static void MovePrefabsByNameContains(string token, string destFolder, StringBuilder log, ref int moved, ref int skipped, ref int failed)
        {
            EnsureFolder(destFolder);

            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            foreach (var guid in guids)
            {
                var srcPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrWhiteSpace(srcPath)) continue;

                // Match by asset filename, not instance name.
                var fileNameNoExt = Path.GetFileNameWithoutExtension(srcPath);
                if (fileNameNoExt.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                // Skip if already in destination folder.
                var normalizedSrc = srcPath.Replace('\\', '/');
                var normalizedDestFolder = destFolder.Replace('\\', '/');
                if (normalizedSrc.StartsWith(normalizedDestFolder + "/", StringComparison.OrdinalIgnoreCase))
                {
                    skipped++;
                    continue;
                }

                var destPath = PathCombine(destFolder, Path.GetFileName(srcPath));
                TryMoveAsset(srcPath, destPath, $"Prefab(name contains '{token}')", log, ref moved, ref skipped, ref failed);
            }
        }

        private static void MoveByTypeName(string typeName, string destFolder, StringBuilder log, ref int moved, ref int skipped, ref int failed)
        {
            EnsureFolder(destFolder);

            // Use type name string so this script still compiles even if the type isn't present.
            var filter = $"t:{typeName}";
            var guids = AssetDatabase.FindAssets(filter, new[] { "Assets" });
            foreach (var guid in guids)
            {
                var srcPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrWhiteSpace(srcPath)) continue;

                var normalizedSrc = srcPath.Replace('\\', '/');
                var normalizedDestFolder = destFolder.Replace('\\', '/');
                if (normalizedSrc.StartsWith(normalizedDestFolder + "/", StringComparison.OrdinalIgnoreCase))
                {
                    skipped++;
                    continue;
                }

                var destPath = PathCombine(destFolder, Path.GetFileName(srcPath));
                TryMoveAsset(srcPath, destPath, $"{typeName}", log, ref moved, ref skipped, ref failed);
            }
        }

        private static void TryMoveAsset(string srcPath, string destPath, string label, StringBuilder log, ref int moved, ref int skipped, ref int failed)
        {
            srcPath = srcPath.Replace('\\', '/');
            destPath = destPath.Replace('\\', '/');

            if (string.Equals(srcPath, destPath, StringComparison.OrdinalIgnoreCase))
            {
                skipped++;
                return;
            }

            // Don't overwrite.
            if (AssetExists(destPath))
            {
                log.AppendLine($"[SKIP] {label}: destination exists '{destPath}' (from '{srcPath}')");
                skipped++;
                return;
            }

            string error = AssetDatabase.MoveAsset(srcPath, destPath);
            if (string.IsNullOrEmpty(error))
            {
                log.AppendLine($"[MOVE] {label}: '{srcPath}' -> '{destPath}'");
                moved++;
            }
            else
            {
                log.AppendLine($"[FAIL] {label}: '{srcPath}' -> '{destPath}' | {error}");
                failed++;
            }
        }

        private static bool AssetExists(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return false;

            return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null;
        }

        private static string GetSceneNameGroup(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return "Misc";

            // Group by first token before '_' or '-', else group by leading Zone# if present.
            int underscore = sceneName.IndexOf('_');
            int dash = sceneName.IndexOf('-');
            int split = -1;

            if (underscore >= 0 && dash >= 0) split = Math.Min(underscore, dash);
            else if (underscore >= 0) split = underscore;
            else if (dash >= 0) split = dash;

            if (split > 0)
                return SanitizeFolderName(sceneName.Substring(0, split));

            // Common case: Zone1Something -> Zone1
            if (sceneName.StartsWith("Zone", StringComparison.OrdinalIgnoreCase))
            {
                var prefix = ExtractZonePrefix(sceneName);
                if (!string.IsNullOrWhiteSpace(prefix))
                    return SanitizeFolderName(prefix);
            }

            return SanitizeFolderName(sceneName);
        }

        private static string ExtractZonePrefix(string name)
        {
            // Zone + digits, e.g., Zone1, Zone12
            if (string.IsNullOrWhiteSpace(name)) return null;
            if (!name.StartsWith("Zone", StringComparison.OrdinalIgnoreCase)) return null;

            int i = 4;
            while (i < name.Length && char.IsDigit(name[i]))
                i++;

            if (i > 4)
                return name.Substring(0, i);

            return null;
        }

        private static string SanitizeFolderName(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return "Misc";

            foreach (var c in Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');

            return s.Trim();
        }

        private static void EnsureFolder(string assetFolderPath)
        {
            assetFolderPath = assetFolderPath.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(assetFolderPath))
                return;

            var parent = Path.GetDirectoryName(assetFolderPath)?.Replace('\\', '/');
            var name = Path.GetFileName(assetFolderPath);

            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
                return;

            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, name);
        }

        private static string PathCombine(params string[] parts)
        {
            if (parts == null || parts.Length == 0)
                return string.Empty;

            string p = parts[0] ?? string.Empty;
            for (int i = 1; i < parts.Length; i++)
            {
                p = Path.Combine(p, parts[i] ?? string.Empty);
            }
            return p.Replace('\\', '/');
        }
    }
}
#endif
