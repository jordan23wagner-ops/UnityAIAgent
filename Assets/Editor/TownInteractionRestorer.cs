using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abyss.EditorTools
{
    public static class TownInteractionRestorer
    {
        private const string MenuFix = "Tools/Abyss/Town/Fix Merchant Interactions (After Move)";
        private const string MenuValidate = "Tools/Abyss/Town/Validate Merchant Interactions";

        [MenuItem(MenuFix)]
        public static void FixMerchantInteractions()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[TownInteractionRestorer] No valid active scene.");
                return;
            }

            EnsureMerchantUiExists();
            EnsureMerchantWorldInteractionExists();

            int fixedByEdgeville = 0;
            try
            {
                // If the Edgeville hub exists, use the existing robust rebuild.
                if (GameObject.Find("EdgevilleHub_Root") != null)
                {
                    Abyss.EditorTools.EdgevilleTownBuilder.RebuildDoorsAndClickTargetsSafe();
                    fixedByEdgeville = 1;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[TownInteractionRestorer] Edgeville rebuild failed; falling back to generic fixer. " + e.Message);
            }

            int shops = 0;
            int createdTargets = 0;
            int updatedTargets = 0;
            int addedColliders = 0;
            int addedHighlighters = 0;

#if UNITY_2022_2_OR_NEWER
            var found = UnityEngine.Object.FindObjectsByType<Abyss.Shop.MerchantShop>(FindObjectsSortMode.None);
#else
            var found = UnityEngine.Object.FindObjectsOfType<Abyss.Shop.MerchantShop>();
#endif
            if (found != null)
            {
                foreach (var shop in found)
                {
                    if (shop == null) continue;
                    shops++;

                    // Ensure a collider exists somewhere under the shop so clicks/hovers work.
                    var anyCollider = shop.GetComponentInChildren<Collider>(true);
                    if (anyCollider == null)
                    {
                        var bc = Undo.AddComponent<BoxCollider>(shop.gameObject);
                        bc.isTrigger = false;
                        bc.center = new Vector3(0f, 1f, 0f);
                        bc.size = new Vector3(1.2f, 2.0f, 1.2f);
                        addedColliders++;
                    }
                    else
                    {
                        anyCollider.isTrigger = false;
                    }

                    // Ensure a dedicated click target exists (matches the builder name).
                    var clickTf = shop.transform.Find("BuildingClickTarget");
                    bool created = false;
                    if (clickTf == null)
                    {
                        var go = new GameObject("BuildingClickTarget");
                        Undo.RegisterCreatedObjectUndo(go, "Create BuildingClickTarget");
                        go.transform.SetParent(shop.transform, false);
                        clickTf = go.transform;
                        created = true;
                        createdTargets++;
                    }

                    // Determine bounds for sizing/positioning.
                    var renderers = shop.GetComponentsInChildren<Renderer>(true);
                    Bounds b;
                    bool hasBounds = TryGetBounds(renderers, out b);
                    if (!hasBounds)
                    {
                        // Fallback bounds around the shop transform.
                        b = new Bounds(shop.transform.position + Vector3.up, new Vector3(6f, 4f, 6f));
                    }

                    // Place click target at bounds center and keep it axis-aligned.
                    clickTf.position = b.center;
                    clickTf.rotation = Quaternion.identity;

                    // Remove any renderer on the click target (it should be invisible).
                    TryRemoveRenderer(clickTf.gameObject);

                    // Box collider fits the bounds.
                    var box = clickTf.GetComponent<BoxCollider>();
                    if (box == null) box = Undo.AddComponent<BoxCollider>(clickTf.gameObject);
                    box.isTrigger = false;
                    box.center = Vector3.zero;
                    box.size = b.size;

                    // Highlight hookup.
                    var target = clickTf.GetComponent<Abyss.Shop.MerchantDoorClickTarget>();
                    if (target == null)
                    {
                        target = Undo.AddComponent<Abyss.Shop.MerchantDoorClickTarget>(clickTf.gameObject);
                        addedHighlighters++;
                    }

                    if (renderers != null && renderers.Length > 0)
                    {
                        target.SetHighlightRenderers(renderers);
                        target.SetHighlightColor(Color.red);
                    }

                    // Layer
                    int interactableLayer = LayerMask.NameToLayer("Interactable");
                    if (interactableLayer >= 0)
                        clickTf.gameObject.layer = interactableLayer;

                    if (!created)
                        updatedTargets++;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log(
                $"[TownInteractionRestorer] Done. edgevilleRebuild={(fixedByEdgeville == 1)} shops={shops} " +
                $"createdTargets={createdTargets} updatedTargets={updatedTargets} addedColliders={addedColliders} addedHighlighters={addedHighlighters}"
            );
        }

        [MenuItem(MenuValidate)]
        public static void ValidateMerchantInteractions()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[TownInteractionRestorer] No valid active scene.");
                return;
            }

            int shops = 0;
            int missingUi = 0;
            int missingWorldRaycasters = 0;
            int missingClickTarget = 0;
            int missingClickCollider = 0;
            int missingHighlighter = 0;
            int disabledClickCollider = 0;
            int ignoreRaycastLayerTargets = 0;

#if UNITY_2022_2_OR_NEWER
            var ui = UnityEngine.Object.FindFirstObjectByType<Abyss.Shop.MerchantShopUI>();
#else
            var ui = UnityEngine.Object.FindObjectOfType<Abyss.Shop.MerchantShopUI>();
#endif
            if (ui == null) missingUi = 1;

            // These are runtime scripts spawned via RuntimeInitializeOnLoadMethod, but that can be skipped
            // when certain Enter Play Mode options are enabled. Validate presence so we can fix in-scene.
#if UNITY_2022_2_OR_NEWER
            var clickRaycaster = UnityEngine.Object.FindFirstObjectByType<Abyss.Shop.MerchantClickRaycaster>();
            var hoverHighlighter = UnityEngine.Object.FindFirstObjectByType<Abyss.Shop.MerchantDoorHoverHighlighter>();
#else
            var clickRaycaster = UnityEngine.Object.FindObjectOfType<Abyss.Shop.MerchantClickRaycaster>();
            var hoverHighlighter = UnityEngine.Object.FindObjectOfType<Abyss.Shop.MerchantDoorHoverHighlighter>();
#endif
            if (clickRaycaster == null || hoverHighlighter == null)
                missingWorldRaycasters = 1;

#if UNITY_2022_2_OR_NEWER
            var found = UnityEngine.Object.FindObjectsByType<Abyss.Shop.MerchantShop>(FindObjectsSortMode.None);
#else
            var found = UnityEngine.Object.FindObjectsOfType<Abyss.Shop.MerchantShop>();
#endif

            if (found != null)
            {
                foreach (var shop in found)
                {
                    if (shop == null) continue;
                    shops++;

                    var clickTf = shop.transform.Find("BuildingClickTarget");
                    if (clickTf == null)
                    {
                        missingClickTarget++;
                        continue;
                    }

                    var col = clickTf.GetComponent<Collider>();
                    if (col == null) missingClickCollider++;
                    else if (!col.enabled) disabledClickCollider++;

                    if (clickTf.gameObject.layer == 2) // Ignore Raycast
                        ignoreRaycastLayerTargets++;

                    var hi = clickTf.GetComponent<Abyss.Shop.MerchantDoorClickTarget>();
                    if (hi == null) missingHighlighter++;
                }
            }

            Debug.Log(
                $"[TownInteractionRestorer] Validate: shops={shops} missingUi={(missingUi == 1)} " +
                $"missingWorldRaycasters={(missingWorldRaycasters == 1)} " +
                $"missingClickTarget={missingClickTarget} missingClickCollider={missingClickCollider} disabledClickCollider={disabledClickCollider} " +
                $"missingHighlighter={missingHighlighter} ignoreRaycastLayerTargets={ignoreRaycastLayerTargets}\n" +
                "If any are missing, run: Tools/Abyss/Town/Fix Merchant Interactions (After Move)"
            );
        }

        private static void EnsureMerchantWorldInteractionExists()
        {
            // Ensure these exist in the active scene so merchant hover/click works regardless of
            // RuntimeInitializeOnLoadMethod behavior.
#if UNITY_2022_2_OR_NEWER
            var clickRaycaster = UnityEngine.Object.FindFirstObjectByType<Abyss.Shop.MerchantClickRaycaster>();
            var hoverHighlighter = UnityEngine.Object.FindFirstObjectByType<Abyss.Shop.MerchantDoorHoverHighlighter>();
#else
            var clickRaycaster = UnityEngine.Object.FindObjectOfType<Abyss.Shop.MerchantClickRaycaster>();
            var hoverHighlighter = UnityEngine.Object.FindObjectOfType<Abyss.Shop.MerchantDoorHoverHighlighter>();
#endif

            if (clickRaycaster != null && hoverHighlighter != null)
                return;

            var root = GameObject.Find("MerchantWorldInteraction");
            if (root == null)
            {
                root = new GameObject("MerchantWorldInteraction");
                Undo.RegisterCreatedObjectUndo(root, "Create MerchantWorldInteraction");
            }

            if (clickRaycaster == null)
            {
                Undo.AddComponent<Abyss.Shop.MerchantClickRaycaster>(root);
            }

            if (hoverHighlighter == null)
            {
                Undo.AddComponent<Abyss.Shop.MerchantDoorHoverHighlighter>(root);
            }

            Debug.Log("[TownInteractionRestorer] Ensured MerchantWorldInteraction (raycaster + hover highlighter)." );
        }

        private static void EnsureMerchantUiExists()
        {
#if UNITY_2022_2_OR_NEWER
            var ui = UnityEngine.Object.FindFirstObjectByType<Abyss.Shop.MerchantShopUI>();
#else
            var ui = UnityEngine.Object.FindObjectOfType<Abyss.Shop.MerchantShopUI>();
#endif
            if (ui != null)
                return;

            try
            {
                BuildMerchantShopUIEditor.BuildMerchantShopUI();
                Debug.Log("[TownInteractionRestorer] Built MerchantShopUI (was missing).");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[TownInteractionRestorer] MerchantShopUI missing and auto-build failed: " + e.Message);
            }
        }

        private static bool TryGetBounds(Renderer[] renderers, out Bounds bounds)
        {
            bounds = default;
            if (renderers == null || renderers.Length == 0)
                return false;

            bool hasAny = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                if (!hasAny) { bounds = r.bounds; hasAny = true; }
                else bounds.Encapsulate(r.bounds);
            }

            return hasAny;
        }

        private static void TryRemoveRenderer(GameObject go)
        {
            if (go == null) return;
            try
            {
                var r = go.GetComponent<Renderer>();
                if (r != null) UnityEngine.Object.DestroyImmediate(r);
            }
            catch { }
        }
    }
}
