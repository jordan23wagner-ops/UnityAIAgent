using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abyssbound.EditorTools
{
    public static class WorldInteractionSetupMenu
    {
        private const string WorldInteractableLayerName = "WorldInteractable";

        [MenuItem("Tools/Abyssbound/World Interaction/Setup WorldInteraction System")]
        public static void SetupWorldInteractionSystem()
        {
            EnsureLayerExists(WorldInteractableLayerName);

            var root = GameObject.Find("WorldInteraction");
            if (root == null)
            {
                root = new GameObject("WorldInteraction");
                Undo.RegisterCreatedObjectUndo(root, "Create WorldInteraction");
            }

            var raycaster = root.GetComponent<Abyssbound.WorldInteraction.WorldInteractionRaycaster>();
            if (raycaster == null)
                raycaster = Undo.AddComponent<Abyssbound.WorldInteraction.WorldInteractionRaycaster>(root);

            var highlighter = root.GetComponent<Abyssbound.WorldInteraction.WorldHoverHighlighter>();
            if (highlighter == null)
                highlighter = Undo.AddComponent<Abyssbound.WorldInteraction.WorldHoverHighlighter>(root);

            if (raycaster != null)
            {
                if (raycaster.RayCamera == null)
                    raycaster.RayCamera = Camera.main;

                int layer = LayerMask.NameToLayer(WorldInteractableLayerName);
                if (layer >= 0)
                {
                    raycaster.InteractableMask = 1 << layer;
                }

                // Ensure it has a highlighter reference via existing wiring.
                raycaster.SetHighlighter(highlighter);
            }

            MarkSceneDirty();
            Debug.Log("[WorldInteraction] Setup complete.");
        }

        [MenuItem("Tools/Abyssbound/World Interaction/Create Sample Mining + Forge Area")]
        public static void CreateSampleMiningForgeArea()
        {
            EnsureLayerExists(WorldInteractableLayerName);
            int wiLayer = LayerMask.NameToLayer(WorldInteractableLayerName);
            if (wiLayer < 0)
            {
                Debug.LogWarning("[WorldInteraction] Missing layer 'WorldInteractable'. Run setup first.");
                return;
            }

            var parent = GameObject.Find("World_Interactables");
            if (parent == null)
            {
                parent = new GameObject("World_Interactables");
                Undo.RegisterCreatedObjectUndo(parent, "Create World_Interactables");
            }

            var placements = new Vector3[]
            {
                new Vector3(15f, 0f, 10f),
                new Vector3(17.5f, 0f, 12f),
                new Vector3(20f, 0f, 14f),
                new Vector3(18.5f, 0f, 10.5f),
            };

            for (int i = 0; i < 3; i++)
            {
                var node = CreateInteractableRoot($"Sample_MiningNode_{i + 1}", parent.transform, placements[i], wiLayer);
                var mining = Undo.AddComponent<Abyssbound.Mining.MiningNode>(node);
                ConfigureCommonVisual(node, isForge: false);
                ConfigureHighlightRenderers(mining, node);
            }

            {
                var forgeGO = CreateInteractableRoot("Sample_ForgeStation", parent.transform, placements[3], wiLayer);
                var forge = Undo.AddComponent<Abyssbound.Smithing.ForgeStation>(forgeGO);
                ConfigureCommonVisual(forgeGO, isForge: true);
                ConfigureHighlightRenderers(forge, forgeGO);
            }

            MarkSceneDirty();
            Debug.Log("[WorldInteraction] Created sample mining + forge area.");
        }

        [MenuItem("Tools/Abyssbound/World Interaction/Set Buildings To Ignore Raycast")]
        public static void SetBuildingsToIgnoreRaycast()
        {
            int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
            if (ignoreRaycastLayer < 0)
            {
                Debug.LogWarning("[WorldInteraction] Built-in layer 'Ignore Raycast' not found.");
                return;
            }

            int worldInteractableLayer = LayerMask.NameToLayer(WorldInteractableLayerName);

            var excludedRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "WeaponsGearMerchant",
                "ConsumablesMerchant",
                "SkillingSuppliesMerchant",
                "WorkshopMerchant",
            };

            var keywordList = new[]
            {
                "building", "house", "town", "inn", "shop", "forgebuilding", "forge building",
            };

            int changed = 0;
            int considered = 0;

            var scenes = GetLoadedScenes();
            foreach (var scene in scenes)
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    Traverse(root.transform, t =>
                    {
                        var go = t.gameObject;
                        if (!go.activeInHierarchy)
                            return;

                        if (excludedRoots.Contains(t.root.name))
                            return;

                        if (HasAbyssShopComponent(go))
                            return;

                        // Very conservative: require some visible geometry.
                        var meshRenderer = go.GetComponent<MeshRenderer>();
                        if (meshRenderer == null)
                            return;

                        bool isWorldInteractable = worldInteractableLayer >= 0 && go.layer == worldInteractableLayer;
                        bool hasWorldInteractableComponent = go.GetComponentInParent<Abyssbound.WorldInteraction.WorldInteractable>() != null;

                        bool nameMatches = ContainsAny(go.name, keywordList);
                        bool looksLikeScenery = nameMatches || (!isWorldInteractable && !hasWorldInteractableComponent);
                        if (!looksLikeScenery)
                            return;

                        considered++;

                        if (go.layer == ignoreRaycastLayer)
                            return;

                        Undo.RecordObject(go, "Set Ignore Raycast Layer");
                        SetLayerRecursively(go, ignoreRaycastLayer);
                        changed++;
                    });
                }
            }

            MarkSceneDirty();
            Debug.Log($"[WorldInteraction] Set Buildings To Ignore Raycast: changed {changed} roots (considered {considered}).");
        }

        private static GameObject CreateInteractableRoot(string name, Transform parent, Vector3 position, int layer)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");

            go.transform.SetParent(parent);
            go.transform.position = position;
            go.layer = layer;

            var sphere = Undo.AddComponent<SphereCollider>(go);
            sphere.isTrigger = true;
            sphere.radius = 1.25f;

            return go;
        }

        private static void ConfigureCommonVisual(GameObject root, bool isForge)
        {
            var visual = GameObject.CreatePrimitive(isForge ? PrimitiveType.Cube : PrimitiveType.Sphere);
            visual.name = "Visual";
            Undo.RegisterCreatedObjectUndo(visual, "Create Visual");

            visual.transform.SetParent(root.transform, worldPositionStays: false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = isForge ? new Vector3(1.2f, 1.0f, 1.2f) : new Vector3(1.2f, 1.2f, 1.2f);

            // Ensure root trigger is the interaction target.
            var col = visual.GetComponent<Collider>();
            if (col != null)
                UnityEngine.Object.DestroyImmediate(col);
        }

        private static void ConfigureHighlightRenderers(Component interactable, GameObject root)
        {
            if (interactable == null)
                return;

            var visual = root.transform.Find("Visual");
            if (visual == null)
                return;

            var renderer = visual.GetComponent<Renderer>();
            if (renderer == null)
                return;

            var so = new SerializedObject(interactable);
            var p = so.FindProperty("highlightRenderers");
            if (p != null && p.isArray)
            {
                p.arraySize = 1;
                p.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var display = so.FindProperty("displayName");
            if (display != null && string.IsNullOrWhiteSpace(display.stringValue))
            {
                display.stringValue = interactable is Abyssbound.Smithing.ForgeStation ? "Forge" : "Copper Rock";
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void EnsureLayerExists(string layerName)
        {
            if (LayerMask.NameToLayer(layerName) >= 0)
                return;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layersProp = tagManager.FindProperty("layers");

            for (int i = 8; i < layersProp.arraySize; i++)
            {
                var sp = layersProp.GetArrayElementAtIndex(i);
                if (sp == null)
                    continue;

                if (string.IsNullOrEmpty(sp.stringValue))
                {
                    sp.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    Debug.Log($"[WorldInteraction] Created layer '{layerName}' at index {i}.");
                    return;
                }
            }

            Debug.LogWarning($"[WorldInteraction] No empty user layer slot to create '{layerName}'.");
        }

        private static bool HasAbyssShopComponent(GameObject go)
        {
            // Exclude anything that has components in Abyss.Shop namespace.
            var components = go.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                var c = components[i];
                if (c == null) continue;

                var ns = c.GetType().Namespace;
                if (!string.IsNullOrEmpty(ns) && ns.StartsWith("Abyss.Shop", StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool ContainsAny(string s, string[] keywords)
        {
            if (string.IsNullOrEmpty(s))
                return false;

            for (int i = 0; i < keywords.Length; i++)
            {
                if (s.IndexOf(keywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            for (int i = 0; i < go.transform.childCount; i++)
            {
                SetLayerRecursively(go.transform.GetChild(i).gameObject, layer);
            }
        }

        private static void Traverse(Transform root, Action<Transform> visit)
        {
            visit(root);
            for (int i = 0; i < root.childCount; i++)
                Traverse(root.GetChild(i), visit);
        }

        private static List<Scene> GetLoadedScenes()
        {
            var list = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.IsValid() && s.isLoaded)
                    list.Add(s);
            }

            return list;
        }

        private static void MarkSceneDirty()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid())
                EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
