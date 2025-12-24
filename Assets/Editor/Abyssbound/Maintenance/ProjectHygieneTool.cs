#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Abyss.Loot;
using Abyss.Items;
using Abyssbound.Loot;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abyssbound.EditorTools
{
    public sealed class ProjectHygieneTool : EditorWindow
    {
        private const string MenuPath = "Tools/Abyssbound/Maintenance/Project Hygiene";

        private bool _dryRun = true;
        private bool _includeSceneHierarchy = false;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            var w = GetWindow<ProjectHygieneTool>(utility: false, title: "Project Hygiene");
            w.minSize = new Vector2(520, 260);
            w.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Project Hygiene (Safe)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Dry Run is recommended first. Asset moves use AssetDatabase.MoveAsset (references preserved).\n" +
                "Assets under Assets/Resources are not moved by default to avoid breaking Resources.Load workflows.",
                MessageType.Info);

            _dryRun = EditorGUILayout.ToggleLeft("Dry Run (do not modify)", _dryRun);
            _includeSceneHierarchy = EditorGUILayout.ToggleLeft("Organize Scene Hierarchy (optional)", _includeSceneHierarchy);

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(_dryRun ? "Dry Run" : "Apply", GUILayout.Height(32)))
                {
                    Run(_dryRun, _includeSceneHierarchy);
                }

                if (GUILayout.Button("Apply (Assets Only)", GUILayout.Height(32)))
                {
                    Run(dryRun: false, includeSceneHierarchy: false);
                }
            }

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Creates these folders if missing:");
            EditorGUILayout.LabelField("- Assets/GameData/Loot/ (and Zone1/)");
            EditorGUILayout.LabelField("- Assets/GameData/Affixes/, Items/, Rarities/, Sets/");
            EditorGUILayout.LabelField("- Assets/Prefabs/Enemies/, Prefabs/UI/");
            EditorGUILayout.LabelField("- Assets/Scripts/Abyssbound/, Assets/Editor/Abyssbound/");
        }

        private static void Run(bool dryRun, bool includeSceneHierarchy)
        {
            EnsureStandardFolders();

            if (includeSceneHierarchy && Application.isPlaying)
            {
                includeSceneHierarchy = false;
                Debug.Log("[ProjectHygiene] Run this outside Play Mode.");
            }

            var assetSummary = OrganizeProjectAssets(dryRun);

            if (includeSceneHierarchy)
            {
                var sceneSummary = OrganizeSceneHierarchy(dryRun);
                Debug.Log($"[ProjectHygiene] Done. dryRun={dryRun}.\n{assetSummary}\n{sceneSummary}");
            }
            else
            {
                Debug.Log($"[ProjectHygiene] Done. dryRun={dryRun}.\n{assetSummary}");
            }
        }

        private static void EnsureStandardFolders()
        {
            EnsureFolder("Assets/GameData");
            EnsureFolder("Assets/GameData/Loot");
            EnsureFolder("Assets/GameData/Loot/Zone1");
            EnsureFolder("Assets/GameData/Affixes");
            EnsureFolder("Assets/GameData/Items");
            EnsureFolder("Assets/GameData/Rarities");
            EnsureFolder("Assets/GameData/Sets");

            EnsureFolder("Assets/Prefabs");
            EnsureFolder("Assets/Prefabs/Enemies");
            EnsureFolder("Assets/Prefabs/UI");

            EnsureFolder("Assets/Scripts");
            EnsureFolder("Assets/Scripts/Abyssbound");

            EnsureFolder("Assets/Editor");
            EnsureFolder("Assets/Editor/Abyssbound");

            AssetDatabase.Refresh();
        }

        private static string OrganizeProjectAssets(bool dryRun)
        {
            // Pre-scan for missing scripts in prefabs so we can surface exact asset paths
            // and avoid chasing warnings while doing asset organization.
            var missingScriptsSummary = MissingScriptsPrefabTools.ScanPrefabsForMissingScriptsSummary(
                new[] { "Assets/Prefabs", "Assets/GameData" },
                maxList: 50);

            var moves = new List<(string src, string dst, string reason)>();
            int skippedResources = 0;
            int skippedAlready = 0;
            int skippedUnknown = 0;
            int skippedPrefabs = 0;

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddAssetsOfType(string findFilter, string reason, Func<UnityEngine.Object, string> getDestFolder)
            {
                var guids = AssetDatabase.FindAssets(findFilter, new[] { "Assets" });
                for (int i = 0; i < guids.Length; i++)
                {
                    var srcPath = AssetDatabase.GUIDToAssetPath(guids[i])?.Replace('\\', '/');
                    if (string.IsNullOrWhiteSpace(srcPath)) continue;

                    if (!srcPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Guard: FindAssets can return prefab GUIDs when a prefab contains ScriptableObject sub-assets.
                    // Never load prefabs during asset-organization.
                    if (srcPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    {
                        skippedPrefabs++;
                        continue;
                    }

                    if (!visited.Add(srcPath))
                        continue;

                    if (srcPath.StartsWith("Assets/Resources/", StringComparison.OrdinalIgnoreCase))
                    {
                        skippedResources++;
                        continue;
                    }

                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(srcPath);
                    if (asset == null) continue;

                    string destFolder = null;
                    try { destFolder = getDestFolder(asset)?.Replace('\\', '/'); }
                    catch { destFolder = null; }

                    if (string.IsNullOrWhiteSpace(destFolder))
                    {
                        skippedUnknown++;
                        continue;
                    }

                    if (srcPath.StartsWith(destFolder + "/", StringComparison.OrdinalIgnoreCase))
                    {
                        skippedAlready++;
                        continue;
                    }

                    var fileName = Path.GetFileName(srcPath);
                    var dstPath = (destFolder + "/" + fileName).Replace('\\', '/');

                    if (string.Equals(srcPath, dstPath, StringComparison.OrdinalIgnoreCase))
                    {
                        skippedAlready++;
                        continue;
                    }

                    // Don't overwrite.
                    if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(dstPath) != null)
                    {
                        skippedAlready++;
                        continue;
                    }

                    moves.Add((srcPath, dstPath, reason));
                }
            }

            // We operate on a small set of known types. Using typed queries avoids loading prefabs
            // (which can emit missing-script warnings when the prefab is broken).
            AddAssetsOfType(
                "t:Abyss.Loot.ZoneLootTable",
                "ZoneLootTable",
                asset =>
                {
                    var name = asset != null ? asset.name : string.Empty;
                    return name.StartsWith("Zone1", StringComparison.OrdinalIgnoreCase)
                        ? "Assets/GameData/Loot/Zone1"
                        : "Assets/GameData/Loot";
                });

            AddAssetsOfType("t:Abyssbound.Loot.LootTableSO", "LootTableSO", _ => "Assets/GameData/Loot");
            AddAssetsOfType("t:DropTable", "DropTable", _ => "Assets/GameData/Loot");
            AddAssetsOfType("t:Abyssbound.Loot.AffixDefinitionSO", "AffixDefinitionSO", _ => "Assets/GameData/Affixes");

            // NOTE: sets currently load via Resources/Loot/Sets in runtime; we still skip any assets under Assets/Resources above.
            AddAssetsOfType("t:Abyssbound.Loot.SetDefinitionSO", "SetDefinitionSO", _ => "Assets/GameData/Sets");

            AddAssetsOfType("t:Abyssbound.Loot.ItemDefinitionSO", "ItemDefinitionSO", _ => "Assets/GameData/Items");
            AddAssetsOfType("t:Abyss.Items.ItemDefinition", "Abyss ItemDefinition", _ => "Assets/GameData/Items");

            int moved = 0;
            int failed = 0;

            if (!dryRun)
            {
                AssetDatabase.StartAssetEditing();
                try
                {
                    foreach (var m in moves)
                    {
                        var err = AssetDatabase.MoveAsset(m.src, m.dst);
                        if (string.IsNullOrEmpty(err)) moved++;
                        else failed++;
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
            }

            string header = $"[ProjectHygiene] Asset organization: plannedMoves={moves.Count}, moved={moved}, failed={failed}, skippedAlreadyOk={skippedAlready}, skippedResources={skippedResources}, skippedUnknownType={skippedUnknown}, skippedPrefabPaths={skippedPrefabs}.";

            if (moves.Count == 0)
                return header + "\n" + missingScriptsSummary;

            // Print one compact block (non-spammy): first N moves.
            const int maxLines = 25;
            var lines = new List<string>(maxLines + 2) { header };

            for (int i = 0; i < Math.Min(maxLines, moves.Count); i++)
            {
                var m = moves[i];
                lines.Add($"- {(dryRun ? "[DRY]" : "[MOVE]")} {m.reason}: '{m.src}' -> '{m.dst}'");
            }

            if (moves.Count > maxLines)
                lines.Add($"- ... ({moves.Count - maxLines} more)");

            return string.Join("\n", lines) + "\n" + missingScriptsSummary;
        }

        private static string OrganizeSceneHierarchy(bool dryRun)
        {
            if (Application.isPlaying)
                return "[ProjectHygiene] Run this outside Play Mode.";

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
                return "[ProjectHygiene] Scene organization skipped (no loaded active scene).";

            var roots = scene.GetRootGameObjects();

            GameObject systems = FindOrCreateRoot("[SYSTEMS]", dryRun);
            GameObject ui = FindOrCreateRoot("[UI]", dryRun);
            GameObject world = FindOrCreateRoot("[WORLD]", dryRun);
            GameObject enemies = FindOrCreateRoot("[ENEMIES]", dryRun);

            var protectedSet = new HashSet<GameObject>(new[] { systems, ui, world, enemies }.Where(x => x != null));

            int planned = 0;
            int moved = 0;
            int skipped = 0;

            var preview = new List<string>();

            foreach (var go in roots)
            {
                if (go == null) continue;
                if (protectedSet.Contains(go)) continue;

                // Keep Player roots stable; if you want them organized later, add a dedicated bucket.
                if (go.name.StartsWith("Player", StringComparison.OrdinalIgnoreCase))
                {
                    skipped++;
                    continue;
                }

                var dest = ClassifyRoot(go, systems, ui, world, enemies);
                if (dest == null)
                {
                    skipped++;
                    continue;
                }

                if (go.transform.parent == dest.transform)
                {
                    skipped++;
                    continue;
                }

                planned++;
                preview.Add($"- {(dryRun ? "[DRY]" : "[MOVE]")} '{go.name}' -> '{dest.name}'");

                if (!dryRun)
                {
                    Undo.SetTransformParent(go.transform, dest.transform, "Project Hygiene: Organize Scene");
                    moved++;
                }
            }

            // Never dirty scenes in Play Mode (Run() already blocks this, but keep as a second line of defense).
            if (!dryRun && planned > 0 && !Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(scene);

            var header = $"[ProjectHygiene] Scene organization: plannedMoves={planned}, moved={moved}, skipped={skipped}.";
            if (planned == 0) return header;

            const int maxLines = 25;
            if (preview.Count > maxLines)
            {
                preview = preview.Take(maxLines).Concat(new[] { $"- ... ({planned - maxLines} more)" }).ToList();
            }

            return header + "\n" + string.Join("\n", preview);
        }

        private static GameObject FindOrCreateRoot(string name, bool dryRun)
        {
            var scene = SceneManager.GetActiveScene();
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go != null && string.Equals(go.name, name, StringComparison.Ordinal))
                    return go;
            }

            if (dryRun)
                return null;

            var created = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(created, "Project Hygiene: Create Root");
            created.transform.SetParent(null);
            return created;
        }

        private static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(path)) return;

            var parts = path.Split('/');
            if (parts.Length < 2) return;

            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static GameObject ClassifyRoot(GameObject go, GameObject systems, GameObject ui, GameObject world, GameObject enemies)
        {
            if (go == null) return null;

            // UI
            if (go.GetComponentInChildren<Canvas>(true) != null || NameContains(go.name, "UI", "HUD", "Canvas", "EventSystem"))
                return ui;

            // Enemies / encounters
            if (HasAnyComponentByName(go, "EnemyHealth", "DropOnDeath", "EnemyAggroChase") || NameContains(go.name, "Enemy", "Boss", "Spawner", "Encounter"))
                return enemies;

            // Systems / managers
            if (HasAnyComponentByName(go, "GameBootstrapper", "LootRegistryBootstrap", "DevCheats") || NameContains(go.name, "Manager", "Registry", "Bootstrap", "System"))
                return systems;

            // World (default)
            if (NameContains(go.name, "Terrain", "World", "Env", "Level", "Map", "Props", "Buildings"))
                return world;

            // If uncertain, do not move.
            return null;
        }

        private static bool NameContains(string name, params string[] tokens)
        {
            if (string.IsNullOrWhiteSpace(name) || tokens == null) return false;
            for (int i = 0; i < tokens.Length; i++)
            {
                var t = tokens[i];
                if (string.IsNullOrWhiteSpace(t)) continue;
                if (name.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private static bool HasAnyComponentByName(GameObject root, params string[] typeNames)
        {
            if (root == null || typeNames == null || typeNames.Length == 0) return false;

            var comps = root.GetComponentsInChildren<Component>(true);
            if (comps == null) return false;

            for (int i = 0; i < comps.Length; i++)
            {
                var c = comps[i];
                if (c == null) continue;
                var n = c.GetType().Name;
                for (int j = 0; j < typeNames.Length; j++)
                {
                    if (string.Equals(n, typeNames[j], StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }
    }
}
#endif
