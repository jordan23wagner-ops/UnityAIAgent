#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abyss.EditorTools
{
    public static class SceneHierarchyOrganizer
    {
        private const string MenuPrint = "Tools/Abyss/Scene/Print Root Summary";
        private const string MenuOrganize = "Tools/Abyss/Scene/Organize Hierarchy (Safe)";

        private const string RootName = "Scene_Root";
        private const string WorldRootName = "World";
        private const string ZoneRootName = "Zone";
        private const string BossRootName = "BossZone";
        private const string TownRootName = "Town";
        private const string UIRootName = "UI";
        private const string SystemsRootName = "Systems";
        private const string PlayerRootName = "Player";

        [MenuItem(MenuPrint)]
        public static void PrintRootSummary()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("No valid active scene.");
                return;
            }

            var roots = scene.GetRootGameObjects();
            Array.Sort(roots, (a, b) => string.CompareOrdinal(a.name, b.name));

            var lines = new List<string>
            {
                $"[SceneHierarchyOrganizer] Root Summary: scene='{scene.name}' rootCount={roots.Length}",
            };

            foreach (var go in roots)
            {
                if (go == null) continue;
                lines.Add($"- {go.name} (children={go.transform.childCount}) -> {SuggestBucket(go.name)}");
            }

            Debug.Log(string.Join("\n", lines));
        }

        [MenuItem(MenuOrganize)]
        public static void OrganizeHierarchySafe()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("No valid active scene.");
                return;
            }

            var sceneRoot = FindOrCreateRoot(RootName);
            var worldRoot = FindOrCreateChild(sceneRoot.transform, WorldRootName);
            var zoneRoot = FindOrCreateChild(sceneRoot.transform, ZoneRootName);
            var bossRoot = FindOrCreateChild(sceneRoot.transform, BossRootName);
            var townRoot = FindOrCreateChild(sceneRoot.transform, TownRootName);
            var uiRoot = FindOrCreateChild(sceneRoot.transform, UIRootName);
            var systemsRoot = FindOrCreateChild(sceneRoot.transform, SystemsRootName);
            var playerRoot = FindOrCreateChild(sceneRoot.transform, PlayerRootName);

            // Only re-parent top-level roots (keeps existing internal structure intact).
            var roots = scene.GetRootGameObjects();
            int moved = 0;
            foreach (var go in roots)
            {
                if (go == null) continue;
                if (go.transform.parent != null) continue; // should be root already

                if (go.name == RootName) continue;

                var bucket = SuggestBucket(go.name);
                Transform parent = bucket switch
                {
                    Bucket.UI => uiRoot.transform,
                    Bucket.Town => townRoot.transform,
                    Bucket.Systems => systemsRoot.transform,
                    Bucket.Player => playerRoot.transform,
                    Bucket.Boss => bossRoot.transform,
                    Bucket.Zone => zoneRoot.transform,
                    Bucket.World => worldRoot.transform,
                    _ => worldRoot.transform,
                };

                Undo.SetTransformParent(go.transform, parent, "Organize scene root");
                moved++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[SceneHierarchyOrganizer] Organized hierarchy. movedRoots={moved}");
        }

        private enum Bucket
        {
            World,
            Zone,
            Boss,
            Town,
            UI,
            Systems,
            Player
        }

        private static Bucket SuggestBucket(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return Bucket.World;

            // UI
            if (name.Equals("EventSystem", StringComparison.OrdinalIgnoreCase) ||
                name.IndexOf("Canvas", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("UI", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("HUD", StringComparison.OrdinalIgnoreCase) >= 0)
                return Bucket.UI;

            // Town
            if (name.Equals("Town_SpawnRoot", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("TownRegistry", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("EdgevilleHub_Root", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Town_", StringComparison.OrdinalIgnoreCase))
                return Bucket.Town;

            // Player
            if (name.Equals("Player_Hero", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Player", StringComparison.OrdinalIgnoreCase))
                return Bucket.Player;

            // Boss
            if (name.StartsWith("Boss_", StringComparison.OrdinalIgnoreCase) ||
                name.IndexOf("Boss", StringComparison.OrdinalIgnoreCase) >= 0)
                return Bucket.Boss;

            // Zone
            if (name.Equals("Zone1", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Zone", StringComparison.OrdinalIgnoreCase))
                return Bucket.Zone;

            // Systems
            if (name.IndexOf("Manager", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Registry", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Bootstrap", StringComparison.OrdinalIgnoreCase) >= 0)
                return Bucket.Systems;

            return Bucket.World;
        }

        private static GameObject FindOrCreateRoot(string name)
        {
            var scene = SceneManager.GetActiveScene();
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go != null && go.name == name)
                    return go;
            }

            var created = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(created, "Create scene root");
            created.transform.position = Vector3.zero;
            created.transform.rotation = Quaternion.identity;
            return created;
        }

        private static GameObject FindOrCreateChild(Transform parent, string name)
        {
            if (parent == null) return null;

            var existing = parent.Find(name);
            if (existing != null) return existing.gameObject;

            var created = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(created, "Create hierarchy bucket");
            created.transform.SetParent(parent, false);
            return created;
        }
    }
}
#endif
