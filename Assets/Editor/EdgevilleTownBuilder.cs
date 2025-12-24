#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abyss.EditorTools
{
    [InitializeOnLoad]
    public static class EdgevilleTownBuilder
    {
        private const string RootName = "EdgevilleHub_Root";

        private static readonly Vector3 DefaultTownAnchor = new Vector3(-7f, 0f, -170f);

        private const string MenuBuild = "Tools/Abyssbound/Dev/Town/Build Edgeville Hub (One Click)";
        private const string MenuRebuild = "Tools/Abyssbound/Dev/Town/Rebuild Door Markers + ClickTargets (Safe)";
        private const string MenuRemove = "Tools/Abyssbound/Dev/Town/Remove Edgeville Hub";
        private const string MenuPrintMap = "Tools/Abyssbound/Dev/Town/Print Building ↔ Merchant Map";
        private const string MenuSnapToDefault = "Tools/Abyssbound/Dev/Town/Snap Hub To Default Anchor (-7,0,-170)";
        private const string MenuSetupWorkshopInteractables = "Tools/Abyssbound/Dev/Town/Workshop/Setup Skilling Interactables (Forge/Smith/Bonfire)";
        private const string MenuTogglePeek = "Tools/Abyssbound/Dev/Town/Settings/Enable Highlights + Roof Peek";
        private const string MenuPeekSelfTest = "Tools/Abyssbound/Dev/Town/Debug/Peek+Highlight Self Test";

        private const string PrefPeekEnabled = "Abyss.EdgevilleTownBuilder.PeekEnabled";

        private static Transform _peekCurrentBuilding;
        private static readonly List<GameObject> _peekHiddenObjects = new List<GameObject>();
        private static bool _peekHooksInstalled;

        static EdgevilleTownBuilder()
        {
            // Some Unity setups (domain reload disabled, etc.) can be finicky;
            // this ensures hooks are installed as soon as the editor loads the class.
            InitPeekAndHighlights();
        }

        private static bool _loggedMissingInteractableLayer;

        private struct BuildingSpec
        {
            public string Name;
            public Vector3 Position;
            public Vector3 Size;
            public Vector3 Forward;
        }

        private static readonly BuildingSpec[] Buildings =
        {
            // Layout inspired by the Edgeville map: a central hub with surrounding buildings.
            // Coordinates are local to EdgevilleHub_Root (anchor stays fixed at -7,0,-170).
            new BuildingSpec { Name = "Building_Workshop",    Position = new Vector3( 0f, 0f,  0f), Size = new Vector3(8f, 4f, 8f), Forward = Vector3.back },
            new BuildingSpec { Name = "Building_Weapons",      Position = new Vector3( 0f, 0f, 12f), Size = new Vector3(6f, 4f, 6f), Forward = Vector3.zero },
            new BuildingSpec { Name = "Building_Consumables", Position = new Vector3(-12f, 0f,  0f), Size = new Vector3(6f, 4f, 6f), Forward = Vector3.zero },
            new BuildingSpec { Name = "Building_Skilling",    Position = new Vector3( 12f, 0f,  0f), Size = new Vector3(6f, 4f, 6f), Forward = Vector3.zero },
            new BuildingSpec { Name = "Building_ExtraA",      Position = new Vector3( 12f, 0f, 12f), Size = new Vector3(5f, 4f, 5f), Forward = Vector3.zero },
            new BuildingSpec { Name = "Building_ExtraB",      Position = new Vector3(-12f, 0f, 12f), Size = new Vector3(5f, 4f, 5f), Forward = Vector3.zero },
        };

        private static readonly Dictionary<string, string> BuildingToDoor = new()
        {
            { "Building_Weapons", "Door_Weapons" },
            { "Building_Consumables", "Door_Consumables" },
            { "Building_Skilling", "Door_Skilling" },
            { "Building_Workshop", "Door_Workshop" },
            { "Building_ExtraA", "Door_ExtraA" },
            { "Building_ExtraB", "Door_ExtraB" },
        };

        private static readonly (string merchantName, string doorName)[] MerchantMap =
        {
            ("WeaponsGearMerchant", "Door_Weapons"),
            ("ConsumablesMerchant", "Door_Consumables"),
            ("SkillingSuppliesMerchant", "Door_Skilling"),
            ("WorkshopMerchant", "Door_Workshop"),
        };

        private static readonly Dictionary<string, string> DoorToMerchant = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Door_Weapons", "WeaponsGearMerchant" },
            { "Door_Consumables", "ConsumablesMerchant" },
            { "Door_Skilling", "SkillingSuppliesMerchant" },
            { "Door_Workshop", "WorkshopMerchant" },
        };

        [MenuItem(MenuBuild)]
        public static void BuildEdgevilleHubOneClick()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("Run this in Edit Mode (not Play Mode).");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("No valid active scene.");
                return;
            }

            var existingRoot = FindInSceneByExactName(RootName);
            if (existingRoot != null)
            {
                bool ok = EditorUtility.DisplayDialog(
                    "Rebuild Edgeville Hub",
                    "Rebuild will delete and recreate EdgevilleHub_Root. Continue?",
                    "Yes",
                    "No");

                if (!ok)
                    return;

                Undo.DestroyObjectImmediate(existingRoot);
            }

            _loggedMissingInteractableLayer = false;

            var summary = new StringBuilder();
            summary.AppendLine("[EdgevilleTownBuilder] Build Edgeville Hub (One Click)");

            var anchor = DefaultTownAnchor;

            var worldLit = TryFindMaterialByNames(
                "World_Lit Base",
                "World_Lit_Base",
                "World_lit_base",
                "WorldLitBase");
            if (worldLit == null)
                summary.AppendLine("- WARN: Material 'World_Lit Base' not found; primitives may use Unity default.");

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create Edgeville hub root");
            root.transform.position = anchor;

            var environment = CreateChild(root.transform, "Environment");
            var groundParent = CreateChild(environment, "Ground");
            var roadsParent = CreateChild(environment, "Roads");
            var fencesParent = CreateChild(environment, "Fences");
            var decorParent = CreateChild(environment, "Decor");

            var buildingsParent = CreateChild(root.transform, "Buildings");
            var doorsParent = CreateChild(root.transform, "Doors");
            var merchantsParent = CreateChild(root.transform, "Merchants");

            BuildGroundAndRoads(groundParent, roadsParent, worldLit, summary);

            var buildingRoots = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
            foreach (var spec in Buildings)
            {
                var b = BuildBuilding(buildingsParent, spec, worldLit);
                buildingRoots[spec.Name] = b;
                summary.AppendLine($"- Created building: {spec.Name} pos={spec.Position} size={spec.Size}");
            }

            // Doors from buildings
            var doorRoots = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
            var doorToBuilding = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in BuildingToDoor)
            {
                if (!buildingRoots.TryGetValue(kv.Key, out var b) || b == null)
                    continue;

                var door = CreateOrReplaceChild(doorsParent, kv.Value);
                ApplyDoorMarkerTransform(door, b, GetBuildingDepthFromShell(b, fallback: 6f));
                doorRoots[kv.Value] = door;
                doorToBuilding[kv.Value] = b;
                summary.AppendLine($"- Door marker: {kv.Value} pos={door.position} rotY={door.eulerAngles.y:0.0}");
            }

            // Merchants
            var foundMerchants = new List<string>();
            var createdPlaceholders = new List<string>();

            foreach (var (merchantName, doorName) in MerchantMap)
            {
                var door = doorRoots.TryGetValue(doorName, out var d) ? d : null;
                if (door == null)
                {
                    summary.AppendLine($"- WARN: Missing door '{doorName}' for merchant '{merchantName}'");
                    continue;
                }

                var building = doorToBuilding.TryGetValue(doorName, out var b) ? b : null;

                var merchant = FindInSceneByNameContains(merchantName);
                if (merchant == null)
                {
                    merchant = new GameObject(merchantName);
                    Undo.RegisterCreatedObjectUndo(merchant, "Create merchant placeholder");
                    createdPlaceholders.Add(merchantName);
                    Debug.LogWarning("Merchant object not found; created placeholder. Attach prefab/scripts later.");
                }
                else
                {
                    foundMerchants.Add(merchantName);
                }

                EnsureParent(merchant.transform, merchantsParent);

                // Ensure an actual shop component exists so the UI can open even on placeholders.
                EnsureMerchantShopComponent(merchant, merchantName);

                PlaceMerchantAtDoor(merchant.transform, door);

                var clickTarget = EnsureClickTarget(merchant.transform, door, building);
                EnsureBuildingClickTarget(merchant.transform, building);
                summary.AppendLine($"- Merchant: {merchantName} at {merchant.transform.position} clickTarget={clickTarget.position}");
            }

            AppendBuildingMerchantMap(summary);

            // Remove any old always-on signage; hover labels handle shop naming now.
            RemoveAllBuildingSigns(buildingRoots);

            EnsureDirectionalLight(summary);
            EnsureSpawnPoint(root.transform, summary);

            // Final pass: ensure town uses World_Lit Base everywhere we can.
            ApplyMaterialToTown(root.transform, worldLit, summary);

            // Minimal decor placeholders (empty parents exist already)
            // Leave fences/decor empty by design.
            _ = fencesParent;
            _ = decorParent;

            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;

            summary.AppendLine($"- Town anchor: {anchor} (fixed)");

            summary.AppendLine("- Merchants found: " + (foundMerchants.Count == 0 ? "(none)" : string.Join(", ", foundMerchants)));
            summary.AppendLine("- Merchants placeholders created: " + (createdPlaceholders.Count == 0 ? "(none)" : string.Join(", ", createdPlaceholders)));

            if (LayerMask.NameToLayer("Interactable") < 0)
                summary.AppendLine("- Reminder: Layer 'Interactable' is missing; ClickTargets left on Default.");

            Debug.Log(summary.ToString());
        }

        [MenuItem(MenuRebuild)]
        public static void RebuildDoorsAndClickTargetsSafe()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("Run this in Edit Mode (not Play Mode).");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("No valid active scene.");
                return;
            }

            _loggedMissingInteractableLayer = false;

            var summary = new StringBuilder();
            summary.AppendLine("[EdgevilleTownBuilder] Rebuild Door Markers + ClickTargets (Safe)");

            var worldLit = TryFindMaterialByNames(
                "World_Lit Base",
                "World_Lit_Base",
                "World_lit_base",
                "WorldLitBase");
            if (worldLit == null)
                summary.AppendLine("- WARN: Material 'World_Lit Base' not found; cannot reapply town materials.");

            var root = FindInSceneByExactName(RootName);
            if (root == null)
            {
                Debug.LogWarning($"'{RootName}' not found. Run '{MenuBuild}' first.");
                return;
            }

            summary.AppendLine($"- Town anchor (unchanged): {root.transform.position}");

            var buildingsParent = FindOrCreateChild(root.transform, "Buildings");
            var doorsParent = FindOrCreateChild(root.transform, "Doors");
            var merchantsParent = FindOrCreateChild(root.transform, "Merchants");

            // Ensure town renderers use World_Lit Base (fixes “orange town” even without rebuild).
            ApplyMaterialToTown(root.transform, worldLit, summary);

            var buildingRoots = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
            foreach (var spec in Buildings)
            {
                var b = FindChildByExactName(buildingsParent, spec.Name);
                if (b != null)
                    buildingRoots[spec.Name] = b;
            }

            // Remove any old always-on signage; hover labels handle shop naming now.
            RemoveAllBuildingSigns(buildingRoots);

            // Ensure skilling interactables live by the Workshop building (and remove debug spheres).
            EnsureWorkshopSkillingInteractables(buildingRoots);

            // Recompute doors from current building transforms
            var doorRoots = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
            var doorToBuilding = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in BuildingToDoor)
            {
                if (!buildingRoots.TryGetValue(kv.Key, out var b) || b == null)
                {
                    summary.AppendLine($"- WARN: Building not found: {kv.Key} (skipping door)");
                    continue;
                }

                var door = FindOrCreateChild(doorsParent, kv.Value);
                ApplyDoorMarkerTransform(door, b, GetBuildingDepthFromShell(b, fallback: 6f));
                doorRoots[kv.Value] = door;
                doorToBuilding[kv.Value] = b;
                summary.AppendLine($"- Door marker updated: {kv.Value} pos={door.position}");
            }

            // Reposition merchants and rebuild click targets
            foreach (var (merchantName, doorName) in MerchantMap)
            {
                var door = doorRoots.TryGetValue(doorName, out var d) ? d : null;
                if (door == null)
                {
                    summary.AppendLine($"- WARN: Door not found: {doorName} (skipping merchant '{merchantName}')");
                    continue;
                }

                var building = doorToBuilding.TryGetValue(doorName, out var b) ? b : null;

                var merchant = FindInSceneByNameContains(merchantName);
                if (merchant == null)
                {
                    merchant = new GameObject(merchantName);
                    Undo.RegisterCreatedObjectUndo(merchant, "Create merchant placeholder");
                    Debug.LogWarning("Merchant object not found; created placeholder. Attach prefab/scripts later.");
                }

                EnsureParent(merchant.transform, merchantsParent);

                EnsureMerchantShopComponent(merchant, merchantName);
                PlaceMerchantAtDoor(merchant.transform, door);

                var clickTarget = EnsureClickTarget(merchant.transform, door, building);
                EnsureBuildingClickTarget(merchant.transform, building);
                summary.AppendLine($"- Merchant updated: {merchantName} at {merchant.transform.position} clickTarget={clickTarget.position}");
            }

            AppendBuildingMerchantMap(summary);

            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;

            if (LayerMask.NameToLayer("Interactable") < 0)
                summary.AppendLine("- Reminder: Layer 'Interactable' is missing; ClickTargets left on Default.");

            Debug.Log(summary.ToString());
        }

        [MenuItem(MenuSetupWorkshopInteractables)]
        public static void SetupWorkshopSkillingInteractablesMenu()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("Run this in Edit Mode (not Play Mode).");
                return;
            }

            var root = FindInSceneByExactName(RootName);
            if (root == null)
            {
                Debug.LogWarning($"'{RootName}' not found. Run '{MenuBuild}' first.");
                return;
            }

            var buildingsParent = FindOrCreateChild(root.transform, "Buildings");
            var buildingRoots = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
            foreach (var spec in Buildings)
            {
                var b = FindChildByExactName(buildingsParent, spec.Name);
                if (b != null)
                    buildingRoots[spec.Name] = b;
            }

            EnsureWorkshopSkillingInteractables(buildingRoots);
            Debug.Log("[EdgevilleTownBuilder] Workshop skilling interactables set up.");
        }

        private static void EnsureWorkshopSkillingInteractables(Dictionary<string, Transform> buildingRoots)
        {
            if (buildingRoots == null) return;
            if (!buildingRoots.TryGetValue("Building_Workshop", out var workshopBuilding) || workshopBuilding == null)
                return;

            // These objects are managed by TownRegistry/Town_SpawnRoot.
            var registry = Game.Town.TownRegistry.Instance;
            registry.EnsureSpawnRoot();
            registry.RebuildIndexFromScene();

            EnsureAndPlaceInteractable(registry, "interactable_forge", typeof(ForgeInteractable), workshopBuilding, new Vector3(2.6f, 0f, 1.8f));
            EnsureAndPlaceInteractable(registry, "interactable_smithingstand", typeof(SmithingStandInteractable), workshopBuilding, new Vector3(3.8f, 0f, 0.0f));
            EnsureAndPlaceInteractable(registry, "interactable_workshop", typeof(WorkshopInteractable), workshopBuilding, new Vector3(2.6f, 0f, -1.8f));
            EnsureAndPlaceInteractable(registry, "interactable_bonfire", typeof(BonfireInteractable), workshopBuilding, new Vector3(1.2f, 0f, 0.0f));
        }

        private static void EnsureAndPlaceInteractable(Game.Town.TownRegistry registry, string key, Type componentType, Transform workshopBuilding, Vector3 localOffset)
        {
            if (registry == null || workshopBuilding == null) return;

            GameObject go;
            if (!registry.TryGet(key, out go) || go == null)
            {
                go = new GameObject(componentType.Name);
                Undo.RegisterCreatedObjectUndo(go, $"Create {key}");
                go.AddComponent(componentType);
                var col = go.GetComponent<BoxCollider>();
                if (col == null) col = Undo.AddComponent<BoxCollider>(go);
                col.isTrigger = true;
                col.center = new Vector3(0f, 1f, 0f);
                col.size = new Vector3(1.2f, 2f, 1.2f);
                go = registry.RegisterOrKeep(key, go);
            }

            // Remove the visible debug sphere marker if it exists.
            RemoveDebugMarkerChildren(go);

            // Place relative to the workshop building (world).
            var world = workshopBuilding.TransformPoint(localOffset);
            world.y = 0f;
            Undo.RecordObject(go.transform, $"Move {key}");
            go.transform.position = world;
            go.transform.rotation = workshopBuilding.rotation;
        }

        private static void RemoveDebugMarkerChildren(GameObject root)
        {
            if (root == null) return;
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                var c = root.transform.GetChild(i);
                if (c == null) continue;
                if (!string.Equals(c.name, "Sphere", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(c.name, "DebugMarker", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Only delete if it looks like a pure visual marker.
                if (c.GetComponent<Renderer>() == null) continue;
                Undo.DestroyObjectImmediate(c.gameObject);
            }
        }

        [MenuItem(MenuSnapToDefault)]
        public static void SnapHubToDefaultAnchor()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("Run this in Edit Mode (not Play Mode).");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("No valid active scene.");
                return;
            }

            var root = FindInSceneByExactName(RootName);
            if (root == null)
            {
                Debug.LogWarning($"'{RootName}' not found. Run '{MenuBuild}' first.");
                return;
            }

            Undo.RecordObject(root.transform, "Snap Edgeville hub to default anchor");
            root.transform.position = DefaultTownAnchor;
            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(scene);

            // Recompute dependent markers so the interaction colliders stay correct.
            RebuildDoorsAndClickTargetsSafe();

            Debug.Log($"[EdgevilleTownBuilder] Snapped '{RootName}' to {DefaultTownAnchor} and rebuilt doors/click targets.");
        }

        [MenuItem(MenuPrintMap)]
        public static void PrintBuildingMerchantMap()
        {
            var summary = new StringBuilder();
            summary.AppendLine("[EdgevilleTownBuilder] Building ↔ Merchant Map");
            AppendBuildingMerchantMap(summary);
            Debug.Log(summary.ToString());
        }

        [MenuItem(MenuTogglePeek)]
        public static void TogglePeekMode()
        {
            InitPeekAndHighlights();
            bool next = !GetPeekEnabled();
            SetPeekEnabled(next);
            Debug.Log($"[EdgevilleTownBuilder] Highlights + Roof Peek = {next}");
            SceneView.RepaintAll();
        }

        [MenuItem(MenuTogglePeek, validate = true)]
        private static bool TogglePeekMode_Validate()
        {
            Menu.SetChecked(MenuTogglePeek, GetPeekEnabled());
            return true;
        }

        private static void BuildGroundAndRoads(Transform groundParent, Transform roadsParent, Material townMat, StringBuilder summary)
        {
            // Ground plane
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Undo.RegisterCreatedObjectUndo(ground, "Create ground");
            ground.name = "Ground";
            ground.transform.SetParent(groundParent, false);
            ground.transform.localPosition = Vector3.zero;
            ground.transform.localRotation = Quaternion.identity;
            ground.transform.localScale = new Vector3(6f, 1f, 6f);
            RemoveColliderIfAny(ground);
            ApplySharedMaterial(ground, townMat);

            // Roads
            var mainRoad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(mainRoad, "Create main road");
            mainRoad.name = "Road_Main_NS";
            mainRoad.transform.SetParent(roadsParent, false);
            mainRoad.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            mainRoad.transform.localScale = new Vector3(4f, 0.1f, 40f);
            RemoveColliderIfAny(mainRoad);
            ApplySharedMaterial(mainRoad, townMat);

            var spur = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(spur, "Create road spur" );
            spur.name = "Road_Spur_EW";
            spur.transform.SetParent(roadsParent, false);
            spur.transform.localPosition = new Vector3(0f, 0.05f, -6f);
            spur.transform.localScale = new Vector3(24f, 0.1f, 4f);
            RemoveColliderIfAny(spur);
            ApplySharedMaterial(spur, townMat);

            // Optional small spur for flavor
            var spur2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(spur2, "Create road spur" );
            spur2.name = "Road_Spur_WN";
            spur2.transform.SetParent(roadsParent, false);
            spur2.transform.localPosition = new Vector3(-10f, 0.05f, 10f);
            spur2.transform.localScale = new Vector3(12f, 0.1f, 3f);
            RemoveColliderIfAny(spur2);
            ApplySharedMaterial(spur2, townMat);

            summary.AppendLine("- Created ground + roads (forcing World_Lit Base if found)");
        }

        private static Transform BuildBuilding(Transform buildingsParent, BuildingSpec spec, Material townMat)
        {
            var buildingRoot = new GameObject(spec.Name);
            Undo.RegisterCreatedObjectUndo(buildingRoot, "Create building root");
            buildingRoot.transform.SetParent(buildingsParent, false);
            buildingRoot.transform.localPosition = spec.Position;

            var forward = spec.Forward;
            if (forward == Vector3.zero)
            {
                var toCenter = (Vector3.zero - spec.Position);
                toCenter.y = 0f;
                forward = toCenter.sqrMagnitude > 0.001f ? toCenter.normalized : Vector3.forward;
            }

            buildingRoot.transform.localRotation = Quaternion.LookRotation(forward, Vector3.up);

            var w = Mathf.Max(0.5f, spec.Size.x);
            var h = Mathf.Max(0.5f, spec.Size.y);
            var d = Mathf.Max(0.5f, spec.Size.z);

            // Shell
            var shell = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(shell, "Create building shell");
            shell.name = "Shell";
            shell.transform.SetParent(buildingRoot.transform, false);
            shell.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);
            shell.transform.localRotation = Quaternion.identity;
            shell.transform.localScale = new Vector3(w, h, d);
            RemoveColliderIfAny(shell);
            ApplySharedMaterial(shell, townMat);

            // Simple interior floor so "roof peek" actually reveals something.
            try
            {
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Undo.RegisterCreatedObjectUndo(floor, "Create interior floor");
                floor.name = "Floor";
                floor.transform.SetParent(buildingRoot.transform, false);
                floor.transform.localRotation = Quaternion.identity;
                floor.transform.localScale = new Vector3(w * 0.88f, 0.05f, d * 0.88f);
                floor.transform.localPosition = new Vector3(0f, 0.025f, 0f);
                RemoveColliderIfAny(floor);
                ApplySharedMaterial(floor, townMat);
            }
            catch { }

            // Small Edgeville-ish details (still primitives): porch + chimney/extension.
            // Keep this subtle to avoid breaking door/merchant placement.
            try
            {
                if (!spec.Name.Contains("Extra", StringComparison.OrdinalIgnoreCase))
                {
                    var porch = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Undo.RegisterCreatedObjectUndo(porch, "Create porch");
                    porch.name = "Porch";
                    porch.transform.SetParent(buildingRoot.transform, false);
                    porch.transform.localRotation = Quaternion.identity;
                    porch.transform.localScale = new Vector3(Mathf.Min(3.2f, w * 0.55f), 0.9f, 1.6f);
                    porch.transform.localPosition = new Vector3(0f, 0.45f, (d * 0.5f) + (porch.transform.localScale.z * 0.5f) - 0.1f);
                    RemoveColliderIfAny(porch);
                    ApplySharedMaterial(porch, townMat);

                    var chimney = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Undo.RegisterCreatedObjectUndo(chimney, "Create chimney");
                    chimney.name = "Chimney";
                    chimney.transform.SetParent(buildingRoot.transform, false);
                    chimney.transform.localRotation = Quaternion.identity;
                    chimney.transform.localScale = new Vector3(0.7f, 1.6f, 0.7f);
                    chimney.transform.localPosition = new Vector3((w * 0.35f) * Mathf.Sign(w), h + 1.1f, -(d * 0.15f));
                    RemoveColliderIfAny(chimney);
                    ApplySharedMaterial(chimney, townMat);
                }

                if (spec.Name.Equals("Building_Workshop", StringComparison.OrdinalIgnoreCase))
                {
                    var annex = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Undo.RegisterCreatedObjectUndo(annex, "Create workshop annex");
                    annex.name = "Annex";
                    annex.transform.SetParent(buildingRoot.transform, false);
                    annex.transform.localRotation = Quaternion.identity;
                    annex.transform.localScale = new Vector3(Mathf.Max(2.8f, w * 0.45f), Mathf.Max(2.6f, h * 0.75f), Mathf.Max(2.8f, d * 0.45f));
                    annex.transform.localPosition = new Vector3(-(w * 0.5f) - (annex.transform.localScale.x * 0.5f) + 0.2f, annex.transform.localScale.y * 0.5f, -0.5f);
                    RemoveColliderIfAny(annex);
                    ApplySharedMaterial(annex, townMat);
                }
            }
            catch { }

            // Door frame on front face (local +Z)
            var doorFrame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(doorFrame, "Create door frame");
            doorFrame.name = "DoorFrame";
            doorFrame.transform.SetParent(buildingRoot.transform, false);
            doorFrame.transform.localScale = new Vector3(1.6f, 2.4f, 0.3f);
            doorFrame.transform.localPosition = new Vector3(0f, 1.2f, (d * 0.5f) + 0.15f);
            doorFrame.transform.localRotation = Quaternion.identity;
            RemoveColliderIfAny(doorFrame);
            ApplySharedMaterial(doorFrame, townMat);

            // Roof
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(roof, "Create roof");
            roof.name = "Roof";
            roof.transform.SetParent(buildingRoot.transform, false);
            roof.transform.localScale = new Vector3(w * 1.05f, 0.6f, d * 1.05f);
            roof.transform.localPosition = new Vector3(0f, h + 0.3f, 0f);
            roof.transform.localRotation = Quaternion.identity;
            RemoveColliderIfAny(roof);
            ApplySharedMaterial(roof, townMat);

            return buildingRoot.transform;
        }

        private static void ApplyDoorMarkerTransform(Transform doorMarker, Transform buildingRoot, float buildingDepth)
        {
            if (doorMarker == null || buildingRoot == null) return;

            doorMarker.rotation = buildingRoot.rotation;

            var forward = buildingRoot.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            forward.Normalize();

            var pos = buildingRoot.position + (forward * ((buildingDepth * 0.5f) + 0.6f));
            pos.y = 0f;
            doorMarker.position = pos;
        }

        private static float GetBuildingDepthFromShell(Transform buildingRoot, float fallback)
        {
            if (buildingRoot == null) return fallback;
            try
            {
                var shell = buildingRoot.Find("Shell");
                if (shell != null)
                {
                    var ls = shell.localScale;
                    if (ls.z > 0.1f) return ls.z;
                }
            }
            catch { }
            return fallback;
        }

        private static void PlaceMerchantAtDoor(Transform merchant, Transform door)
        {
            if (merchant == null || door == null) return;

            var doorForward = door.forward;
            doorForward.y = 0f;
            if (doorForward.sqrMagnitude < 0.0001f)
                doorForward = Vector3.forward;
            doorForward.Normalize();

            var pos = door.position + (doorForward * -0.9f);
            pos.y = 0f;
            merchant.position = pos;
            merchant.rotation = Quaternion.LookRotation(doorForward, Vector3.up);
        }

        private static Transform EnsureClickTarget(Transform merchant, Transform door)
        {
            Transform clickTarget = null;
            try { clickTarget = merchant.Find("ClickTarget"); } catch { }

            if (clickTarget == null)
            {
                var go = new GameObject("ClickTarget");
                Undo.RegisterCreatedObjectUndo(go, "Create ClickTarget");
                go.transform.SetParent(merchant, false);
                clickTarget = go.transform;
            }

            var doorForward = door.forward;
            doorForward.y = 0f;
            if (doorForward.sqrMagnitude < 0.0001f)
                doorForward = Vector3.forward;
            doorForward.Normalize();

            clickTarget.position = door.position + (doorForward * 0.75f);
            clickTarget.rotation = door.rotation;

            // Collider setup
            var bc = clickTarget.GetComponent<BoxCollider>();
            if (bc == null)
                bc = Undo.AddComponent<BoxCollider>(clickTarget.gameObject);

            bc.size = new Vector3(1.8f, 2.4f, 1.8f);
            bc.center = new Vector3(0f, 1.2f, 0f);
            bc.isTrigger = false;

            // Ensure it has no renderers (pure collider)
            RemoveRendererIfAny(clickTarget.gameObject);

            // Layer setup
            int layer = LayerMask.NameToLayer("Interactable");
            if (layer >= 0)
            {
                clickTarget.gameObject.layer = layer;
            }
            else
            {
                if (!_loggedMissingInteractableLayer)
                {
                    _loggedMissingInteractableLayer = true;
                    Debug.LogWarning("Layer 'Interactable' not found; ClickTargets will remain on Default.");
                }
            }

            return clickTarget;
        }

        private static Transform EnsureClickTarget(Transform merchant, Transform door, Transform building)
        {
            var clickTarget = EnsureClickTarget(merchant, door);

            // Add runtime hover-highlighting. Trigger is the door ClickTarget, but the highlight affects the whole building.
            try
            {
                Renderer[] renderers = null;
                if (building != null)
                {
                    renderers = building.GetComponentsInChildren<Renderer>(true);
                }

                var target = clickTarget.GetComponent<Abyss.Shop.MerchantDoorClickTarget>();
                if (target == null)
                    target = Undo.AddComponent<Abyss.Shop.MerchantDoorClickTarget>(clickTarget.gameObject);

                if (renderers != null && renderers.Length > 0)
                {
                    target.SetHighlightRenderers(renderers);
                    target.SetHighlightColor(Color.red);
                }
            }
            catch { }

            return clickTarget;
        }

        private static void EnsureBuildingClickTarget(Transform merchant, Transform building)
        {
            if (merchant == null || building == null) return;

            Transform click = null;
            try { click = merchant.Find("BuildingClickTarget"); } catch { }

            if (click == null)
            {
                var go = new GameObject("BuildingClickTarget");
                Undo.RegisterCreatedObjectUndo(go, "Create BuildingClickTarget");
                go.transform.SetParent(merchant, true);
                click = go.transform;
            }

            // Align to building volume (no renderer).
            click.position = building.position;
            click.rotation = building.rotation;

            float h = 4f;
            float w = 6f;
            float d = 6f;
            try
            {
                var shell = building.Find("Shell");
                if (shell != null)
                {
                    var ls = shell.localScale;
                    w = Mathf.Max(0.5f, ls.x);
                    h = Mathf.Max(0.5f, ls.y);
                    d = Mathf.Max(0.5f, ls.z);
                }
            }
            catch { }

            var bc = click.GetComponent<BoxCollider>();
            if (bc == null)
                bc = Undo.AddComponent<BoxCollider>(click.gameObject);

            bc.size = new Vector3(w, h, d);
            bc.center = new Vector3(0f, h * 0.5f, 0f);
            bc.isTrigger = false;

            RemoveRendererIfAny(click.gameObject);

            // Add hover/highlight component to the building-wide collider as well.
            try
            {
                var renderers = building.GetComponentsInChildren<Renderer>(true);
                if (renderers != null && renderers.Length > 0)
                {
                    var target = click.GetComponent<Abyss.Shop.MerchantDoorClickTarget>();
                    if (target == null)
                        target = Undo.AddComponent<Abyss.Shop.MerchantDoorClickTarget>(click.gameObject);

                    target.SetHighlightRenderers(renderers);
                    target.SetHighlightColor(Color.red);
                }
            }
            catch { }

            int layer = LayerMask.NameToLayer("Interactable");
            if (layer >= 0)
                click.gameObject.layer = layer;
        }

        private static void EnsureMerchantShopComponent(GameObject merchant, string merchantName)
        {
            if (merchant == null) return;

            var shop = merchant.GetComponent<Abyss.Shop.MerchantShop>();
            if (shop == null)
                shop = Undo.AddComponent<Abyss.Shop.MerchantShop>(merchant);

            try
            {
                var so = new SerializedObject(shop);
                var p = so.FindProperty("_merchantName");
                if (p != null)
                {
                    p.stringValue = GetFriendlyShopName(merchantName);
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            catch { }
        }

        private static string GetFriendlyShopName(string merchantName)
        {
            if (string.IsNullOrWhiteSpace(merchantName)) return "Merchant";

            if (merchantName.IndexOf("Weapons", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Weapons & Gear";
            if (merchantName.IndexOf("Consum", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Consumables";
            if (merchantName.IndexOf("Skill", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Skilling Supplies";
            if (merchantName.IndexOf("Work", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Workshop";

            return merchantName;
        }

        private static void RemoveAllBuildingSigns(Dictionary<string, Transform> buildingRoots)
        {
            if (buildingRoots == null) return;
            foreach (var kv in buildingRoots)
            {
                var b = kv.Value;
                if (b == null) continue;
                try
                {
                    var sign = b.Find("Sign");
                    if (sign != null)
                        Undo.DestroyObjectImmediate(sign.gameObject);
                }
                catch { }
            }
        }

        private static void TryCreateSignage(Dictionary<string, Transform> buildingRoots, StringBuilder summary)
        {
            // Signage only for the 4 merchant buildings
            var signs = new (string building, string text)[]
            {
                ("Building_Weapons", "Weapons"),
                ("Building_Consumables", "Supplies"),
                ("Building_Skilling", "Skilling"),
                ("Building_Workshop", "Workshop"),
            };

            var tmpType = Type.GetType("TMPro.TextMeshPro, Unity.TextMeshPro");
            if (tmpType == null)
                tmpType = Type.GetType("TMPro.TextMeshPro, TMPro");

            if (tmpType == null)
            {
                summary.AppendLine("- TMPro (3D) not available; skipped signage.");
                return;
            }

            int created = 0;

            foreach (var (buildingName, label) in signs)
            {
                if (!buildingRoots.TryGetValue(buildingName, out var b) || b == null)
                    continue;

                var existing = b.Find("Sign");
                Transform signTf;
                if (existing != null)
                {
                    signTf = existing;
                }
                else
                {
                    var signGo = new GameObject("Sign");
                    Undo.RegisterCreatedObjectUndo(signGo, "Create sign");
                    signTf = signGo.transform;
                    signTf.SetParent(b, false);
                    created++;
                }

                signTf.localRotation = Quaternion.identity;

                // Place above door frame, slightly forward
                var depth = GetBuildingDepthFromShell(b, fallback: 6f);
                signTf.localPosition = new Vector3(0f, 3.8f, (depth * 0.5f) + 0.45f);

                Component tmp;
                try
                {
                    tmp = signTf.GetComponent(tmpType) ?? signTf.gameObject.AddComponent(tmpType);
                }
                catch
                {
                    continue;
                }

                try
                {
                    // Set text via reflection
                    var textProp = tmpType.GetProperty("text");
                    textProp?.SetValue(tmp, label, null);

                    var fontSizeProp = tmpType.GetProperty("fontSize");
                    fontSizeProp?.SetValue(tmp, 2.2f, null);

                    var colorProp = tmpType.GetProperty("color");
                    colorProp?.SetValue(tmp, Color.white, null);

                    // Center align if available
                    var alignProp = tmpType.GetProperty("alignment");
                    if (alignProp != null)
                    {
                        // TMPro.TextAlignmentOptions.Center
                        var alignEnum = alignProp.PropertyType;
                        var centerValue = Enum.Parse(alignEnum, "Center");
                        alignProp.SetValue(tmp, centerValue, null);
                    }
                }
                catch { }
            }

            if (created > 0)
                summary.AppendLine($"- Created {created} signage labels (TMPro 3D).");
            else
                summary.AppendLine("- Signage already present (TMPro 3D).");
        }

        private static void EnsureDirectionalLight(StringBuilder summary)
        {
            try
            {
                var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
                foreach (var l in lights)
                {
                    if (l != null && l.type == LightType.Directional)
                    {
                        summary.AppendLine("- Directional light already exists.");
                        return;
                    }
                }
            }
            catch { }

            var go = new GameObject("Directional Light");
            Undo.RegisterCreatedObjectUndo(go, "Create directional light");
            var light = Undo.AddComponent<Light>(go);
            light.type = LightType.Directional;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            summary.AppendLine("- Created Directional Light.");
        }

        private static void EnsureSpawnPoint(Transform root, StringBuilder summary)
        {
            if (root == null) return;

            var existing = FindChildByExactName(root, "PlayerSpawn_Town");
            if (existing == null)
            {
                var go = new GameObject("PlayerSpawn_Town");
                Undo.RegisterCreatedObjectUndo(go, "Create spawn point");
                go.transform.SetParent(root, false);
                existing = go.transform;
            }

            existing.localPosition = new Vector3(0f, 0f, -10f);
            existing.localRotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);

            summary.AppendLine("- Ensured PlayerSpawn_Town at (0,0,-10) looking north.");
        }

        private static void AppendBuildingMerchantMap(StringBuilder summary)
        {
            if (summary == null) return;

            summary.AppendLine("- Building → Door → Merchant:");
            foreach (var kv in BuildingToDoor)
            {
                var building = kv.Key;
                var door = kv.Value;
                var merchant = DoorToMerchant.TryGetValue(door, out var m) ? m : "(none)";
                summary.AppendLine($"  - {building} → {door} → {merchant}");
            }
        }

        private static bool GetPeekEnabled()
        {
            return EditorPrefs.GetBool(PrefPeekEnabled, true);
        }

        private static void SetPeekEnabled(bool value)
        {
            EditorPrefs.SetBool(PrefPeekEnabled, value);
        }

        [InitializeOnLoadMethod]
        private static void InitPeekAndHighlights()
        {
            if (_peekHooksInstalled)
                return;

            _peekHooksInstalled = true;

            Selection.selectionChanged -= OnEditorSelectionChanged;
            Selection.selectionChanged += OnEditorSelectionChanged;

            SceneView.duringSceneGui -= OnEditorSceneGui;
            SceneView.duringSceneGui += OnEditorSceneGui;

            // In some cases Unity calls load hooks before SceneViews exist; delay ensures repaint works.
            EditorApplication.delayCall += () =>
            {
                try { SceneView.RepaintAll(); } catch { }
            };
        }

        [MenuItem(MenuPeekSelfTest)]
        public static void PeekHighlightSelfTest()
        {
            InitPeekAndHighlights();

            var sb = new StringBuilder();
            sb.AppendLine("[EdgevilleTownBuilder] Peek+Highlight Self Test");
            sb.AppendLine($"- PeekEnabled: {GetPeekEnabled()}");

            var root = FindInSceneByExactName(RootName);
            sb.AppendLine($"- Root '{RootName}': {(root != null ? "FOUND" : "MISSING")}");
            if (root != null)
            {
                sb.AppendLine($"- Root position: {root.transform.position}");
                sb.AppendLine($"- Has Buildings child: {(root.transform.Find("Buildings") != null)}");
                sb.AppendLine($"- Has Doors child: {(root.transform.Find("Doors") != null)}");
                sb.AppendLine($"- Has Merchants child: {(root.transform.Find("Merchants") != null)}");
            }

            var active = Selection.activeTransform;
            sb.AppendLine($"- Selection: {(active != null ? active.name : "(none)")}");
            var building = FindBuildingRoot(active);
            sb.AppendLine($"- BuildingRoot: {(building != null ? building.name : "(none)")}");

            if (building != null)
            {
                var roof = building.Find("Roof");
                sb.AppendLine($"- Roof child: {(roof != null ? (roof.gameObject.activeSelf ? "FOUND (active)" : "FOUND (inactive)") : "MISSING")}");
            }

            // Force the selection handler to run now.
            try { OnEditorSelectionChanged(); } catch { }
            try { SceneView.RepaintAll(); } catch { }

            Debug.Log(sb.ToString());
        }

        private static void EnsureParent(Transform child, Transform parent)
        {
            if (child == null || parent == null) return;
            if (child.parent == parent) return;
            Undo.SetTransformParent(child, parent, "Reparent object");
        }

        private static Transform CreateChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create child");
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static Transform FindOrCreateChild(Transform parent, string name)
        {
            var t = FindChildByExactName(parent, name);
            if (t != null) return t;
            return CreateChild(parent, name);
        }

        private static Transform CreateOrReplaceChild(Transform parent, string name)
        {
            var existing = FindChildByExactName(parent, name);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
            }

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create door marker");
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static Transform FindChildByExactName(Transform parent, string name)
        {
            if (parent == null || string.IsNullOrWhiteSpace(name)) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                var ch = parent.GetChild(i);
                if (ch != null && string.Equals(ch.name, name, StringComparison.Ordinal))
                    return ch;
            }
            return null;
        }

        private static GameObject FindInSceneByExactName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return null;

            var roots = scene.GetRootGameObjects();
            foreach (var r in roots)
            {
                if (r == null) continue;
                if (string.Equals(r.name, name, StringComparison.Ordinal))
                    return r;
            }

            return null;
        }

        private static GameObject FindInSceneByNameContains(string needle)
        {
            if (string.IsNullOrWhiteSpace(needle)) return null;

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return null;

            var roots = scene.GetRootGameObjects();
            foreach (var r in roots)
            {
                var found = FindInHierarchyByNameContains(r != null ? r.transform : null, needle);
                if (found != null)
                    return found.gameObject;
            }

            return null;
        }

        private static Transform FindInHierarchyByNameContains(Transform root, string needle)
        {
            if (root == null) return null;

            if (root.name != null && root.name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                var ch = root.GetChild(i);
                var found = FindInHierarchyByNameContains(ch, needle);
                if (found != null) return found;
            }

            return null;
        }

        private static void RemoveColliderIfAny(GameObject go)
        {
            if (go == null) return;

            try
            {
                var col = go.GetComponent<Collider>();
                if (col != null)
                    UnityEngine.Object.DestroyImmediate(col);
            }
            catch { }
        }

        private static void RemoveRendererIfAny(GameObject go)
        {
            if (go == null) return;

            try
            {
                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null) UnityEngine.Object.DestroyImmediate(mr);

                var mf = go.GetComponent<MeshFilter>();
                if (mf != null) UnityEngine.Object.DestroyImmediate(mf);
            }
            catch { }
        }

        private static Material TryFindMaterialByName(string materialName)
        {
            if (string.IsNullOrWhiteSpace(materialName)) return null;

            try
            {
                var guids = AssetDatabase.FindAssets(materialName + " t:Material");
                if (guids == null || guids.Length == 0)
                    return null;

                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<Material>(path);
            }
            catch
            {
                return null;
            }
        }

        private static Material TryFindMaterialByNames(params string[] materialNames)
        {
            if (materialNames == null || materialNames.Length == 0)
                return null;

            for (int i = 0; i < materialNames.Length; i++)
            {
                var n = materialNames[i];
                if (string.IsNullOrWhiteSpace(n)) continue;
                var m = TryFindMaterialByName(n);
                if (m != null) return m;
            }

            return null;
        }

        private static void ApplyMaterialToTown(Transform root, Material townMat, StringBuilder summary)
        {
            if (root == null || townMat == null)
                return;

            int changed = 0;

            try
            {
                var renderers = root.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers)
                {
                    if (r == null) continue;

                    // Skip TMP signage (it uses its own font/material pipeline)
                    if (HasComponentNamespacePrefix(r.gameObject, "TMPro."))
                        continue;

                    // Skip non-mesh renderers just in case
                    if (r is MeshRenderer || r is SkinnedMeshRenderer)
                    {
                        if (r.sharedMaterial != townMat)
                        {
                            r.sharedMaterial = townMat;
                            changed++;
                        }
                    }
                }
            }
            catch { }

            if (changed > 0)
                summary?.AppendLine($"- Applied 'World_Lit Base' to {changed} renderers.");
        }

        private static bool HasComponentNamespacePrefix(GameObject go, string prefix)
        {
            if (go == null || string.IsNullOrWhiteSpace(prefix)) return false;
            try
            {
                var comps = go.GetComponents<Component>();
                foreach (var c in comps)
                {
                    if (c == null) continue;
                    var t = c.GetType();
                    var full = t != null ? t.FullName : null;
                    if (!string.IsNullOrWhiteSpace(full) && full.StartsWith(prefix, StringComparison.Ordinal))
                        return true;
                }
            }
            catch { }
            return false;
        }

        private static void ApplySharedMaterial(GameObject go, Material mat)
        {
            if (go == null || mat == null) return;
            try
            {
                var r = go.GetComponent<Renderer>();
                if (r != null)
                    r.sharedMaterial = mat;
            }
            catch { }
        }

        // -------- Editor-only interaction: highlights + roof peek --------

        private static void OnEditorSelectionChanged()
        {
            if (!GetPeekEnabled())
            {
                RestorePeekVisuals();
                return;
            }

            var active = Selection.activeTransform;
            var building = FindBuildingRoot(active);
            if (building == _peekCurrentBuilding)
                return;

            RestorePeekVisuals();

            _peekCurrentBuilding = building;
            if (_peekCurrentBuilding == null)
                return;

            try
            {
                // Non-destructive hide (Scene visibility) so we don't accidentally save the roof disabled.
                var svm = SceneVisibilityManager.instance;
                if (svm != null)
                {
                    var roofTf = _peekCurrentBuilding.Find("Roof");
                    if (roofTf != null)
                    {
                        svm.Hide(roofTf.gameObject, false);
                        _peekHiddenObjects.Add(roofTf.gameObject);
                    }

                    // Also hide the shell so you can actually see inside.
                    var shellTf = _peekCurrentBuilding.Find("Shell");
                    if (shellTf != null)
                    {
                        svm.Hide(shellTf.gameObject, false);
                        _peekHiddenObjects.Add(shellTf.gameObject);
                    }
                }
            }
            catch { }

            SceneView.RepaintAll();
        }

        private static void RestorePeekVisuals()
        {
            try
            {
                var svm = SceneVisibilityManager.instance;
                if (svm != null)
                {
                    for (int i = 0; i < _peekHiddenObjects.Count; i++)
                    {
                        var go = _peekHiddenObjects[i];
                        if (go == null) continue;
                        svm.Show(go, false);
                    }
                }
            }
            catch { }

            _peekHiddenObjects.Clear();

            _peekCurrentBuilding = null;
            SceneView.RepaintAll();
        }

        private static Transform FindBuildingRoot(Transform t)
        {
            if (t == null) return null;

            // Only operate within the Edgeville hub.
            var root = FindInSceneByExactName(RootName);
            if (root == null) return null;

            var hubRoot = root.transform;

            Transform cur = t;
            while (cur != null)
            {
                if (cur == hubRoot)
                    return null;

                if (cur.name != null && cur.name.StartsWith("Building_", StringComparison.Ordinal))
                {
                    if (cur.IsChildOf(hubRoot))
                        return cur;
                    return null;
                }

                cur = cur.parent;
            }

            return null;
        }

        private static void OnEditorSceneGui(SceneView view)
        {
            if (!GetPeekEnabled())
                return;

            var root = FindInSceneByExactName(RootName);
            if (root == null)
                return;

            var doors = root.transform.Find("Doors");
            var buildings = root.transform.Find("Buildings");
            var merchants = root.transform.Find("Merchants");

            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

            // Buildings highlight
            if (buildings != null)
            {
                for (int i = 0; i < buildings.childCount; i++)
                {
                    var b = buildings.GetChild(i);
                    if (b == null) continue;

                    var shell = b.Find("Shell");
                    var r = shell != null ? shell.GetComponent<Renderer>() : b.GetComponentInChildren<Renderer>();
                    if (r == null) continue;

                    var isSelected = _peekCurrentBuilding != null && b == _peekCurrentBuilding;
                    Handles.color = isSelected ? new Color(1f, 0.85f, 0.2f, 0.9f) : new Color(0.2f, 0.75f, 1f, 0.35f);
                    Handles.DrawWireCube(r.bounds.center, r.bounds.size);
                }
            }

            // Doors highlight + labels
            if (doors != null)
            {
                for (int i = 0; i < doors.childCount; i++)
                {
                    var d = doors.GetChild(i);
                    if (d == null) continue;

                    Handles.color = new Color(0.3f, 1f, 0.3f, 0.9f);
                    Handles.SphereHandleCap(0, d.position + Vector3.up * 0.1f, Quaternion.identity, 0.35f, EventType.Repaint);
                    Handles.Label(d.position + Vector3.up * 0.6f, d.name);
                }
            }

            // ClickTargets highlight
            if (merchants != null)
            {
                for (int i = 0; i < merchants.childCount; i++)
                {
                    var m = merchants.GetChild(i);
                    if (m == null) continue;

                    var ct = m.Find("ClickTarget");
                    if (ct == null) continue;

                    var bc = ct.GetComponent<BoxCollider>();
                    if (bc == null) continue;

                    Handles.color = new Color(1f, 0.6f, 0.2f, 0.65f);
                    var worldCenter = ct.TransformPoint(bc.center);
                    var size = Vector3.Scale(bc.size, ct.lossyScale);
                    Handles.DrawWireCube(worldCenter, size);
                    Handles.Label(worldCenter + Vector3.up * (size.y * 0.6f), $"{m.name} ClickTarget");
                }
            }

            // Strong focus highlight for the selected building's action (door + click target)
            try
            {
                if (_peekCurrentBuilding != null && doors != null)
                {
                    if (BuildingToDoor.TryGetValue(_peekCurrentBuilding.name, out var doorName))
                    {
                        var doorTf = doors.Find(doorName);
                        if (doorTf != null)
                        {
                            Handles.color = new Color(1f, 1f, 0.2f, 0.95f);
                            Handles.SphereHandleCap(0, doorTf.position + Vector3.up * 0.15f, Quaternion.identity, 0.6f, EventType.Repaint);
                            Handles.Label(doorTf.position + Vector3.up * 0.9f, $"ACTION: {doorName}");

                            // Find mapped merchant click target for this door
                            if (DoorToMerchant.TryGetValue(doorName, out var merchantName) && merchants != null)
                            {
                                Transform merchantTf = null;
                                for (int i = 0; i < merchants.childCount; i++)
                                {
                                    var m = merchants.GetChild(i);
                                    if (m == null) continue;
                                    if (string.Equals(m.name, merchantName, StringComparison.OrdinalIgnoreCase))
                                    {
                                        merchantTf = m;
                                        break;
                                    }
                                }

                                var ct = merchantTf != null ? merchantTf.Find("ClickTarget") : null;
                                var bc = ct != null ? ct.GetComponent<BoxCollider>() : null;
                                if (ct != null && bc != null)
                                {
                                    var worldCenter = ct.TransformPoint(bc.center);
                                    Handles.color = new Color(1f, 0.55f, 0.1f, 0.95f);
                                    Handles.DrawAAPolyLine(6f, doorTf.position + Vector3.up * 0.15f, worldCenter);

                                    var size = Vector3.Scale(bc.size, ct.lossyScale);
                                    Handles.DrawWireCube(worldCenter, size);
                                    Handles.Label(worldCenter + Vector3.up * (size.y * 0.8f), $"CLICK HERE: {merchantName}");
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }
    }
}
#endif
