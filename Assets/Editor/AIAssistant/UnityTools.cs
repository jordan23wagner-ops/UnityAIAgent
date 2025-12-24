// Assets/Editor/AIAssistant/UnityTools.cs
// Contract-compatible stable shell for AiAssistantWindow.

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Game.Systems;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#endif

namespace AIAssistant
{
    public static class UnityTools
    {
#if UNITY_EDITOR
        private const string SceneRootName = "__SCENE";
        private const string SystemsRootName = "_Systems";

        [Serializable]
        private class SceneRootsReport
        {
            public bool pass;
            public string sceneName;
            public string zoneRootName;
            public string[] missing;
            public string[] created;
        }

        [Serializable]
        private class FoundationReport
        {
            public bool pass;
            public string playerPath;
            public string cameraPath;
            public bool hasCanvas;
            public bool hasEventSystem;
            public bool cameraHasTopDownFollow;
            public bool cameraHasTarget;
            public bool hasPlayerHealth;
            public bool hasSimplePlayerCombat;
            public bool hasBootstrapper;
            public string[] notes;
        }

        [Serializable]
        private class ScaleIssue
        {
            public string severity; // "error" | "warning"
            public string category;
            public string path;
            public Vector3 scale;
        }

        [Serializable]
        private class NoScaledParentsReport
        {
            public bool pass;
            public string sceneName;
            public string zoneRootName;
            public ScaleIssue[] issues;
        }

        [Serializable]
        private class PlayerInputStackReport
        {
            public bool pass;
            public string playerPath;
            public string inputActionsPath;
            public string[] planned;
            public string[] applied;
        }
#endif

        private static void AddLog(AiExecutionResult result, string op, string message)
        {
            if (result == null) return;
            result.logs.Add($"{op}: {message}");
        }

        private static void AddWarning(AiExecutionResult result, string op, string message)
        {
            if (result == null) return;
            result.warnings.Add($"{op}: {message}");
        }

        private static void AddError(AiExecutionResult result, string op, string message)
        {
            if (result == null) return;
            result.errors.Add($"{op}: {message}");
            result.success = false;
        }

#if UNITY_EDITOR
        private static SceneRootsReport EnsureSceneRootsInternal(AiExecutionResult result, ExecutionMode mode)
        {
            const string opName = "ensureSceneRoots";
            var created = new List<string>();
            var missing = new List<string>();

            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            string sceneName = scene.IsValid() ? scene.name : "<invalid>";
            string zoneRootName = GetZoneRootName(sceneName);

            var sceneRoot = GameObject.Find(SceneRootName);
            if (sceneRoot == null)
            {
                if (mode == ExecutionMode.Apply)
                {
                    sceneRoot = new GameObject(SceneRootName);
                    UnityEditor.Undo.RegisterCreatedObjectUndo(sceneRoot, "AI ensureSceneRoots");
                    created.Add(SceneRootName);
                }
                else
                {
                    missing.Add(SceneRootName);
                }
            }

            // If we can't create (DryRun), we still return what would be missing.
            if (sceneRoot == null)
            {
                return new SceneRootsReport
                {
                    pass = false,
                    sceneName = sceneName,
                    zoneRootName = zoneRootName,
                    missing = missing.ToArray(),
                    created = created.ToArray()
                };
            }

            var systems = EnsureChild(sceneRoot.transform, SystemsRootName, mode, created);
            var zone = EnsureChild(sceneRoot.transform, zoneRootName, mode, created);

            // Under zone
            EnsureChild(zone, "_ENV", mode, created);
            EnsureChild(zone, "_GAMEPLAY", mode, created);
            EnsureChild(zone, "_ENCOUNTERS", mode, created);
            EnsureChild(zone, "_ACTORS", mode, created);

            if (mode == ExecutionMode.Apply && !Application.isPlaying)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

            AddLog(result, opName, created.Count > 0 ? $"Created: [{string.Join(", ", created)}]" : "No changes.");

            return new SceneRootsReport
            {
                pass = true,
                sceneName = sceneName,
                zoneRootName = zoneRootName,
                missing = Array.Empty<string>(),
                created = created.ToArray()
            };
        }

        private static SceneRootsReport ValidateSceneRootsInternal(AiExecutionResult result)
        {
            var missing = new List<string>();
            var created = Array.Empty<string>();
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            string sceneName = scene.IsValid() ? scene.name : "<invalid>";
            string zoneRootName = GetZoneRootName(sceneName);

            var sceneRoot = GameObject.Find(SceneRootName);
            if (sceneRoot == null)
            {
                missing.Add(SceneRootName);
                return new SceneRootsReport
                {
                    pass = false,
                    sceneName = sceneName,
                    zoneRootName = zoneRootName,
                    missing = missing.ToArray(),
                    created = created
                };
            }

            ValidateChild(sceneRoot.transform, SystemsRootName, missing);
            var zone = FindChild(sceneRoot.transform, zoneRootName);
            if (zone == null)
                missing.Add($"{SceneRootName}/{zoneRootName}");
            else
            {
                ValidateChild(zone, "_ENV", missing);
                ValidateChild(zone, "_GAMEPLAY", missing);
                ValidateChild(zone, "_ENCOUNTERS", missing);
                ValidateChild(zone, "_ACTORS", missing);
            }

            return new SceneRootsReport
            {
                pass = missing.Count == 0,
                sceneName = sceneName,
                zoneRootName = zoneRootName,
                missing = missing.ToArray(),
                created = created
            };
        }

        private static NoScaledParentsReport ValidateNoScaledParentsInternal(AiExecutionResult result)
        {
            const string opName = "validateNoScaledParents";
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            string sceneName = scene.IsValid() ? scene.name : "<invalid>";
            string zoneRootName = GetZoneRootName(sceneName);

            // Find zone roots (supports either __SCENE/ZoneName or bare ZoneName).
            Transform zoneRoot = null;
            var sceneRoot = GameObject.Find(SceneRootName);
            if (sceneRoot != null)
                zoneRoot = FindChild(sceneRoot.transform, zoneRootName);
            if (zoneRoot == null)
            {
                var fallback = GameObject.Find(zoneRootName);
                if (fallback != null) zoneRoot = fallback.transform;
            }

            var issues = new List<ScaleIssue>();

            // Build some roots for category checks.
            Transform envRoot = null;
            if (zoneRoot != null)
                envRoot = FindChild(zoneRoot, "_ENV");

            var all = GameObject.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == null) continue;
                if (!t.gameObject.scene.IsValid()) continue;
                if (string.Equals(t.gameObject.scene.name, "DontDestroyOnLoad", StringComparison.OrdinalIgnoreCase)) continue;

                var scale = t.localScale;
                bool isOne = ApproximatelyOne(scale);
                if (isOne) continue;

                // Rule A: any parent under Zone roots except _ENV.
                if (zoneRoot != null && t.IsChildOf(zoneRoot))
                {
                    bool underEnv = envRoot != null && t.IsChildOf(envRoot);
                    if (!underEnv)
                    {
                        issues.Add(new ScaleIssue
                        {
                            severity = "error",
                            category = "ZoneNonEnvScaled",
                            path = GetTransformPath(t),
                            scale = scale
                        });
                        continue;
                    }

                    // ENV scaled: warning (preferred warning)
                    issues.Add(new ScaleIssue
                    {
                        severity = "warning",
                        category = "EnvScaled",
                        path = GetTransformPath(t),
                        scale = scale
                    });
                    continue;
                }

                // Rule B: actor/gameplay components disallow non-1 scale.
                if (HasAnyComponentByName(t.gameObject, "EnemyHealth", "DropOnDeath", "PlayerHealth", "SimplePlayerCombat"))
                {
                    issues.Add(new ScaleIssue
                    {
                        severity = "error",
                        category = "ActorScaled",
                        path = GetTransformPath(t),
                        scale = scale
                    });
                    continue;
                }

                // Rule C: encounter controllers / spawn points / gates / triggers.
                if (HasAnyComponentByName(t.gameObject, "BossEncounterController", "BossGate") ||
                    NameContainsAny(t.gameObject.name, "Encounter", "Spawner", "Spawn", "Waypoint", "Gate", "Trigger", "Portal"))
                {
                    // Collider trigger is a strong hint.
                    issues.Add(new ScaleIssue
                    {
                        severity = "error",
                        category = "GameplayScaled",
                        path = GetTransformPath(t),
                        scale = scale
                    });
                    continue;
                }

                // If it didn't match any category, ignore.
            }

            bool pass = true;
            for (int i = 0; i < issues.Count; i++)
            {
                if (string.Equals(issues[i].severity, "error", StringComparison.OrdinalIgnoreCase))
                {
                    pass = false;
                    break;
                }
            }

            AddLog(result, opName, $"Issues found: {issues.Count} (errors fail, warnings allowed)");
            for (int i = 0; i < issues.Count; i++)
            {
                var it = issues[i];
                AddLog(result, opName, $"{it.severity.ToUpperInvariant()} {it.category} {it.path} scale={it.scale}");
            }

            return new NoScaledParentsReport
            {
                pass = pass,
                sceneName = sceneName,
                zoneRootName = zoneRootName,
                issues = issues.ToArray()
            };
        }

        private static FoundationReport EnsureFoundationInternal(AiExecutionResult result, ExecutionMode mode)
        {
            const string opName = "ensureFoundation";
            var notes = new List<string>();

            // Prefer bridging to an existing bootstrapper.
            var bootstrapper = FindFirstObjectByTypeInOpenScenes("GameBootstrapper");
            bool hasBootstrapper = bootstrapper != null;

            if (!hasBootstrapper && mode == ExecutionMode.Apply)
            {
                var go = GameObject.Find("_GameBootstrapper");
                if (go == null)
                {
                    go = new GameObject("_GameBootstrapper");
                    UnityEditor.Undo.RegisterCreatedObjectUndo(go, "AI ensureFoundation");
                    notes.Add("Created _GameBootstrapper");
                }

                // Add the component by type name via reflection to avoid hard dependency.
                var t = go.GetComponent("GameBootstrapper");
                if (t == null)
                {
                    UnityEditor.Undo.AddComponent(go, typeof(GameBootstrapper));
                    notes.Add("Added GameBootstrapper");
                }

                bootstrapper = go;
                hasBootstrapper = true;
            }

            if (hasBootstrapper)
            {
                // Trigger its EnsureFoundation method if present (private is fine via reflection).
                var comp = bootstrapper.GetComponent("GameBootstrapper");
                if (comp != null)
                {
                    var mi = comp.GetType().GetMethod("EnsureFoundation", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (mi != null)
                    {
                        mi.Invoke(comp, null);
                        notes.Add("Invoked GameBootstrapper.EnsureFoundation() via reflection");
                    }
                    else
                    {
                        // Fallback: call Awake if it exists (should be public Unity message; not ideal).
                        notes.Add("GameBootstrapper.EnsureFoundation() not found; foundation may still be ensured by Awake/scene load.");
                    }
                }
            }
            else
            {
                // No bootstrapper; do minimal in-scene ensure for editor use only.
                notes.Add("No GameBootstrapper found; using minimal editor ensure.");
                if (mode == ExecutionMode.Apply)
                    MinimalEnsureFoundationInEditor(notes);
            }

            var validation = ValidateFoundationInternal(result);
            validation.notes = notes.ToArray();
            AddLog(result, opName, string.Join(" | ", notes));
            return validation;
        }

        private static PlayerInputStackReport EnsurePlayerInputStackInternal(AiExecutionResult result, ExecutionMode mode, AiCommand cmd)
        {
            const string opName = "ensurePlayerInputStack";
            var planned = new List<string>();
            var applied = new List<string>();

            bool apply = mode == ExecutionMode.Apply;
            bool anyChanged = false;

            string playerTag = cmd != null && !string.IsNullOrWhiteSpace(cmd.playerTag) ? cmd.playerTag : "Player";
            bool ensureCameraPan = cmd == null || cmd.ensureCameraPan;

#if !ENABLE_INPUT_SYSTEM
            AddError(result, opName, "ENABLE_INPUT_SYSTEM is not defined; cannot ensure PlayerInput stack.");
            return new PlayerInputStackReport
            {
                pass = false,
                playerPath = null,
                inputActionsPath = "Assets/Input/InputSystem_Actions.inputactions",
                planned = planned.ToArray(),
                applied = applied.ToArray()
            };
#else
            const string inputActionsPath = "Assets/Input/InputSystem_Actions.inputactions";

            EnsureAssetFoldersForPath(inputActionsPath, apply, planned, applied, ref anyChanged);
            var actionsAsset = EnsureInputActionsAsset(inputActionsPath, apply, planned, applied, ref anyChanged);

            var player = FindPlayerForInputStack(playerTag);
            if (player == null)
            {
                AddError(result, opName, $"Player not found (tag='{playerTag}' or PlayerInventory)." );
                return new PlayerInputStackReport
                {
                    pass = false,
                    playerPath = null,
                    inputActionsPath = inputActionsPath,
                    planned = planned.ToArray(),
                    applied = applied.ToArray()
                };
            }

            // Remove legacy stack pieces that conflict with click-to-move + intent system.
            RemoveIfPresent(player, "PlayerMovement", apply, planned, applied, ref anyChanged);
            RemoveIfPresent(player, "PlayerClickToMoveController", apply, planned, applied, ref anyChanged);
            RemoveIfPresent(player, "PlayerInputGameplayBinder", apply, planned, applied, ref anyChanged);
            RemoveIfPresent(player, "DebugPlayerMover_NewInput", apply, planned, applied, ref anyChanged);

            // Ensure required components.
            var existingPi = player.GetComponent<UnityEngine.InputSystem.PlayerInput>();
            var pi = EnsureComponent<UnityEngine.InputSystem.PlayerInput>(player, apply, planned, applied, ref anyChanged);
            EnsureComponentByName(result, player, "PlayerInputAuthority", apply, planned, applied, ref anyChanged);
            EnsureComponentByName(result, player, "PlayerMovementMotor", apply, planned, applied, ref anyChanged);
            EnsureComponentByName(result, player, "ClickToMoveController", apply, planned, applied, ref anyChanged);
            EnsureComponentByName(result, player, "CombatLoopController", apply, planned, applied, ref anyChanged);
            var combat = EnsureComponent<SimplePlayerCombat>(player, apply, planned, applied, ref anyChanged);

            // Plan/configure PlayerInput even in DryRun (pi may be null because we didn't add it).
            var piForPlanning = pi != null ? pi : existingPi;
            if (piForPlanning != null || existingPi == null)
            {
                if (existingPi == null)
                {
                    planned.Add($"Set PlayerInput.actions = {inputActionsPath}");
                    planned.Add("Set PlayerInput.defaultActionMap = 'Player'");
                    planned.Add("Set PlayerInput.notificationBehavior = InvokeCSharpEvents");
                }

                if (piForPlanning != null && piForPlanning.actions != actionsAsset)
                {
                    planned.Add($"Set PlayerInput.actions = {inputActionsPath}");
                    if (apply)
                    {
                        Undo.RecordObject(piForPlanning, "AI ensurePlayerInputStack");
                        piForPlanning.actions = actionsAsset;
                        EditorUtility.SetDirty(piForPlanning);
                        applied.Add($"Set PlayerInput.actions = {inputActionsPath}");
                        anyChanged = true;
                    }
                }

                if (piForPlanning != null && !string.Equals(piForPlanning.defaultActionMap, "Player", StringComparison.Ordinal))
                {
                    planned.Add("Set PlayerInput.defaultActionMap = 'Player'");
                    if (apply)
                    {
                        Undo.RecordObject(piForPlanning, "AI ensurePlayerInputStack");
                        piForPlanning.defaultActionMap = "Player";
                        EditorUtility.SetDirty(piForPlanning);
                        applied.Add("Set PlayerInput.defaultActionMap = 'Player'");
                        anyChanged = true;
                    }
                }

                if (piForPlanning != null && piForPlanning.notificationBehavior != UnityEngine.InputSystem.PlayerNotifications.InvokeCSharpEvents)
                {
                    planned.Add("Set PlayerInput.notificationBehavior = InvokeCSharpEvents");
                    if (apply)
                    {
                        Undo.RecordObject(piForPlanning, "AI ensurePlayerInputStack");
                        piForPlanning.notificationBehavior = UnityEngine.InputSystem.PlayerNotifications.InvokeCSharpEvents;
                        EditorUtility.SetDirty(piForPlanning);
                        applied.Add("Set PlayerInput.notificationBehavior = InvokeCSharpEvents");
                        anyChanged = true;
                    }
                }
            }

            // Ensure camera pan controller exists.
            if (ensureCameraPan)
            {
                var cam = Camera.main;
                if (cam == null)
                    cam = FindFirstObjectByType<Camera>();

                if (cam == null)
                {
                    AddWarning(result, opName, "No camera found to attach CameraPanController.");
                }
                else
                {
                    if (cam.GetComponent<CameraPanController>() == null)
                    {
                        planned.Add("Add CameraPanController to Main Camera");
                        if (apply)
                        {
                            Undo.AddComponent<CameraPanController>(cam.gameObject);
                            applied.Add("Added CameraPanController to Main Camera");
                            anyChanged = true;
                        }
                    }
                }
            }

            if (apply && anyChanged && !Application.isPlaying)
            {
                var scene = EditorSceneManager.GetActiveScene();
                if (scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(scene);
            }

            AddLog(result, opName, anyChanged ? "Changes ensured." : "No changes needed.");

            return new PlayerInputStackReport
            {
                pass = result == null || (result.errors == null || result.errors.Count == 0),
                playerPath = GetTransformPath(player.transform),
                inputActionsPath = inputActionsPath,
                planned = planned.ToArray(),
                applied = applied.ToArray()
            };
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static void EnsureAssetFoldersForPath(string assetPath, bool apply, List<string> planned, List<string> applied, ref bool anyChanged)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return;

            var dir = System.IO.Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(dir))
                return;

            if (AssetDatabase.IsValidFolder(dir))
                return;

            // Create recursively.
            var parts = dir.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            string cur = parts.Length > 0 ? parts[0] : "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                var next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    planned.Add($"Create folder {next}");
                    if (apply)
                    {
                        AssetDatabase.CreateFolder(cur, parts[i]);
                        applied.Add($"Created folder {next}");
                        anyChanged = true;
                    }
                }
                cur = next;
            }
        }

        private static InputActionAsset EnsureInputActionsAsset(string assetPath, bool apply, List<string> planned, List<string> applied, ref bool anyChanged)
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(assetPath);
            if (asset == null)
            {
                planned.Add($"Create InputActionAsset at {assetPath}");
                if (!apply)
                    return null;

                asset = ScriptableObject.CreateInstance<InputActionAsset>();
                AssetDatabase.CreateAsset(asset, assetPath);
                AssetDatabase.ImportAsset(assetPath);
                applied.Add($"Created InputActionAsset at {assetPath}");
                anyChanged = true;
            }

            var map = asset.FindActionMap("Player", false);
            if (map == null)
            {
                planned.Add("Add ActionMap 'Player'");
                if (apply)
                {
                    map = new InputActionMap("Player");
                    asset.AddActionMap(map);
                    anyChanged = true;
                    applied.Add("Added ActionMap 'Player'");
                }
                else
                    return asset;
            }

            EnsureAction(map, "CameraPan", InputActionType.Value, "Vector2", apply, planned, applied, ref anyChanged);
            EnsureAction(map, "Click", InputActionType.Button, "Button", apply, planned, applied, ref anyChanged);
            EnsureAction(map, "PointerPosition", InputActionType.Value, "Vector2", apply, planned, applied, ref anyChanged);
            EnsureAction(map, "AttackDebug", InputActionType.Button, "Button", apply, planned, applied, ref anyChanged);

            // Bindings
            var cameraPan = map.FindAction("CameraPan", false);
            if (cameraPan != null)
            {
                EnsureKeyboard2DVector(cameraPan, "WASD", "<Keyboard>/w", "<Keyboard>/s", "<Keyboard>/a", "<Keyboard>/d", apply, planned, applied, ref anyChanged);
                EnsureKeyboard2DVector(cameraPan, "Arrows", "<Keyboard>/upArrow", "<Keyboard>/downArrow", "<Keyboard>/leftArrow", "<Keyboard>/rightArrow", apply, planned, applied, ref anyChanged);
            }

            var click = map.FindAction("Click", false);
            if (click != null)
                EnsureBinding(click, "<Mouse>/leftButton", apply, planned, applied, ref anyChanged);

            var pointer = map.FindAction("PointerPosition", false);
            if (pointer != null)
                EnsureBinding(pointer, "<Pointer>/position", apply, planned, applied, ref anyChanged);

            var attackDebug = map.FindAction("AttackDebug", false);
            if (attackDebug != null)
                EnsureBinding(attackDebug, "<Keyboard>/space", apply, planned, applied, ref anyChanged);

            if (apply)
            {
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
            }

            return asset;
        }

        private static void EnsureAction(InputActionMap map, string actionName, InputActionType type, string expectedControlType, bool apply, List<string> planned, List<string> applied, ref bool anyChanged)
        {
            if (map == null) return;
            if (map.FindAction(actionName, false) != null) return;

            planned.Add($"Add action Player/{actionName} ({type})");
            if (apply)
            {
                map.AddAction(actionName, type, null, null, null, expectedControlType);
                applied.Add($"Added action Player/{actionName}");
                anyChanged = true;
            }
        }

        private static void EnsureBinding(InputAction action, string path, bool apply, List<string> planned, List<string> applied, ref bool anyChanged)
        {
            if (action == null || string.IsNullOrWhiteSpace(path)) return;
            for (int i = 0; i < action.bindings.Count; i++)
            {
                if (string.Equals(action.bindings[i].path, path, StringComparison.Ordinal))
                    return;
            }

            planned.Add($"Add binding {action.actionMap.name}/{action.name} -> {path}");
            if (apply)
            {
                action.AddBinding(path);
                applied.Add($"Added binding {action.actionMap.name}/{action.name} -> {path}");
                anyChanged = true;
            }
        }

        private static void EnsureKeyboard2DVector(InputAction action, string label, string up, string down, string left, string right, bool apply, List<string> planned, List<string> applied, ref bool anyChanged)
        {
            if (action == null) return;
            bool hasUp = HasBindingPath(action, up);
            bool hasDown = HasBindingPath(action, down);
            bool hasLeft = HasBindingPath(action, left);
            bool hasRight = HasBindingPath(action, right);

            if (hasUp && hasDown && hasLeft && hasRight)
                return;

            planned.Add($"Ensure CameraPan has {label} bindings");
            if (!apply)
                return;

            anyChanged = true;
            var composite = action.AddCompositeBinding("2DVector");
            composite.With("Up", up);
            composite.With("Down", down);
            composite.With("Left", left);
            composite.With("Right", right);
            applied.Add($"Added CameraPan {label} 2DVector composite");
        }

        private static bool HasBindingPath(InputAction action, string path)
        {
            if (action == null) return false;
            for (int i = 0; i < action.bindings.Count; i++)
            {
                if (string.Equals(action.bindings[i].path, path, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static GameObject FindPlayerForInputStack(string playerTag)
        {
            // Prefer tag when possible.
            if (!string.IsNullOrWhiteSpace(playerTag))
            {
                try
                {
                    var byTag = GameObject.FindWithTag(playerTag);
                    if (byTag != null)
                        return byTag;
                }
                catch { }
            }

            // Fallback: PlayerInventory.
            try
            {
                var inv = PlayerInventoryResolver.GetOrFind();
                if (inv != null)
                    return inv.gameObject;
            }
            catch { }

            return null;
        }

        private static void RemoveIfPresent(GameObject go, string componentTypeName, bool apply, List<string> planned, List<string> applied, ref bool anyChanged)
        {
            if (go == null || string.IsNullOrWhiteSpace(componentTypeName))
                return;

            var comp = go.GetComponent(componentTypeName) as Component;
            if (comp == null)
                return;

            planned.Add($"Remove {componentTypeName} from {go.name}");
            if (apply)
            {
                Undo.DestroyObjectImmediate(comp);
                applied.Add($"Removed {componentTypeName} from {go.name}");
                anyChanged = true;
            }
        }

        private static T EnsureComponent<T>(GameObject go, bool apply, List<string> planned, List<string> applied, ref bool anyChanged) where T : Component
        {
            if (go == null) return null;

            var existing = go.GetComponent<T>();
            if (existing != null)
                return existing;

            planned.Add($"Add {typeof(T).Name} to {go.name}");
            if (apply)
            {
                var added = Undo.AddComponent<T>(go);
                applied.Add($"Added {typeof(T).Name} to {go.name}");
                anyChanged = true;
                return added;
            }

            return null;
        }

        private static Component EnsureComponentByName(AiExecutionResult result, GameObject go, string typeName, bool apply, List<string> planned, List<string> applied, ref bool anyChanged)
        {
            if (go == null || string.IsNullOrWhiteSpace(typeName))
                return null;

            var existing = go.GetComponent(typeName) as Component;
            if (existing != null)
                return existing;

            planned.Add($"Add {typeName} to {go.name}");
            if (!apply)
                return null;

            var t = FindTypeByName(typeName);
            if (t == null)
            {
                AddError(result, "ensurePlayerInputStack", $"Could not resolve type '{typeName}' to add to '{go.name}'.");
                return null;
            }

            var added = Undo.AddComponent(go, t);
            applied.Add($"Added {typeName} to {go.name}");
            anyChanged = true;
            return added;
        }

        private static Type FindTypeByName(string typeName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                var asm = assemblies[i];
                Type[] types;
                try { types = asm.GetTypes(); }
                catch { continue; }

                for (int j = 0; j < types.Length; j++)
                {
                    var t = types[j];
                    if (t == null) continue;
                    if (string.Equals(t.Name, typeName, StringComparison.Ordinal))
                        return t;
                }
            }
            return null;
        }
#endif

        private static FoundationReport ValidateFoundationInternal(AiExecutionResult result)
        {
            const string opName = "validateFoundation";
            var report = new FoundationReport { pass = true };

            var player = FindPlayer();
            report.playerPath = player != null ? GetTransformPath(player.transform) : null;
            if (player == null)
            {
                report.pass = false;
                AddError(result, opName, "Missing Player tagged 'Player'.");
            }

            var cam = Camera.main;
            if (cam == null)
            {
                // fallback: any camera
                cam = FindFirstObjectByType<Camera>();
            }
            report.cameraPath = cam != null ? GetTransformPath(cam.transform) : null;
            if (cam == null)
            {
                report.pass = false;
                AddError(result, opName, "Missing Main Camera.");
            }
            else
            {
                var follow = cam.GetComponent<TopDownFollowCamera>();
                report.cameraHasTopDownFollow = follow != null;
                if (follow == null)
                {
                    report.pass = false;
                    AddError(result, opName, "Main Camera missing TopDownFollowCamera.");
                }
                else
                {
                    var target = follow.GetTarget();
                    report.cameraHasTarget = target != null;
                    if (target == null)
                    {
                        report.pass = false;
                        AddError(result, opName, "TopDownFollowCamera has no target.");
                    }
                    else if (player != null && !ReferenceEquals(target, player.transform))
                    {
                        AddWarning(result, opName, $"TopDownFollowCamera target is '{target.name}', expected Player '{player.name}'.");
                    }
                }
            }

            var canvas = FindFirstObjectByType<Canvas>();
            report.hasCanvas = canvas != null;
            if (canvas == null)
            {
                report.pass = false;
                AddError(result, opName, "Missing Canvas.");
            }

            var es = FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            report.hasEventSystem = es != null;
            if (es == null)
            {
                report.pass = false;
                AddError(result, opName, "Missing EventSystem.");
            }

            if (player != null)
            {
                report.hasPlayerHealth = player.GetComponent<PlayerHealth>() != null;
                report.hasSimplePlayerCombat = player.GetComponent<SimplePlayerCombat>() != null;
                if (!report.hasPlayerHealth)
                {
                    report.pass = false;
                    AddError(result, opName, "Player missing PlayerHealth.");
                }
                if (!report.hasSimplePlayerCombat)
                {
                    report.pass = false;
                    AddError(result, opName, "Player missing SimplePlayerCombat.");
                }
            }

            var bootstrapper = FindFirstObjectByTypeInOpenScenes("GameBootstrapper");
            report.hasBootstrapper = bootstrapper != null || GameObject.Find("_GameBootstrapper") != null;

            return report;
        }

        private static void RunRecipeInternal(AiExecutionResult result, ExecutionMode mode, AiCommand cmd)
        {
            const string opName = "runRecipe";
            var recipeName = cmd?.recipeName;
            if (string.IsNullOrWhiteSpace(recipeName))
            {
                AddError(result, opName, "recipeName is required.");
                return;
            }

            string[] ops;
            if (cmd.recipeOps != null && cmd.recipeOps.Length > 0)
            {
                ops = cmd.recipeOps;
                AddLog(result, opName, $"Using provided recipeOps override (count={ops.Length}).");
            }
            else
            {
                ops = GetBuiltInRecipeOps(recipeName);
                if (ops == null || ops.Length == 0)
                {
                    AddError(result, opName, $"Unknown recipe '{recipeName}'.");
                    return;
                }
                AddLog(result, opName, $"Running built-in recipe '{recipeName}' (steps={ops.Length}).");
            }

            for (int i = 0; i < ops.Length; i++)
            {
                var op = ops[i];
                if (string.IsNullOrWhiteSpace(op))
                    continue;

                AddLog(result, opName, $"Step {i + 1}/{ops.Length}: {op}");

                // Execute by dispatching directly to internal handlers.
                switch (op)
                {
                    case "ensureSceneRoots":
                        EnsureSceneRootsInternal(result, mode);
                        break;
                    case "validateSceneRoots":
                        ValidateSceneRootsInternal(result);
                        break;
                    case "ensureFoundation":
                        EnsureFoundationInternal(result, mode);
                        break;
                    case "validateFoundation":
                        ValidateFoundationInternal(result);
                        break;
                    case "validateNoScaledParents":
                        ValidateNoScaledParentsInternal(result);
                        break;
                    default:
                        AddWarning(result, opName, $"Unknown recipe op '{op}' skipped.");
                        break;
                }
            }
        }

        private static string[] GetBuiltInRecipeOps(string recipeName)
        {
            if (string.Equals(recipeName, "zone1_foundation_validate", StringComparison.OrdinalIgnoreCase))
            {
                return new[]
                {
                    "ensureSceneRoots",
                    "validateSceneRoots",
                    "ensureFoundation",
                    "validateFoundation",
                    "validateNoScaledParents"
                };
            }
            return null;
        }

        private static string GetZoneRootName(string sceneName)
        {
            if (!string.IsNullOrWhiteSpace(sceneName) && sceneName.IndexOf("Zone1", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Zone1";
            return string.IsNullOrWhiteSpace(sceneName) ? "Scene" : sceneName;
        }

        private static Transform EnsureChild(Transform parent, string childName, ExecutionMode mode, List<string> created)
        {
            if (parent == null || string.IsNullOrWhiteSpace(childName))
                return null;

            var existing = FindChild(parent, childName);
            if (existing != null)
                return existing;

            if (mode != ExecutionMode.Apply)
                return null;

            var go = new GameObject(childName);
            UnityEditor.Undo.RegisterCreatedObjectUndo(go, "AI ensureSceneRoots");
            go.transform.SetParent(parent, false);
            created?.Add(GetTransformPath(go.transform));
            return go.transform;
        }

        private static Transform FindChild(Transform parent, string childName)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c != null && string.Equals(c.name, childName, StringComparison.Ordinal))
                    return c;
            }
            return null;
        }

        private static void ValidateChild(Transform parent, string childName, List<string> missing)
        {
            var t = FindChild(parent, childName);
            if (t == null)
                missing?.Add($"{GetTransformPath(parent)}/{childName}");
        }

        private static string GetTransformPath(Transform t)
        {
            if (t == null) return "<null>";
            var parts = new List<string>();
            while (t != null)
            {
                parts.Add(t.name);
                t = t.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static bool ApproximatelyOne(Vector3 v)
        {
            return Mathf.Abs(v.x - 1f) <= 0.0001f && Mathf.Abs(v.y - 1f) <= 0.0001f && Mathf.Abs(v.z - 1f) <= 0.0001f;
        }

        private static T FindFirstObjectByType<T>() where T : UnityEngine.Object
        {
            // Unity API surface differs across versions; keep a local helper.
            // This finds scene objects (including inactive). In Editor, this can include DontDestroyOnLoad objects too.
            var all = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (all == null || all.Length == 0)
                return null;
            return all[0];
        }

        private static bool HasAnyComponentByName(GameObject go, params string[] typeNames)
        {
            if (go == null || typeNames == null || typeNames.Length == 0) return false;

            // include children; some scripts are attached below the root
            var monos = go.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < monos.Length; i++)
            {
                var m = monos[i];
                if (m == null) continue;
                var n = m.GetType().Name;
                for (int j = 0; j < typeNames.Length; j++)
                {
                    if (string.Equals(n, typeNames[j], StringComparison.Ordinal))
                        return true;
                }
            }
            return false;
        }

        private static bool NameContainsAny(string name, params string[] tokens)
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

        private static GameObject FindPlayer()
        {
            try
            {
                return GameObject.FindWithTag("Player");
            }
            catch
            {
                // Tag may not exist.
                return null;
            }
        }

        private static GameObject FindFirstObjectByTypeInOpenScenes(string componentTypeName)
        {
            if (string.IsNullOrWhiteSpace(componentTypeName))
                return null;

            var monos = GameObject.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < monos.Length; i++)
            {
                var m = monos[i];
                if (m == null) continue;
                if (!m.gameObject.scene.IsValid()) continue;
                if (string.Equals(m.gameObject.scene.name, "DontDestroyOnLoad", StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(m.GetType().Name, componentTypeName, StringComparison.Ordinal))
                    return m.gameObject;
            }
            return null;
        }

        private static void MinimalEnsureFoundationInEditor(List<string> notes)
        {
            // This is a fallback path; prefer GameBootstrapper.
            var player = FindPlayer();
            if (player == null)
            {
                var go = new GameObject("Player");
                UnityEditor.Undo.RegisterCreatedObjectUndo(go, "AI ensureFoundation");
                try { go.tag = "Player"; } catch { }
                player = go;
                notes?.Add("Created Player (tagged 'Player')");
            }
            else
            {
                notes?.Add("Found Player (tagged 'Player')");
            }

            if (player.GetComponent<PlayerHealth>() == null)
            {
                UnityEditor.Undo.AddComponent<PlayerHealth>(player);
                notes?.Add("Added PlayerHealth to Player");
            }
            if (player.GetComponent<SimplePlayerCombat>() == null)
            {
                UnityEditor.Undo.AddComponent<SimplePlayerCombat>(player);
                notes?.Add("Added SimplePlayerCombat to Player");
            }

            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                UnityEditor.Undo.RegisterCreatedObjectUndo(camGo, "AI ensureFoundation");
                cam = camGo.AddComponent<Camera>();
                camGo.tag = "MainCamera";
                notes?.Add("Created Main Camera");
            }
            if (cam.GetComponent<TopDownFollowCamera>() == null)
            {
                UnityEditor.Undo.AddComponent<TopDownFollowCamera>(cam.gameObject);
                notes?.Add("Added TopDownFollowCamera to Main Camera");
            }

            // Ensure a canvas.
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasGo = new GameObject("HUDCanvas");
                UnityEditor.Undo.RegisterCreatedObjectUndo(canvasGo, "AI ensureFoundation");
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var _scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
                _scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                _scaler.referenceResolution = new UnityEngine.Vector2(1920f, 1080f);
                _scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                _scaler.matchWidthOrHeight = 0.5f;
                canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                notes?.Add("Created HUDCanvas");
            }

            var es = FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (es == null)
            {
                var esGo = new GameObject("EventSystem");
                UnityEditor.Undo.RegisterCreatedObjectUndo(esGo, "AI ensureFoundation");
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
#if ENABLE_INPUT_SYSTEM
                esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
                notes?.Add("Created EventSystem");
            }

            // Wire camera target.
            var follow = cam.GetComponent<TopDownFollowCamera>();
            if (follow != null)
                follow.SetTarget(player.transform);
        }
#endif

#if UNITY_EDITOR
        private static List<T> DiscoverAssetsByType<T>(string unityTypeFilter) where T : UnityEngine.Object
        {
            var guids = UnityEditor.AssetDatabase.FindAssets(unityTypeFilter);
            var found = new List<T>();
            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                    found.Add(asset);
            }
            return found;
        }

        private static List<UnityEngine.ScriptableObject> DiscoverAssetsByFilterAsScriptableObject(string unityTypeFilter)
        {
            var guids = UnityEditor.AssetDatabase.FindAssets(unityTypeFilter);
            var found = new List<UnityEngine.ScriptableObject>();
            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.ScriptableObject>(path);
                if (asset != null)
                    found.Add(asset);
            }
            return found;
        }

        private static string GetAssetPath(UnityEngine.Object asset)
        {
            if (asset == null) return null;
            return UnityEditor.AssetDatabase.GetAssetPath(asset);
        }

        private static List<UnityEngine.ScriptableObject> DiscoverAllScriptableObjects()
        {
            return DiscoverAssetsByType<UnityEngine.ScriptableObject>("t:ScriptableObject");
        }

        private static List<UnityEngine.ScriptableObject> DiscoverScriptableObjectCandidatesByTypeName(Func<string, bool> typeNamePredicate)
        {
            var all = DiscoverAllScriptableObjects();
            var candidates = new List<UnityEngine.ScriptableObject>();
            foreach (var so in all)
            {
                if (so == null) continue;
                var typeName = so.GetType().Name;
                if (typeNamePredicate != null && typeNamePredicate(typeName))
                    candidates.Add(so);
            }
            return candidates;
        }

        private static void LogCandidateNamesAndTypes(AiExecutionResult result, string op, string label, List<UnityEngine.ScriptableObject> candidates, int maxToLog = 50)
        {
            if (candidates == null || candidates.Count == 0)
            {
                AddLog(result, op, $"{label}: (none)");
                return;
            }

            int count = Math.Min(maxToLog, candidates.Count);
            AddLog(result, op, $"{label}: {candidates.Count} candidate(s) (showing {count})");
            for (int i = 0; i < count; i++)
            {
                var so = candidates[i];
                if (so == null)
                {
                    AddLog(result, op, " - <null>");
                    continue;
                }
                AddLog(result, op, $" - {so.name} | {so.GetType().FullName}");
            }
        }

        private static void LogAssetsWithTypeAndPath(AiExecutionResult result, string op, string label, List<UnityEngine.ScriptableObject> assets, int maxToLog = 50)
        {
            if (assets == null || assets.Count == 0)
            {
                AddLog(result, op, $"{label}: (none)");
                return;
            }

            int count = Math.Min(maxToLog, assets.Count);
            AddLog(result, op, $"{label}: {assets.Count} asset(s) (showing {count})");
            for (int i = 0; i < count; i++)
            {
                var a = assets[i];
                if (a == null)
                {
                    AddLog(result, op, " - <null>");
                    continue;
                }
                AddLog(result, op, $" - {a.name} | {a.GetType().FullName} | {GetAssetPath(a)}");
            }
        }

        private static List<DropTable> DiscoverDropTablesWithFallback(AiExecutionResult result, string op, out List<UnityEngine.ScriptableObject> fallbackCandidates)
        {
            fallbackCandidates = null;
            var found = DiscoverAssetsByType<DropTable>("t:DropTable");
            if (found.Count > 0)
                return found;

            fallbackCandidates = DiscoverScriptableObjectCandidatesByTypeName(typeName =>
                typeName != null && typeName.IndexOf("DropTable", StringComparison.OrdinalIgnoreCase) >= 0
            );
            LogCandidateNamesAndTypes(result, op, "DropTable fallback candidates", fallbackCandidates);

            var casted = new List<DropTable>();
            foreach (var so in fallbackCandidates)
            {
                if (so is DropTable dt)
                    casted.Add(dt);
            }
            return casted;
        }

        private static List<UnityEngine.ScriptableObject> DiscoverItemDefinitionsWithFallback(AiExecutionResult result, string op)
        {
            var exact = DiscoverAssetsByFilterAsScriptableObject("t:LegacyItemDefinition");
            if (exact.Count > 0) return exact;

            var candidates = DiscoverScriptableObjectCandidatesByTypeName(typeName =>
            {
                if (string.IsNullOrEmpty(typeName)) return false;
                if (typeName.IndexOf("Item", StringComparison.OrdinalIgnoreCase) < 0) return false;
                return typeName.IndexOf("Definition", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       typeName.IndexOf("Def", StringComparison.OrdinalIgnoreCase) >= 0;
            });
            LogCandidateNamesAndTypes(result, op, "LegacyItemDefinition fallback candidates", candidates);
            return candidates;
        }

        private static List<UnityEngine.ScriptableObject> DiscoverEnemiesWithFallback(AiExecutionResult result, string op)
        {
            var exact = DiscoverAssetsByFilterAsScriptableObject("t:EnemyDefinition");
            if (exact.Count > 0)
            {
                LogAssetsWithTypeAndPath(result, op, "EnemyDefinition assets", exact);
                return exact;
            }

            var candidates = DiscoverScriptableObjectCandidatesByTypeName(typeName =>
            {
                if (string.IsNullOrEmpty(typeName)) return false;
                if (typeName.IndexOf("Enemy", StringComparison.OrdinalIgnoreCase) < 0) return false;
                return typeName.IndexOf("Definition", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       typeName.IndexOf("Config", StringComparison.OrdinalIgnoreCase) >= 0;
            });
            LogAssetsWithTypeAndPath(result, op, "Enemy fallback candidates", candidates);
            return candidates;
        }

        private static List<GateDefinition> DiscoverGateDefinitions()
        {
            return DiscoverAssetsByType<GateDefinition>("t:GateDefinition");
        }

        private static bool ItemDefinitionMatches(UnityEngine.ScriptableObject item, string token)
        {
            if (item == null) return false;
            if (string.IsNullOrWhiteSpace(token)) return false;

            var itemId = TryGetStringMember(item, "itemId");
            if (!string.IsNullOrWhiteSpace(itemId) && string.Equals(itemId, token, StringComparison.OrdinalIgnoreCase))
                return true;

            return string.Equals(item.name, token, StringComparison.OrdinalIgnoreCase);
        }

        private static UnityEngine.ScriptableObject FindItemDefinitionByIdOrName(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;
            var items = DiscoverAssetsByFilterAsScriptableObject("t:LegacyItemDefinition");
            foreach (var item in items)
            {
                if (ItemDefinitionMatches(item, token))
                    return item;
            }
            return null;
        }

        private static string[] GetAssetNames<T>(List<T> assets) where T : UnityEngine.Object
        {
            if (assets == null || assets.Count == 0) return Array.Empty<string>();
            var names = new string[assets.Count];
            for (int i = 0; i < assets.Count; i++)
                names[i] = assets[i] != null ? assets[i].name : "<null>";
            return names;
        }

        private static bool IsProvidedMinDropChance(float minDropChance)
        {
            return minDropChance > 0.000001f;
        }

        private static Dictionary<string, List<DropEntry>> GetTierLists(DropTable table)
        {
            return new Dictionary<string, List<DropEntry>>
            {
                {"Trash", table != null ? table.trashDrops : null},
                {"Normal", table != null ? table.normalDrops : null},
                {"Elite", table != null ? table.eliteDrops : null},
                {"MiniBoss", table != null ? table.miniBossDrops : null}
            };
        }

        private static HashSet<UnityEngine.ScriptableObject> CollectReferencedItemDefinitions(DropTable table)
        {
            var referenced = new HashSet<UnityEngine.ScriptableObject>();
            if (table == null) return referenced;
            var tiers = GetTierLists(table);
            foreach (var kvp in tiers)
            {
                var drops = kvp.Value;
                if (drops == null) continue;
                foreach (var entry in drops)
                {
                    if (entry == null || entry.item == null) continue;
                    referenced.Add(entry.item);
                }
            }
            return referenced;
        }

        private static void ValidateDropTableNonMutating(AiExecutionResult result, string opName, DropTable table, string[] expectedItems, float minDropChance)
        {
            if (table == null)
            {
                AddError(result, opName, "Null DropTable.");
                return;
            }

            var tierLists = GetTierLists(table);

            float maxTierTotal = 0f;
            bool anyNonZeroTierTotal = false;
            foreach (var kvp in tierLists)
            {
                var tier = kvp.Key;
                var drops = kvp.Value;
                float total = 0f;
                int entryCount = 0;
                if (drops != null)
                {
                    foreach (var entry in drops)
                    {
                        if (entry == null) continue;
                        entryCount++;
                        total += entry.dropChance;
                    }
                }
                if (total > 0f) anyNonZeroTierTotal = true;
                if (total > maxTierTotal) maxTierTotal = total;
                AddLog(result, opName, $"{table.name} | Tier '{tier}' entries={entryCount} totalChance={total:0.###}");
            }

            if (!anyNonZeroTierTotal)
                AddWarning(result, opName, $"{table.name} | All tiers have totalChance == 0 (table effectively empty)");

            AddLog(result, opName, $"{table.name} | Max possible drop chance (max tier total) = {maxTierTotal:0.###}");
            if (IsProvidedMinDropChance(minDropChance) && maxTierTotal < minDropChance)
                AddWarning(result, opName, $"{table.name} | Max possible drop chance {maxTierTotal:0.###} is below minDropChance {minDropChance:0.###}");

            if (expectedItems == null || expectedItems.Length == 0)
                return;

            var foundItemDefs = DiscoverItemDefinitionsWithFallback(result, opName);
            var itemByName = new Dictionary<string, UnityEngine.ScriptableObject>(StringComparer.OrdinalIgnoreCase);
            foreach (var itemDef in foundItemDefs)
            {
                if (itemDef == null) continue;
                if (!itemByName.ContainsKey(itemDef.name))
                    itemByName.Add(itemDef.name, itemDef);
            }

            foreach (var itemName in expectedItems)
            {
                if (string.IsNullOrWhiteSpace(itemName))
                {
                    AddWarning(result, opName, $"{table.name} | Encountered empty expectedItems entry; skipped.");
                    continue;
                }

                if (!itemByName.TryGetValue(itemName, out var itemDefAsset) || itemDefAsset == null)
                {
                    AddError(result, opName, $"{table.name} | Expected item '{itemName}' is missing LegacyItemDefinition asset (matched by asset.name)");
                    continue;
                }

                var itemId = TryGetStringMember(itemDefAsset, "itemId");
                if (!string.IsNullOrEmpty(itemId) && !string.Equals(itemId, itemDefAsset.name, StringComparison.OrdinalIgnoreCase))
                    AddWarning(result, opName, $"{table.name} | LegacyItemDefinition '{itemDefAsset.name}' itemId '{itemId}' does not match asset name");

                float maxChance = 0f;
                var perTier = new List<string>();
                bool foundInTable = false;
                foreach (var kvp in tierLists)
                {
                    var tier = kvp.Key;
                    var drops = kvp.Value;
                    if (drops == null) continue;
                    foreach (var entry in drops)
                    {
                        if (entry != null && entry.item != null && string.Equals(entry.item.name, itemName, StringComparison.OrdinalIgnoreCase))
                        {
                            foundInTable = true;
                            if (entry.dropChance > maxChance) maxChance = entry.dropChance;
                            perTier.Add($"{tier}: {entry.dropChance:0.###}");
                        }
                    }
                }

                if (!foundInTable)
                {
                    AddError(result, opName, $"{table.name} | Expected item '{itemName}' not referenced by any tier entry");
                    continue;
                }

                if (IsProvidedMinDropChance(minDropChance) && maxChance < minDropChance)
                    AddWarning(result, opName, $"{table.name} | '{itemName}' max entry chance {maxChance:0.###} < minDropChance {minDropChance:0.###}");

                AddLog(result, opName, $"{table.name} | '{itemName}' | {string.Join(", ", perTier)} | maxEntryChance={maxChance:0.###}");
            }
        }

        private class DropTableRef
        {
            public DropTable table;
            public string id;
            public string source;
        }

#if UNITY_EDITOR
        private class EnemyPrefabCandidate
        {
            public GameObject prefab;
            public string path;
            public List<string> matchingComponentTypeFullNames;
        }

        private static List<EnemyPrefabCandidate> DiscoverEnemyPrefabCandidates(AiExecutionResult result, string op)
        {
            var candidates = new List<EnemyPrefabCandidate>();
            var guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab");
            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                bool nameMatch = prefab.name.IndexOf("Enemy", StringComparison.OrdinalIgnoreCase) >= 0;
                var matchingComponentTypes = new HashSet<string>(StringComparer.Ordinal);
                try
                {
                    var comps = prefab.GetComponentsInChildren<Component>(true);
                    if (comps != null)
                    {
                        foreach (var c in comps)
                        {
                            if (c == null) continue;
                            var t = c.GetType();
                            var typeName = t.Name;
                            if (!NameContainsAny(typeName, "Enemy", "Mob", "Monster"))
                                continue;
                            matchingComponentTypes.Add(t.FullName ?? t.Name);
                        }
                    }
                }
                catch (Exception ex)
                {
                    AddWarning(result, op, $"Failed to inspect components for prefab '{prefab.name}' at '{path}': {ex.GetType().Name}");
                }

                if (!nameMatch && matchingComponentTypes.Count == 0)
                    continue;

                candidates.Add(new EnemyPrefabCandidate
                {
                    prefab = prefab,
                    path = path,
                    matchingComponentTypeFullNames = new List<string>(matchingComponentTypes)
                });
            }

            return candidates;
        }

        private static List<DropTableRef> ExtractDropTableRefsFromComponent(Component component, HashSet<string> matchedMemberNames)
        {
            var refs = new List<DropTableRef>();
            if (component == null) return refs;

            void HandleValue(object value, string source)
            {
                if (value == null) return;

                if (value is DropTable dt)
                {
                    refs.Add(new DropTableRef { table = dt, id = null, source = source });
                    return;
                }

                if (value is string s)
                {
                    if (!string.IsNullOrWhiteSpace(s))
                        refs.Add(new DropTableRef { table = null, id = s, source = source });
                    return;
                }

                if (value is System.Collections.IEnumerable enumerable)
                {
                    foreach (var element in enumerable)
                        HandleValue(element, source);
                }
            }

            var t = component.GetType();
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            foreach (var f in t.GetFields(flags))
            {
                if (!NameContainsAny(f.Name, "dropTable", "dropTables", "loot")) continue;
                matchedMemberNames?.Add($"{t.FullName}.{f.Name}");
                try { HandleValue(f.GetValue(component), $"{t.FullName}.field:{f.Name}"); } catch { }
            }

            foreach (var p in t.GetProperties(flags))
            {
                if (!p.CanRead) continue;
                if (p.GetIndexParameters().Length > 0) continue;
                if (!NameContainsAny(p.Name, "dropTable", "dropTables", "loot")) continue;
                matchedMemberNames?.Add($"{t.FullName}.{p.Name}");
                try { HandleValue(p.GetValue(component), $"{t.FullName}.prop:{p.Name}"); } catch { }
            }

            return refs;
        }
#endif

        private static List<DropTableRef> ExtractDropTableRefsFromEnemy(UnityEngine.ScriptableObject enemy)
        {
            var refs = new List<DropTableRef>();
            if (enemy == null) return refs;

            void HandleValue(object value, string source)
            {
                if (value == null) return;

                if (value is DropTable dt)
                {
                    refs.Add(new DropTableRef { table = dt, id = null, source = source });
                    return;
                }

                if (value is string s)
                {
                    if (!string.IsNullOrWhiteSpace(s))
                        refs.Add(new DropTableRef { table = null, id = s, source = source });
                    return;
                }

                if (value is System.Collections.IEnumerable enumerable)
                {
                    foreach (var element in enumerable)
                        HandleValue(element, source);
                }
            }

            var t = enemy.GetType();
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            foreach (var f in t.GetFields(flags))
            {
                if (!NameContainsAny(f.Name, "dropTable", "dropTables", "loot")) continue;
                try { HandleValue(f.GetValue(enemy), $"field:{f.Name}"); } catch { }
            }

            foreach (var p in t.GetProperties(flags))
            {
                if (!p.CanRead) continue;
                if (p.GetIndexParameters().Length > 0) continue;
                if (!NameContainsAny(p.Name, "dropTable", "dropTables", "loot")) continue;
                try { HandleValue(p.GetValue(enemy), $"prop:{p.Name}"); } catch { }
            }

            return refs;
        }
#endif

        private static string TryGetStringMember(object obj, string memberName)
        {
            if (obj == null || string.IsNullOrEmpty(memberName)) return null;
            var t = obj.GetType();

            var field = t.GetField(memberName, BindingFlags.Public | BindingFlags.Instance);
            if (field != null && field.FieldType == typeof(string))
                return field.GetValue(obj) as string;

            var prop = t.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.PropertyType == typeof(string) && prop.CanRead)
                return prop.GetValue(obj) as string;

            return null;
        }

        private static void ValidateCommandSchemaForList(AiExecutionResult result, AiCommandList list)
        {
            const string opName = "validateCommandSchema";

            if (list == null || list.commands == null)
            {
                AddError(result, opName, "Null command list.");
                return;
            }

            for (int i = 0; i < list.commands.Length; i++)
            {
                var cmd = list.commands[i];
                if (cmd == null)
                {
                    AddWarning(result, opName, $"Command[{i}] is null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(cmd.op))
                {
                    AddError(result, opName, $"Command[{i}] missing required field 'op'.");
                    continue;
                }

                var requiredMissing = new List<string>();
                var allowedFields = new HashSet<string>(StringComparer.Ordinal) { "op" };

                switch (cmd.op)
                {
                    case "ping":
                    case "listDropTables":
                    case "listItemDefinitions":
                    case "listGates":
                    case "listScriptableObjectTypes":
                    case "listEnemies":
                    case "listEnemyPrefabs":
                    case "validateCommandSchema":
                    case "validateAllDropTables":
                    case "validateOrphanItemDefinitions":
                    case "ensureSceneRoots":
                    case "validateSceneRoots":
                    case "validateNoScaledParents":
                    case "ensureFoundation":
                    case "validateFoundation":
                        break;

                    case "ensurePlayerInputStack":
                        allowedFields.Add("playerTag");
                        allowedFields.Add("ensureCameraPan");
                        break;

                    case "runRecipe":
                        allowedFields.Add("recipeName");
                        allowedFields.Add("recipeOps");
                        if (string.IsNullOrWhiteSpace(cmd.recipeName)) requiredMissing.Add("recipeName");
                        break;

                    case "validateDropTable":
                        allowedFields.Add("dropTableId");
                        allowedFields.Add("expectedItems");
                        allowedFields.Add("minDropChance");
                        if (string.IsNullOrWhiteSpace(cmd.dropTableId)) requiredMissing.Add("dropTableId");
                        break;

                    case "validateEnemyDrops":
                        allowedFields.Add("enemyId");
                        allowedFields.Add("dropTableId");
                        allowedFields.Add("expectedItems");
                        allowedFields.Add("minDropChance");
                        break;

                    case "validateEnemyPrefabDrops":
                        allowedFields.Add("prefabId");
                        allowedFields.Add("dropTableId");
                        allowedFields.Add("expectedItems");
                        allowedFields.Add("minDropChance");
                        break;

                    case "validateGate":
                        allowedFields.Add("gateId");
                        allowedFields.Add("expectedKeyItem");
                        allowedFields.Add("requiredAmount");
                        if (string.IsNullOrWhiteSpace(cmd.gateId)) requiredMissing.Add("gateId");
                        if (string.IsNullOrWhiteSpace(cmd.expectedKeyItem)) requiredMissing.Add("expectedKeyItem");
                        if (cmd.requiredAmount <= 0) requiredMissing.Add("requiredAmount");
                        break;

                    default:
                        AddError(result, opName, $"Command[{i}] unknown op '{cmd.op}'.");
                        continue;
                }

                var setButUnused = new List<string>();
                if (!allowedFields.Contains("name") && !string.IsNullOrWhiteSpace(cmd.name)) setButUnused.Add("name");
                if (!allowedFields.Contains("path") && !string.IsNullOrWhiteSpace(cmd.path)) setButUnused.Add("path");
                if (!allowedFields.Contains("parentPath") && !string.IsNullOrWhiteSpace(cmd.parentPath)) setButUnused.Add("parentPath");
                if (!allowedFields.Contains("enemyId") && !string.IsNullOrWhiteSpace(cmd.enemyId)) setButUnused.Add("enemyId");
                if (!allowedFields.Contains("prefabId") && !string.IsNullOrWhiteSpace(cmd.prefabId)) setButUnused.Add("prefabId");
                if (!allowedFields.Contains("dropTableId") && !string.IsNullOrWhiteSpace(cmd.dropTableId)) setButUnused.Add("dropTableId");
                if (!allowedFields.Contains("expectedItems") && cmd.expectedItems != null && cmd.expectedItems.Length > 0) setButUnused.Add("expectedItems");
                if (!allowedFields.Contains("minDropChance") && Math.Abs(cmd.minDropChance) > 0.000001f) setButUnused.Add("minDropChance");
                if (!allowedFields.Contains("gateId") && !string.IsNullOrWhiteSpace(cmd.gateId)) setButUnused.Add("gateId");
                if (!allowedFields.Contains("expectedKeyItem") && !string.IsNullOrWhiteSpace(cmd.expectedKeyItem)) setButUnused.Add("expectedKeyItem");
                if (!allowedFields.Contains("requiredAmount") && cmd.requiredAmount != 0) setButUnused.Add("requiredAmount");
                if (!allowedFields.Contains("recipeName") && !string.IsNullOrWhiteSpace(cmd.recipeName)) setButUnused.Add("recipeName");
                if (!allowedFields.Contains("recipeOps") && cmd.recipeOps != null && cmd.recipeOps.Length > 0) setButUnused.Add("recipeOps");

                if (!allowedFields.Contains("playerTag") && !string.IsNullOrWhiteSpace(cmd.playerTag)) setButUnused.Add("playerTag");
                if (!allowedFields.Contains("ensureCameraPan") && cmd.ensureCameraPan != true) setButUnused.Add("ensureCameraPan");

                if (requiredMissing.Count > 0)
                    AddError(result, opName, $"Command[{i}] ({cmd.op}) missing required: {string.Join(", ", requiredMissing)}");
                else
                    AddLog(result, opName, $"Command[{i}] ({cmd.op}) required fields OK.");

                if (setButUnused.Count > 0)
                    AddWarning(result, opName, $"Command[{i}] ({cmd.op}) contains unused fields: {string.Join(", ", setButUnused)}");

                if ((cmd.op == "validateDropTable" || cmd.op == "validateEnemyDrops" || cmd.op == "validateEnemyPrefabDrops") && cmd.expectedItems != null)
                {
                    for (int j = 0; j < cmd.expectedItems.Length; j++)
                    {
                        if (string.IsNullOrWhiteSpace(cmd.expectedItems[j]))
                            AddWarning(result, opName, $"Command[{i}] expectedItems[{j}] is empty.");
                    }
                    if (cmd.minDropChance < 0f || cmd.minDropChance > 1f)
                        AddWarning(result, opName, $"Command[{i}] minDropChance {cmd.minDropChance:0.###} is outside [0,1].");
                }
            }
        }

        public static AiExecutionResult ExecuteCommands(AiCommandList list, ExecutionMode mode = ExecutionMode.DryRun)
        {
            // Example JSON:
            // {
            //   "commands": [ { "op": "listEnemyPrefabs" } ]
            // }
            //
            // {
            //   "commands": [ { "op": "validateEnemyPrefabDrops" } ]
            // }
            //
            // {
            //   "commands": [ { "op": "validateEnemyPrefabDrops", "prefabId": "Enemy_Goblin" } ]
            // }
            //
            // {
            //   "commands": [
            //     {
            //       "op": "validateEnemyPrefabDrops",
            //       "dropTableId": "Drops_Zone1_Sigil",
            //       "expectedItems": ["Item_AbyssalSigil"],
            //       "minDropChance": 0.1
            //     }
            //   ]
            // }

            var result = new AiExecutionResult
            {
                mode = mode,
                opsPlanned = (list?.commands?.Length) ?? 0,
                opsExecuted = 0,
                success = true
            };

            // Log mode, command count, and scope if present
            string scopeMsg = "";
            if (list?.commands != null && list.commands.Length > 0)
            {
                var first = list.commands[0];
                var scope = first != null ? first.scope : null;
                if (scope != null)
                    scopeMsg = $" | allowedRoots: [{string.Join(",", scope.allowedRoots)}] deniedRoots: [{string.Join(",", scope.deniedRoots)}] maxOps: {scope.maxOperations}";
            }
            result.logs.Add($"[UnityTools] Mode: {mode} | Commands: {(list?.commands?.Length ?? 0)}{scopeMsg}");

            if (list?.commands == null)
            {
                AddError(result, "ExecuteCommands", "Null command list.");
                return result;
            }

            for (int i = 0; i < list.commands.Length; i++)
            {
                var cmd = list.commands[i];
                if (cmd == null || string.IsNullOrWhiteSpace(cmd.op))
                {
                    AddWarning(result, "ExecuteCommands", $"Command[{i}] missing op. Skipped.");
                    continue;
                }

                // --- Pre-execution scope safety check (preserve existing behavior) ---
                var pathsToCheck = new[] { cmd.parentPath, cmd.name, cmd.prefabId, cmd.enemyId, cmd.dropTableId, cmd.gateId, cmd.expectedKeyItem, cmd.path };
                bool denied = false;

                var deniedRoots = cmd.scope != null && cmd.scope.deniedRoots != null
                    ? cmd.scope.deniedRoots
                    : null;

                if (deniedRoots != null)
                {
                    for (int d = 0; d < deniedRoots.Count; d++)
                    {
                        var deniedRoot = deniedRoots[d];
                        foreach (var p in pathsToCheck)
                        {
                            if (!string.IsNullOrEmpty(deniedRoot) && !string.IsNullOrEmpty(p) && p.StartsWith(deniedRoot, StringComparison.OrdinalIgnoreCase))
                            {
                                AddError(result, "ExecuteCommands", $"Command[{i}] references denied root '{deniedRoot}' in field value '{p}'. Blocked.");
                                denied = true;
                                break;
                            }
                        }
                        if (denied) break;
                    }
                }

                if (denied)
                    continue;
                switch (cmd.op)
                {
                    case "ping":
                        AddLog(result, "ping", "ok");
                        result.opsExecuted++;
                        break;

                    case "ensureSceneRoots":
                    {
                        const string opName = "ensureSceneRoots";
#if UNITY_EDITOR
                        var report = EnsureSceneRootsInternal(result, mode);
                        AddLog(result, opName, JsonUtility.ToJson(report));
#else
                        AddError(result, opName, "Only supported in Unity Editor.");
#endif
                        result.opsExecuted++;
                        break;
                    }

                    case "validateSceneRoots":
                    {
                        const string opName = "validateSceneRoots";
#if UNITY_EDITOR
                        var report = ValidateSceneRootsInternal(result);
                        if (!report.pass)
                            AddError(result, opName, $"Missing: [{string.Join(", ", report.missing ?? Array.Empty<string>())}]");
                        AddLog(result, opName, JsonUtility.ToJson(report));
#else
                        AddError(result, opName, "Only supported in Unity Editor.");
#endif
                        result.opsExecuted++;
                        break;
                    }

                    case "validateNoScaledParents":
                    {
                        const string opName = "validateNoScaledParents";
#if UNITY_EDITOR
                        var report = ValidateNoScaledParentsInternal(result);
                        if (!report.pass)
                            AddError(result, opName, "Found disallowed non-(1,1,1) scales in gameplay/zone transforms.");
                        AddLog(result, opName, JsonUtility.ToJson(report));
#else
                        AddError(result, opName, "Only supported in Unity Editor.");
#endif
                        result.opsExecuted++;
                        break;
                    }

                    case "ensureFoundation":
                    {
                        const string opName = "ensureFoundation";
#if UNITY_EDITOR
                        var report = EnsureFoundationInternal(result, mode);
                        if (!report.pass)
                            AddWarning(result, opName, "Foundation ensured best-effort; validateFoundation for details.");
                        AddLog(result, opName, JsonUtility.ToJson(report));
#else
                        AddError(result, opName, "Only supported in Unity Editor.");
#endif
                        result.opsExecuted++;
                        break;
                    }

                    case "validateFoundation":
                    {
                        const string opName = "validateFoundation";
#if UNITY_EDITOR
                        var report = ValidateFoundationInternal(result);
                        if (!report.pass)
                            AddError(result, opName, "Foundation validation failed; see report JSON.");
                        AddLog(result, opName, JsonUtility.ToJson(report));
#else
                        AddError(result, opName, "Only supported in Unity Editor.");
#endif
                        result.opsExecuted++;
                        break;
                    }

                    case "ensurePlayerInputStack":
                    {
                        const string opName = "ensurePlayerInputStack";
#if UNITY_EDITOR
                        var report = EnsurePlayerInputStackInternal(result, mode, cmd);
                        if (!report.pass)
                            AddError(result, opName, "ensurePlayerInputStack failed; see report JSON.");
                        AddLog(result, opName, JsonUtility.ToJson(report));
#else
                        AddError(result, opName, "Only supported in Unity Editor.");
#endif
                        result.opsExecuted++;
                        break;
                    }

                    case "runRecipe":
                    {
#if UNITY_EDITOR
                        RunRecipeInternal(result, mode, cmd);
#else
                        AddError(result, "runRecipe", "Only supported in Unity Editor.");
#endif
                        result.opsExecuted++;
                        break;
                    }

                    case "listScriptableObjectTypes":
                    {
#if UNITY_EDITOR
                        var all = DiscoverAllScriptableObjects();
                        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
                        foreach (var so in all)
                        {
                            if (so == null) continue;
                            var fullName = so.GetType().FullName ?? so.GetType().Name;
                            if (counts.TryGetValue(fullName, out var c))
                                counts[fullName] = c + 1;
                            else
                                counts.Add(fullName, 1);
                        }
                        var entries = new List<KeyValuePair<string, int>>(counts);
                        entries.Sort((a, b) => b.Value.CompareTo(a.Value));
                        int take = Math.Min(50, entries.Count);
                        AddLog(result, "listScriptableObjectTypes", $"Total ScriptableObjects loaded: {all.Count}. Distinct types: {entries.Count}. Top {take}:");
                        for (int j = 0; j < take; j++)
                            AddLog(result, "listScriptableObjectTypes", $" - {entries[j].Key} = {entries[j].Value}");
#else
                        AddError(result, "listScriptableObjectTypes", "Only supported in Unity Editor.");
#endif
                        result.opsExecuted++;
                        break;
                    }

                    case "listDropTables":
                    {
#if UNITY_EDITOR
                        var foundTables = DiscoverDropTablesWithFallback(result, "listDropTables", out var fallbackCandidates);
                        var names = GetAssetNames(foundTables);
                        if (names.Length == 0)
                        {
                            if (fallbackCandidates != null && fallbackCandidates.Count > 0)
                                AddWarning(result, "listDropTables", "No assets matched exact type 'DropTable'. Fallback candidates found; see logs for names and type full names.");
                            else
                                AddWarning(result, "listDropTables", "No DropTable assets found (exact or fallback).");
                        }
                        else
                        {
                            AddLog(result, "listDropTables", $"Found {names.Length}: [{string.Join(", ", names)}]");
                        }
#else
                        AddError(result, "listDropTables", "Only supported in Unity Editor.");
#endif
                        result.opsExecuted++;
                        break;
                    }

                    case "listItemDefinitions":
                    {
#if UNITY_EDITOR
                        var foundItems = DiscoverItemDefinitionsWithFallback(result, "listItemDefinitions");
                        var names = GetAssetNames(foundItems);
                        if (names.Length == 0)
                            AddWarning(result, "listItemDefinitions", "No LegacyItemDefinition assets found (exact or fallback).");
                        else
                            AddLog(result, "listItemDefinitions", $"Found {names.Length}: [{string.Join(", ", names)}]");
#else
                        AddError(result, "listItemDefinitions", "Only supported in Unity Editor.");
#endif
                        result.opsExecuted++;
                        break;
                    }

                    case "listGates":
                    {
                        const string opName = "listGates";
#if UNITY_EDITOR
                        var gates = DiscoverGateDefinitions();
                        if (gates.Count == 0)
                        {
                            AddWarning(result, opName, "No GateDefinition assets found.");
                        }
                        else
                        {
                            AddLog(result, opName, $"Found {gates.Count} GateDefinition asset(s).");
                            foreach (var g in gates)
                            {
                                if (g == null) continue;
                                UnityEngine.ScriptableObject required = g.requiredItem;
                                var requiredId = required != null ? TryGetStringMember(required, "itemId") : null;
                                var requiredLabel = required == null
                                    ? "<null>"
                                    : (!string.IsNullOrWhiteSpace(requiredId) ? requiredId : required.name);
                                AddLog(result, opName, $"{g.name} | requiredItem={requiredLabel} | path={GetAssetPath(g)}");
                            }
                        }
#else
                        AddError(result, opName, "Only supported in Unity Editor.");
#endif
                        result.opsExecuted++;
                        break;
                    }

                    case "validateGate":
                    {
                        const string opName = "validateGate";
                        var gateId = cmd.gateId;
                        var expectedKeyItem = cmd.expectedKeyItem;
                        var requiredAmount = cmd.requiredAmount;

                        if (requiredAmount <= 0)
                        {
                            AddError(result, opName, $"requiredAmount must be > 0 (got {requiredAmount}).");
                            result.opsExecuted++;
                            break;
                        }

#if UNITY_EDITOR
                        var gates = DiscoverGateDefinitions();
                        GateDefinition gate = null;
                        foreach (var g in gates)
                        {
                            if (g == null) continue;
                            if (string.Equals(g.name, gateId, StringComparison.OrdinalIgnoreCase))
                            {
                                gate = g;
                                break;
                            }
                        }

                        if (gate == null)
                        {
                            AddError(result, opName, $"GateDefinition not found for gateId '{gateId}' (matched by asset name, case-insensitive).");
                            result.opsExecuted++;
                            break;
                        }

                        AddLog(result, opName, $"GateDefinition found: {gate.name} | path={GetAssetPath(gate)}");

                        var expectedItem = FindItemDefinitionByIdOrName(expectedKeyItem);
                        if (expectedItem == null)
                        {
                            AddError(result, opName, $"Expected key item not found in LegacyItemDefinition assets: '{expectedKeyItem}' (matched by itemId or asset name, case-insensitive).");
                            result.opsExecuted++;
                            break;
                        }

                        var expectedItemId = TryGetStringMember(expectedItem, "itemId");
                        AddLog(result, opName, $"Expected key item resolved: {expectedItem.name} (itemId='{expectedItemId}') | path={GetAssetPath(expectedItem)}");

                        if (gate.requiredItem == null)
                        {
                            AddError(result, opName, $"GateDefinition '{gate.name}' has requiredItem = null.");
                            result.opsExecuted++;
                            break;
                        }

                        if (!ItemDefinitionMatches(gate.requiredItem, expectedKeyItem))
                        {
                            var actualId = TryGetStringMember(gate.requiredItem, "itemId");
                            var actualLabel = !string.IsNullOrWhiteSpace(actualId) ? actualId : gate.requiredItem.name;
                            AddError(result, opName, $"GateDefinition '{gate.name}' requires '{actualLabel}', which does not match expected '{expectedKeyItem}'.");
                            result.opsExecuted++;
                            break;
                        }

                        if (requiredAmount != 1)
                        {
                            AddWarning(result, opName, $"GateDefinition has no quantity field; runtime gate treats requiredAmount as 1. You requested {requiredAmount}.");
                        }

                        AddLog(result, opName, "GateDefinition requirements validated.");
#else
                        AddError(result, opName, "Only supported in Unity Editor.");
#endif

                        result.opsExecuted++;
                        break;
                    }

                    case "listEnemies":
                    {
                        const string opName = "listEnemies";
#if UNITY_EDITOR
                        var enemies = DiscoverEnemiesWithFallback(result, opName);
                        AddLog(result, opName, $"Found {enemies.Count} enemy asset(s).");
#else
                        AddError(result, opName, "Only supported in Unity Editor.");
#endif
                        result.opsExecuted++;
                        break;
                    }

                    case "listEnemyPrefabs":
                    {
                        const string opName = "listEnemyPrefabs";
#if UNITY_EDITOR
                        var candidates = DiscoverEnemyPrefabCandidates(result, opName);
                        AddLog(result, opName, $"Found {candidates.Count} enemy prefab candidate(s). Detection heuristics: prefab.name contains 'Enemy' OR component type name contains 'Enemy'/'Mob'/'Monster'.");
                        foreach (var c in candidates)
                        {
                            if (c == null || c.prefab == null) continue;
                            var types = (c.matchingComponentTypeFullNames != null && c.matchingComponentTypeFullNames.Count > 0)
                                ? string.Join(", ", c.matchingComponentTypeFullNames)
                                : "(none)";
                            AddLog(result, opName, $"{c.prefab.name} | {c.path} | matchingComponents=[{types}]");
                        }
#else
                        AddError(result, opName, "Only supported in Unity Editor.");
#endif
                        result.opsExecuted++;
                        break;
                    }

                    case "validateCommandSchema":
                    {
                        ValidateCommandSchemaForList(result, list);
                        result.opsExecuted++;
                        break;
                    }

                    case "validateDropTable":
                    {
                        const string opName = "validateDropTable";
                        string dropTableId = cmd.dropTableId;
                        string[] expectedItems = cmd.expectedItems;
                        float minDropChance = cmd.minDropChance;

#if UNITY_EDITOR
                        var foundTables = DiscoverDropTablesWithFallback(result, opName, out var dropTableFallbackCandidates);
                        var discoveredTableNames = GetAssetNames(foundTables);
                        AddLog(result, opName, $"Discovered DropTables ({discoveredTableNames.Length}): [{string.Join(", ", discoveredTableNames)}]");

                        if (foundTables.Count == 0)
                        {
                            if (dropTableFallbackCandidates != null && dropTableFallbackCandidates.Count > 0)
                                AddError(result, opName, "No assets were loadable as DropTable (exact type search returned 0; fallback candidates exist but were not DropTable instances). See logs for candidate names/types.");
                            else
                                AddError(result, opName, "No DropTable assets found (exact or fallback).");
                            result.opsExecuted++;
                            break;
                        }

                        if (string.IsNullOrWhiteSpace(dropTableId))
                        {
                            AddError(result, opName, "Missing required field dropTableId.");
                            result.opsExecuted++;
                            break;
                        }

                        DropTable table = null;
                        foreach (var t in foundTables)
                        {
                            if (t != null && string.Equals(t.name, dropTableId, StringComparison.OrdinalIgnoreCase))
                            {
                                table = t;
                                break;
                            }
                        }

                        if (table == null)
                        {
                            AddError(result, opName, $"DropTable '{dropTableId}' not found (match is case-insensitive). Available: [{string.Join(", ", discoveredTableNames)}]");
                            result.opsExecuted++;
                            break;
                        }

                        // Hardened validation: tier totals, empty-table warning, optional expectations.
                        ValidateDropTableNonMutating(result, opName, table, expectedItems, minDropChance);
#else
                        AddError(result, opName, "Only supported in Unity Editor.");
#endif
                        result.opsExecuted++;
                        break;
                    }

                    case "validateAllDropTables":
                    {
                        const string opName = "validateAllDropTables";
#if UNITY_EDITOR
                        int warn0 = result.warnings != null ? result.warnings.Count : 0;
                        int err0 = result.errors != null ? result.errors.Count : 0;

                        var tables = DiscoverDropTablesWithFallback(result, opName, out var fallbackCandidates);
                        if (tables.Count == 0)
                        {
                            if (fallbackCandidates != null && fallbackCandidates.Count > 0)
                                AddError(result, opName, "No assets were loadable as DropTable (fallback candidates exist; see logs).");
                            else
                                AddError(result, opName, "No DropTable assets found.");
                            result.opsExecuted++;
                            break;
                        }

                        AddLog(result, opName, $"Validating {tables.Count} DropTable(s)...");
                        foreach (var t in tables)
                        {
                            if (t == null) continue;
                            ValidateDropTableNonMutating(result, opName, t, expectedItems: null, minDropChance: cmd.minDropChance);
                        }

                        int warn1 = result.warnings != null ? result.warnings.Count : 0;
                        int err1 = result.errors != null ? result.errors.Count : 0;
                        AddLog(result, opName, $"Summary: tables={tables.Count} warningsAdded={warn1 - warn0} errorsAdded={err1 - err0}");
#else
                        AddError(result, opName, "Only supported in Unity Editor.");
#endif
                        result.opsExecuted++;
                        break;
                    }

                    case "validateOrphanItemDefinitions":
                    {
                        const string opName = "validateOrphanItemDefinitions";
#if UNITY_EDITOR
                        var itemCandidates = DiscoverItemDefinitionsWithFallback(result, opName);
                        var items = itemCandidates;

                        if (items.Count == 0)
                        {
                            if (itemCandidates.Count > 0)
                                AddWarning(result, opName, "Found LegacyItemDefinition-like candidates but none were LegacyItemDefinition instances (see logs).");
                            else
                                AddWarning(result, opName, "No LegacyItemDefinition assets found.");
                            result.opsExecuted++;
                            break;
                        }

                        var tables = DiscoverDropTablesWithFallback(result, opName, out _);
                        var referenced = new HashSet<UnityEngine.ScriptableObject>();
                        foreach (var t in tables)
                        {
                            if (t == null) continue;
                            foreach (var it in CollectReferencedItemDefinitions(t))
                                referenced.Add(it);
                        }

                        var orphans = new List<UnityEngine.ScriptableObject>();
                        foreach (var it in items)
                        {
                            if (it == null) continue;
                            if (!referenced.Contains(it))
                                orphans.Add(it);
                        }

                        if (orphans.Count == 0)
                        {
                            AddLog(result, opName, $"No orphan LegacyItemDefinitions found. Total={items.Count}");
                        }
                        else
                        {
                            AddWarning(result, opName, $"Found {orphans.Count} orphan LegacyItemDefinition(s) not referenced by any DropTable:");
                            foreach (var o in orphans)
                                AddLog(result, opName, $" - {o.name} | {GetAssetPath(o)}");
                        }
#else
                        AddError(result, opName, "Only supported in Unity Editor.");
#endif
                        result.opsExecuted++;
                        break;
                    }

                    case "validateEnemyDrops":
                    {
                        const string opName = "validateEnemyDrops";
                        string enemyId = cmd.enemyId;
                        string expectedDropTableId = cmd.dropTableId;
                        string[] expectedItems = cmd.expectedItems;
                        float minDropChance = cmd.minDropChance;

#if UNITY_EDITOR
                        var enemies = DiscoverEnemiesWithFallback(result, opName);
                        if (!string.IsNullOrWhiteSpace(enemyId))
                            enemies = enemies.FindAll(e => e != null && string.Equals(e.name, enemyId, StringComparison.OrdinalIgnoreCase));

                        if (enemies.Count == 0)
                        {
                            AddWarning(result, opName, string.IsNullOrWhiteSpace(enemyId)
                                ? "No enemy assets found."
                                : $"No enemy assets found matching enemyId '{enemyId}'.");
                            result.opsExecuted++;
                            break;
                        }

                        var tables = DiscoverDropTablesWithFallback(result, opName, out _);
                        var tableByName = new Dictionary<string, DropTable>(StringComparer.OrdinalIgnoreCase);
                        foreach (var t in tables)
                        {
                            if (t == null) continue;
                            if (!tableByName.ContainsKey(t.name))
                                tableByName.Add(t.name, t);
                        }

                        foreach (var enemy in enemies)
                        {
                            if (enemy == null) continue;
                            AddLog(result, opName, $"Enemy '{enemy.name}' | {enemy.GetType().FullName} | {GetAssetPath(enemy)}");

                            var refs = ExtractDropTableRefsFromEnemy(enemy);
                            if (refs.Count == 0)
                            {
                                AddWarning(result, opName, $"Enemy '{enemy.name}' has no dropTable/dropTables/loot fields or properties found via reflection.");
                                continue;
                            }

                            var resolved = new List<DropTable>();
                            foreach (var r in refs)
                            {
                                if (r.table != null)
                                {
                                    resolved.Add(r.table);
                                    AddLog(result, opName, $"Enemy '{enemy.name}' ref {r.source} -> DropTable asset '{r.table.name}'");
                                    continue;
                                }

                                if (!string.IsNullOrWhiteSpace(r.id))
                                {
                                    if (tableByName.TryGetValue(r.id, out var dt) && dt != null)
                                    {
                                        resolved.Add(dt);
                                        AddLog(result, opName, $"Enemy '{enemy.name}' ref {r.source} -> DropTableId '{r.id}' resolved to '{dt.name}'");
                                    }
                                    else
                                    {
                                        AddError(result, opName, $"Enemy '{enemy.name}' ref {r.source} -> DropTableId '{r.id}' could not be resolved to an asset");
                                    }
                                }
                            }

                            var uniqueTables = new List<DropTable>();
                            var seen = new HashSet<DropTable>();
                            foreach (var t in resolved)
                            {
                                if (t == null) continue;
                                if (seen.Add(t)) uniqueTables.Add(t);
                            }

                            if (uniqueTables.Count == 0)
                            {
                                AddWarning(result, opName, $"Enemy '{enemy.name}' had drop table references but none could be resolved to assets.");
                                continue;
                            }

                            if (!string.IsNullOrWhiteSpace(expectedDropTableId))
                            {
                                bool matched = false;
                                foreach (var t in uniqueTables)
                                {
                                    if (t != null && string.Equals(t.name, expectedDropTableId, StringComparison.OrdinalIgnoreCase))
                                    {
                                        matched = true;
                                        break;
                                    }
                                }
                                if (!matched)
                                    AddError(result, opName, $"Enemy '{enemy.name}' does not reference expected dropTableId '{expectedDropTableId}'.");

                                uniqueTables = uniqueTables.FindAll(t => t != null && string.Equals(t.name, expectedDropTableId, StringComparison.OrdinalIgnoreCase));
                            }

                            foreach (var t in uniqueTables)
                            {
                                if (t == null) continue;
                                ValidateDropTableNonMutating(result, opName, t, expectedItems, minDropChance);
                            }
                        }
#else
                        AddError(result, opName, "Only supported in Unity Editor.");
#endif
                        result.opsExecuted++;
                        break;
                    }

                    case "validateEnemyPrefabDrops":
                    {
                        const string opName = "validateEnemyPrefabDrops";
                        string prefabId = cmd.prefabId;
                        string expectedDropTableId = cmd.dropTableId;
                        string[] expectedItems = cmd.expectedItems;
                        float minDropChance = cmd.minDropChance;

#if UNITY_EDITOR
                        // Discover candidates via heuristics; if prefabId is specified, we will still validate it even if it doesn't match.
                        var candidates = DiscoverEnemyPrefabCandidates(result, opName);
                        var prefabsToValidate = new List<EnemyPrefabCandidate>(candidates);

                        if (!string.IsNullOrWhiteSpace(prefabId))
                        {
                            prefabsToValidate = prefabsToValidate.FindAll(p => p != null && p.prefab != null &&
                                string.Equals(p.prefab.name, prefabId, StringComparison.OrdinalIgnoreCase));

                            if (prefabsToValidate.Count == 0)
                            {
                                var allPrefabGuids = UnityEditor.AssetDatabase.FindAssets("t:Prefab");
                                foreach (var guid in allPrefabGuids)
                                {
                                    var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                                    var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                                    if (prefab == null) continue;
                                    if (!string.Equals(prefab.name, prefabId, StringComparison.OrdinalIgnoreCase))
                                        continue;

                                    AddWarning(result, opName, $"prefabId '{prefabId}' did not match enemy heuristics, but will be validated because it was explicitly requested.");
                                    prefabsToValidate.Add(new EnemyPrefabCandidate
                                    {
                                        prefab = prefab,
                                        path = path,
                                        matchingComponentTypeFullNames = new List<string>()
                                    });
                                    break;
                                }
                            }
                        }

                        if (prefabsToValidate.Count == 0)
                        {
                            AddWarning(result, opName, string.IsNullOrWhiteSpace(prefabId)
                                ? "No enemy prefab candidates found."
                                : $"No prefabs found matching prefabId '{prefabId}'.");
                            result.opsExecuted++;
                            break;
                        }

                        var allTables = DiscoverDropTablesWithFallback(result, opName, out _);
                        var tableByName = new Dictionary<string, DropTable>(StringComparer.OrdinalIgnoreCase);
                        foreach (var t in allTables)
                        {
                            if (t == null) continue;
                            if (!tableByName.ContainsKey(t.name))
                                tableByName.Add(t.name, t);
                        }

                        AddLog(result, opName, "Matched member name tokens: dropTable, dropTables, loot");

                        foreach (var candidate in prefabsToValidate)
                        {
                            if (candidate == null || candidate.prefab == null) continue;
                            var prefab = candidate.prefab;
                            AddLog(result, opName, $"Prefab '{prefab.name}' | {candidate.path}");

                            var matchedMembers = new HashSet<string>(StringComparer.Ordinal);
                            var resolved = new List<DropTable>();
                            var unresolvedStringIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                            Component[] comps = null;
                            try { comps = prefab.GetComponentsInChildren<Component>(true); } catch { comps = null; }
                            if (comps == null || comps.Length == 0)
                            {
                                AddWarning(result, opName, $"Prefab '{prefab.name}' has no components to inspect.");
                                continue;
                            }

                            foreach (var comp in comps)
                            {
                                if (comp == null) continue; // missing script
                                var refs = ExtractDropTableRefsFromComponent(comp, matchedMembers);
                                foreach (var r in refs)
                                {
                                    if (r == null) continue;
                                    if (r.table != null)
                                    {
                                        resolved.Add(r.table);
                                        continue;
                                    }

                                    if (!string.IsNullOrWhiteSpace(r.id))
                                    {
                                        if (tableByName.TryGetValue(r.id, out var dt) && dt != null)
                                            resolved.Add(dt);
                                        else
                                            unresolvedStringIds.Add(r.id);
                                    }
                                }
                            }

                            if (matchedMembers.Count > 0)
                                AddLog(result, opName, $"Prefab '{prefab.name}' matched members: [{string.Join(", ", matchedMembers)}]");
                            else
                                AddLog(result, opName, $"Prefab '{prefab.name}' matched members: (none)");

                            var uniqueTables = new List<DropTable>();
                            var seen = new HashSet<DropTable>();
                            foreach (var t in resolved)
                            {
                                if (t == null) continue;
                                if (seen.Add(t)) uniqueTables.Add(t);
                            }

                            if (uniqueTables.Count == 0 && unresolvedStringIds.Count == 0)
                            {
                                AddWarning(result, opName, $"Prefab '{prefab.name}' | No DropTable reference found on prefab.");
                                continue;
                            }

                            foreach (var badId in unresolvedStringIds)
                                AddError(result, opName, $"Prefab '{prefab.name}' | DropTableId '{badId}' could not be resolved to an asset.");

                            if (!string.IsNullOrWhiteSpace(expectedDropTableId))
                            {
                                bool matched = false;
                                foreach (var t in uniqueTables)
                                {
                                    if (t != null && string.Equals(t.name, expectedDropTableId, StringComparison.OrdinalIgnoreCase))
                                    {
                                        matched = true;
                                        break;
                                    }
                                }
                                if (!matched)
                                    AddError(result, opName, $"Prefab '{prefab.name}' does not reference expected dropTableId '{expectedDropTableId}'.");

                                uniqueTables = uniqueTables.FindAll(t => t != null && string.Equals(t.name, expectedDropTableId, StringComparison.OrdinalIgnoreCase));
                            }

                            bool shouldValidateDropTables = (expectedItems != null && expectedItems.Length > 0) || IsProvidedMinDropChance(minDropChance);
                            if (shouldValidateDropTables)
                            {
                                foreach (var t in uniqueTables)
                                {
                                    if (t == null) continue;
                                    ValidateDropTableNonMutating(result, opName, t, expectedItems, minDropChance);
                                }
                            }
                            else
                            {
                                foreach (var t in uniqueTables)
                                {
                                    if (t == null) continue;
                                    AddLog(result, opName, $"Prefab '{prefab.name}' references DropTable '{t.name}'");
                                }
                            }
                        }
#else
                        AddError(result, opName, "Only supported in Unity Editor.");
#endif
                        result.opsExecuted++;
                        break;
                    }

                    default:
                        AddWarning(result, "ExecuteCommands", $"Unknown op: {cmd.op}. Skipped.");
                        result.opsExecuted++;
                        break;
                }
            }

            if (result.errors != null && result.errors.Count > 0)
                result.success = false;

            return result;
        }

        public static AiExecutionResult ExecuteCommands(AiCommandEnvelope env, ExecutionMode mode = ExecutionMode.DryRun)
        {
            var list = env != null ? env.commands : null;
            if (list == null)
            {
                var r = new AiExecutionResult { mode = mode, opsPlanned = 0, opsExecuted = 0, success = false };
                AddError(r, "ExecuteCommands", "Envelope had no commands list.");
                return r;
            }

            // Apply envelope scope as effective scope when command doesn't specify one.
            if (env.scope != null && list.commands != null)
            {
                for (int i = 0; i < list.commands.Length; i++)
                {
                    var cmd = list.commands[i];
                    if (cmd == null) continue;

                    if (cmd.scope == null)
                        cmd.scope = CloneScope(env.scope);
                }

                // Enforce maxOperations at the envelope level (safe default: block extras).
                if (env.scope.maxOperations > 0 && list.commands.Length > env.scope.maxOperations)
                {
                    var r = new AiExecutionResult { mode = mode, opsPlanned = list.commands.Length, opsExecuted = 0, success = false };
                    AddError(r, "ExecuteCommands", $"Envelope maxOperations={env.scope.maxOperations} but received {list.commands.Length} commands. Blocked.");
                    return r;
                }
            }

            return ExecuteCommands(list, mode);
        }

        private static SafetyScope CloneScope(SafetyScope scope)
        {
            if (scope == null) return null;
            return new SafetyScope
            {
                allowedRoots = scope.allowedRoots != null ? new List<string>(scope.allowedRoots) : new List<string>(),
                deniedRoots = scope.deniedRoots != null ? new List<string>(scope.deniedRoots) : new List<string>(),
                maxOperations = scope.maxOperations
            };
        }
    }
}
