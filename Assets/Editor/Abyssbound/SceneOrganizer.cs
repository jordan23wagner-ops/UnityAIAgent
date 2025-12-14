#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abyssbound.EditorTools
{
    public static class SceneOrganizer
    {
        private const string SceneRootName = "__SCENE";
        private const string SystemsRootName = "_Systems";

        private const string MenuBase = "Abyssbound/Tools/Organize Scene Hierarchy";

        [MenuItem(MenuBase)]
        public static void OrganizeDryRun()
        {
            Organize(apply: false);
        }

        [MenuItem(MenuBase + " (Apply)")]
        public static void OrganizeApply()
        {
            Organize(apply: true);
        }

        private static void Organize(bool apply)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[SceneOrganizer] Refusing to organize while in Play Mode.");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[SceneOrganizer] No active scene found.");
                return;
            }

            var sceneRoot = EnsureRoot(SceneRootName, apply);
            var systemsRoot = EnsureChild(sceneRoot.transform, SystemsRootName, apply);

            string zoneRootName = GetZoneRootName(scene);
            var zoneRoot = EnsureChild(sceneRoot.transform, zoneRootName, apply);

            var envRoot = EnsureChild(zoneRoot.transform, "_ENV", apply);
            var gameplayRoot = EnsureChild(zoneRoot.transform, "_GAMEPLAY", apply);
            var encountersRoot = EnsureChild(zoneRoot.transform, "_ENCOUNTERS", apply);
            var actorsRoot = EnsureChild(zoneRoot.transform, "_ACTORS", apply);

            var protectedRoots = new HashSet<Transform>
            {
                sceneRoot.transform,
                systemsRoot,
                zoneRoot.transform,
                envRoot,
                gameplayRoot,
                encountersRoot,
                actorsRoot,
            };

            // Consider all root objects and any objects not already under __SCENE.
            var candidates = new List<GameObject>();
            var all = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                var go = all[i];
                if (go == null) continue;

                // Skip DontDestroyOnLoad / invalid scene objects.
                if (!go.scene.IsValid()) continue;
                if (string.Equals(go.scene.name, "DontDestroyOnLoad", StringComparison.OrdinalIgnoreCase)) continue;

                // Skip our organizer roots.
                if (IsAnyOf(go.transform, protectedRoots))
                    continue;

                candidates.Add(go);
            }

            var moves = new List<MoveOp>();

            for (int i = 0; i < candidates.Count; i++)
            {
                var go = candidates[i];
                if (go == null) continue;

                // Never move __SCENE itself.
                if (string.Equals(go.name, SceneRootName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var dest = ClassifyDestination(go, envRoot, gameplayRoot, encountersRoot, actorsRoot);
                if (dest == null)
                    continue;

                if (go.transform.IsChildOf(dest))
                    continue; // already correctly parented

                // Safety: never move any of the roots.
                if (IsAnyOf(go.transform, protectedRoots))
                    continue;

                moves.Add(new MoveOp(go, dest, GetTransformPath(go.transform), GetTransformPath(dest), ClassifyReason(go)));
            }

            LogMoves(scene.name, apply, moves);

            if (!apply)
                return;

            if (moves.Count == 0)
                return;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Organize Scene Hierarchy");

            for (int i = 0; i < moves.Count; i++)
            {
                var op = moves[i];
                if (op.GameObject == null || op.Destination == null) continue;

                // Preserve world transform.
                Undo.SetTransformParent(op.GameObject.transform, op.Destination, "Organize Scene Hierarchy");
            }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static string GetZoneRootName(Scene scene)
        {
            if (scene.name.IndexOf("Zone1", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Zone1";
            return string.IsNullOrWhiteSpace(scene.name) ? "Scene" : scene.name;
        }

        private static GameObject EnsureRoot(string name, bool apply)
        {
            var existing = GameObject.Find(name);
            if (existing != null)
            {
                // Ensure it is a root object.
                if (existing.transform.parent != null)
                {
                    if (apply)
                        Undo.SetTransformParent(existing.transform, null, "Organize Scene Hierarchy");
                }
                return existing;
            }

            if (!apply)
            {
                // For dry run, create a temporary in-memory object? No; just report.
                // We'll still create it to compute destinations correctly, but via Undo so it can be reverted.
            }

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Organize Scene Hierarchy");
            go.transform.SetParent(null);
            return go;
        }

        private static Transform EnsureChild(Transform parent, string childName, bool apply)
        {
            if (parent == null)
                return null;

            Transform found = null;
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c != null && string.Equals(c.name, childName, StringComparison.Ordinal))
                {
                    found = c;
                    break;
                }
            }

            if (found != null)
                return found;

            var go = new GameObject(childName);
            Undo.RegisterCreatedObjectUndo(go, "Organize Scene Hierarchy");
            // Preserve world doesn't matter for an empty root.
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static Transform ClassifyDestination(
            GameObject go,
            Transform envRoot,
            Transform gameplayRoot,
            Transform encountersRoot,
            Transform actorsRoot)
        {
            if (go == null)
                return null;

            // ACTORS (highest priority)
            if (NameContainsAny(go.name, "Boss_", "Enemy_", "Player_") ||
                HasAnyComponent(go, "EnemyHealth", "DropOnDeath", "PlayerHealth", "SimplePlayerCombat"))
            {
                return actorsRoot;
            }

            // ENCOUNTERS
            if (NameContainsAny(go.name, "Encounter", "SpawnerController", "BossEncounter") ||
                HasAnyComponent(go, "BossEncounterController"))
            {
                return encountersRoot;
            }

            // GAMEPLAY
            if (NameContainsAny(go.name, "Spawn", "Waypoint", "Trigger", "Gate", "Portal") ||
                HasTriggerCollider(go))
            {
                return gameplayRoot;
            }

            // ENV
            if (NameContainsAny(go.name, "Floor", "Wall", "Rock", "Prop", "Deco", "Tree", "Terrain", "Cliff") ||
                IsLikelyEnvironment(go))
            {
                return envRoot;
            }

            return null;
        }

        private static string ClassifyReason(GameObject go)
        {
            if (go == null) return "";

            if (NameContainsAny(go.name, "Boss_", "Enemy_", "Player_") ||
                HasAnyComponent(go, "EnemyHealth", "DropOnDeath", "PlayerHealth", "SimplePlayerCombat"))
                return "Actors rule";

            if (NameContainsAny(go.name, "Encounter", "SpawnerController", "BossEncounter") ||
                HasAnyComponent(go, "BossEncounterController"))
                return "Encounters rule";

            if (NameContainsAny(go.name, "Spawn", "Waypoint", "Trigger", "Gate", "Portal") ||
                HasTriggerCollider(go))
                return "Gameplay rule";

            if (NameContainsAny(go.name, "Floor", "Wall", "Rock", "Prop", "Deco", "Tree", "Terrain", "Cliff"))
                return "Env name rule";

            if (IsLikelyEnvironment(go))
                return "Env renderer heuristic";

            return "";
        }

        private static bool NameContainsAny(string name, params string[] tokens)
        {
            if (string.IsNullOrWhiteSpace(name) || tokens == null)
                return false;

            for (int i = 0; i < tokens.Length; i++)
            {
                var t = tokens[i];
                if (string.IsNullOrWhiteSpace(t)) continue;
                if (name.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static bool HasTriggerCollider(GameObject go)
        {
            if (go == null) return false;

            var colliders = go.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                var c = colliders[i];
                if (c != null && c.isTrigger)
                    return true;
            }

            return false;
        }

        private static bool IsLikelyEnvironment(GameObject go)
        {
            if (go == null) return false;

            // Must have a renderer.
            Renderer renderer = go.GetComponentInChildren<MeshRenderer>(true);
            if (renderer == null)
                renderer = go.GetComponentInChildren<SkinnedMeshRenderer>(true);

            if (renderer == null)
                return false;

            // Heuristic: has no known gameplay components and doesn't look like gameplay by collider trigger.
            if (HasAnyComponent(go,
                    "EnemyHealth",
                    "DropOnDeath",
                    "PlayerHealth",
                    "SimplePlayerCombat",
                    "BossEncounterController",
                    "BossGate",
                    "GateDefinition",
                    "PlayerInventory"))
                return false;

            if (HasTriggerCollider(go))
                return false;

            return true;
        }

        private static bool HasAnyComponent(GameObject go, params string[] typeNames)
        {
            if (go == null || typeNames == null || typeNames.Length == 0)
                return false;

            // Scan MonoBehaviours in children to catch typical patterns.
            var monos = go.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < monos.Length; i++)
            {
                var m = monos[i];
                if (m == null) continue;

                string tn = m.GetType().Name;
                for (int j = 0; j < typeNames.Length; j++)
                {
                    if (string.Equals(tn, typeNames[j], StringComparison.Ordinal))
                        return true;
                }
            }

            return false;
        }

        private static bool IsAnyOf(Transform t, HashSet<Transform> set)
        {
            if (t == null || set == null) return false;
            if (set.Contains(t)) return true;
            return false;
        }

        private static string GetTransformPath(Transform t)
        {
            if (t == null) return "<null>";

            var sb = new StringBuilder();
            while (t != null)
            {
                if (sb.Length == 0) sb.Insert(0, t.name);
                else sb.Insert(0, t.name + "/");
                t = t.parent;
            }
            return sb.ToString();
        }

        private static void LogMoves(string sceneName, bool apply, List<MoveOp> moves)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[SceneOrganizer] Scene='{sceneName}' mode={(apply ? "APPLY" : "DRY RUN")}");
            sb.AppendLine($"[SceneOrganizer] Moves: {moves.Count}");

            for (int i = 0; i < moves.Count; i++)
            {
                var m = moves[i];
                sb.AppendLine($"  - {m.FromPath} -> {m.ToPath} | {m.Reason}");
            }

            Debug.Log(sb.ToString());
        }

        private readonly struct MoveOp
        {
            public readonly GameObject GameObject;
            public readonly Transform Destination;
            public readonly string FromPath;
            public readonly string ToPath;
            public readonly string Reason;

            public MoveOp(GameObject go, Transform dest, string fromPath, string toPath, string reason)
            {
                GameObject = go;
                Destination = dest;
                FromPath = fromPath;
                ToPath = toPath;
                Reason = reason;
            }
        }
    }
}
#endif
