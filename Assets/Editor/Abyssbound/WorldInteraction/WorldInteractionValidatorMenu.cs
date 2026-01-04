using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abyssbound.EditorTools.WorldInteraction
{
    public static class WorldInteractionValidatorMenu
    {
        private const string WorldInteractionRootName = "WorldInteraction";
        private const string WorldInteractableLayerName = "WorldInteractable";

        [MenuItem("Tools/Abyssbound/World Interaction/Validate & Fix Setup")]
        public static void ValidateAndFixSetup()
        {
            bool layersEnsured = EnsureLayerExists(WorldInteractableLayerName);

            int wiLayer = LayerMask.NameToLayer(WorldInteractableLayerName);
            if (wiLayer < 0)
            {
                Debug.LogWarning($"[WorldInteraction] Validate & Fix aborted: failed to ensure layer '{WorldInteractableLayerName}'.");
                return;
            }

            int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
            if (ignoreRaycastLayer < 0)
            {
                Debug.LogWarning("[WorldInteraction] Built-in layer 'Ignore Raycast' not found; continuing.");
            }

            bool worldInteractionCreatedOrUpdated = false;

            var worldInteractionGO = GameObject.Find(WorldInteractionRootName);
            if (worldInteractionGO == null)
            {
                worldInteractionGO = new GameObject(WorldInteractionRootName);
                Undo.RegisterCreatedObjectUndo(worldInteractionGO, "Create WorldInteraction");
                worldInteractionCreatedOrUpdated = true;
            }

            var raycaster = worldInteractionGO.GetComponent<Abyssbound.WorldInteraction.WorldInteractionRaycaster>();
            if (raycaster == null)
            {
                raycaster = Undo.AddComponent<Abyssbound.WorldInteraction.WorldInteractionRaycaster>(worldInteractionGO);
                worldInteractionCreatedOrUpdated = true;
            }

            var highlighter = worldInteractionGO.GetComponent<Abyssbound.WorldInteraction.WorldHoverHighlighter>();
            if (highlighter == null)
            {
                highlighter = Undo.AddComponent<Abyssbound.WorldInteraction.WorldHoverHighlighter>(worldInteractionGO);
                worldInteractionCreatedOrUpdated = true;
            }

            // Ensure raycaster references.
            int raycasterFixes = 0;
            if (raycaster != null)
            {
                if (raycaster.RayCamera == null)
                {
                    raycaster.RayCamera = Camera.main;
                    raycasterFixes++;
                }

                raycaster.SetHighlighter(highlighter);

                // Set mask to ONLY WorldInteractable layer.
                var desiredMask = 1 << wiLayer;
                if (raycaster.InteractableMask != desiredMask)
                {
                    raycaster.InteractableMask = desiredMask;
                    raycasterFixes++;
                }
            }

            // Validate interactables in scene.
            int interactablesUpdated = 0;
            int collidersAdded = 0;
            int layerFixesApplied = 0;
            int skippedMerchantProtected = 0;

            foreach (var mining in FindAllInLoadedScenes<Abyssbound.Mining.MiningNode>())
            {
                ProcessInteractable(mining.gameObject, wiLayer, ignoreRaycastLayer, ref interactablesUpdated, ref collidersAdded, ref layerFixesApplied, ref skippedMerchantProtected);
            }

            foreach (var forge in FindAllInLoadedScenes<Abyssbound.Smithing.ForgeStation>())
            {
                ProcessInteractable(forge.gameObject, wiLayer, ignoreRaycastLayer, ref interactablesUpdated, ref collidersAdded, ref layerFixesApplied, ref skippedMerchantProtected);
            }

            MarkSceneDirty();

            Debug.Log(
                "[WorldInteraction] Validate & Fix complete:\n" +
                $"- WorldInteraction object created/updated: {(worldInteractionCreatedOrUpdated ? "yes" : "no")}\n" +
                $"- Layers ensured: {(layersEnsured ? "yes" : "no")}\n" +
                $"- Raycaster fixes applied: {raycasterFixes}\n" +
                $"- Interactables updated: {interactablesUpdated}\n" +
                $"- Colliders added: {collidersAdded}\n" +
                $"- Layer fixes applied: {layerFixesApplied}\n" +
                $"- Skipped (merchant-protected): {skippedMerchantProtected}");
        }

        private static void ProcessInteractable(
            GameObject root,
            int worldInteractableLayer,
            int ignoreRaycastLayer,
            ref int interactablesUpdated,
            ref int collidersAdded,
            ref int layerFixesApplied,
            ref int skippedMerchantProtected)
        {
            if (root == null)
                return;

            if (IsMerchantProtected(root))
            {
                skippedMerchantProtected++;
                return;
            }

            bool changedAny = false;

            // Ensure not Ignore Raycast.
            if (ignoreRaycastLayer >= 0)
            {
                if (root.layer == ignoreRaycastLayer)
                {
                    Undo.RecordObject(root, "Fix interactable layer");
                    root.layer = worldInteractableLayer;
                    layerFixesApplied++;
                    changedAny = true;
                }
            }

            // Ensure layer is WorldInteractable.
            if (root.layer != worldInteractableLayer)
            {
                Undo.RecordObject(root, "Fix interactable layer");
                root.layer = worldInteractableLayer;
                layerFixesApplied++;
                changedAny = true;
            }

            // Ensure at least one collider in self/children.
            var colliders = root.GetComponentsInChildren<Collider>(true);
            if (colliders == null || colliders.Length == 0)
            {
                var sphere = Undo.AddComponent<SphereCollider>(root);
                sphere.isTrigger = true;
                sphere.radius = 1f;
                collidersAdded++;
                changedAny = true;
            }
            else
            {
                // Ensure any collider object isn't accidentally Ignore Raycast.
                if (ignoreRaycastLayer >= 0)
                {
                    for (int i = 0; i < colliders.Length; i++)
                    {
                        var c = colliders[i];
                        if (c == null) continue;

                        var go = c.gameObject;
                        if (go.layer == ignoreRaycastLayer)
                        {
                            Undo.RecordObject(go, "Fix collider layer");
                            go.layer = worldInteractableLayer;
                            layerFixesApplied++;
                            changedAny = true;
                        }
                    }
                }
            }

            if (changedAny)
                interactablesUpdated++;
        }

        private static bool IsMerchantProtected(GameObject go)
        {
            if (go == null)
                return false;

            var rootName = go.transform.root != null ? go.transform.root.name : string.Empty;
            if (string.Equals(rootName, "WeaponsGearMerchant", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rootName, "ConsumablesMerchant", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rootName, "SkillingSuppliesMerchant", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rootName, "WorkshopMerchant", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Type-name based hard exclusion.
            var components = go.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                var c = components[i];
                if (c == null) continue;

                var typeName = c.GetType().Name;
                if (typeName.IndexOf("Merchant", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

                if (typeName.IndexOf("MerchantShop", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

                if (typeName.IndexOf("MerchantWorldInteraction", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

                if (typeName.IndexOf("MerchantDoorClickTarget", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static bool EnsureLayerExists(string layerName)
        {
            if (LayerMask.NameToLayer(layerName) >= 0)
                return false;

            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning("[WorldInteraction] Could not load TagManager.asset");
                return false;
            }

            var tagManager = new SerializedObject(assets[0]);
            var layersProp = tagManager.FindProperty("layers");
            if (layersProp == null)
            {
                Debug.LogWarning("[WorldInteraction] TagManager layers property not found");
                return false;
            }

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
                    return true;
                }
            }

            Debug.LogWarning($"[WorldInteraction] No empty user layer slot to create '{layerName}'.");
            return false;
        }

        private static IEnumerable<T> FindAllInLoadedScenes<T>() where T : Component
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                foreach (var root in scene.GetRootGameObjects())
                {
                    var comps = root.GetComponentsInChildren<T>(true);
                    for (int c = 0; c < comps.Length; c++)
                    {
                        if (comps[c] != null)
                            yield return comps[c];
                    }
                }
            }
        }

        private static void MarkSceneDirty()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid())
                EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
