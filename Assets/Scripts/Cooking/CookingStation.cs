using System;
using System.Collections.Generic;
using Game.Systems;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Abyssbound.Cooking
{
    [DisallowMultipleComponent]
    public sealed class CookingStation : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private CookingUIController cookingUiPrefab;

        [Header("Recipes")]
        [SerializeField] private List<CookingRecipeSO> recipes = new List<CookingRecipeSO>();

        private Game.Input.PlayerInputAuthority _inputAuthority;
        private CookingUIController _uiInstance;
        private bool _uiOpenedLogged;

        private void Awake()
        {
            ResolveInputAuthority();
        }

        private void Update()
        {
            // Bonfire interaction is handled via click-to-interact WorldInteraction.
            // No local prompts, and no keypress activation.
        }

        public void Open()
        {
            try
            {
                var inv = PlayerInventoryResolver.GetOrFind();
                if (inv == null)
                    return;

                // If we already have an instance, just bring it forward.
                if (_uiInstance != null)
                {
                    _uiInstance.Show(recipes);
                    return;
                }

                // Prevent duplicate UI instances.
                var existing = FindFirstObjectByType<CookingUIController>(FindObjectsInactive.Include);
                if (existing != null)
                {
                    _uiInstance = existing;
                    try { _uiInstance.OnClosed -= HandleUiClosed; } catch { }
                    try { _uiInstance.OnClosed += HandleUiClosed; } catch { }
                    LockGameplayInput(true);
                    existing.Show(recipes);
                    return;
                }

                if (cookingUiPrefab == null)
                {
                    Debug.LogWarning("[Cooking] No Cooking UI prefab assigned.", this);
                    return;
                }

                var canvas = ResolveCanvas();
                if (canvas == null)
                {
                    canvas = CreateRuntimeCanvas();
                }

                EnsureEventSystem();

                var ui = Instantiate(cookingUiPrefab, canvas.transform);
                _uiInstance = ui;
                try { _uiInstance.OnClosed -= HandleUiClosed; } catch { }
                try { _uiInstance.OnClosed += HandleUiClosed; } catch { }

                LockGameplayInput(true);

                ui.Show(recipes);

                try
                {
                    ui.transform.SetAsLastSibling();
                }
                catch { }

                if (!_uiOpenedLogged)
                {
                    _uiOpenedLogged = true;
                    Debug.Log("[Cooking] UI opened");
                }
            }
            catch { }
        }

        private void HandleUiClosed()
        {
            _uiInstance = null;
            _uiOpenedLogged = false;
            LockGameplayInput(false);

            // Ensure no stuck focus after close.
            try { EventSystem.current?.SetSelectedGameObject(null); } catch { }
        }

        private void LockGameplayInput(bool locked)
        {
            ResolveInputAuthority();
            try { _inputAuthority?.SetUiInputLocked(locked); }
            catch { }
        }



        private void ResolveInputAuthority()
        {
            if (_inputAuthority != null)
                return;

            try
            {
#if UNITY_2022_2_OR_NEWER
                _inputAuthority = FindFirstObjectByType<Game.Input.PlayerInputAuthority>(FindObjectsInactive.Exclude);
#else
                _inputAuthority = FindObjectOfType<Game.Input.PlayerInputAuthority>();
#endif
            }
            catch { _inputAuthority = null; }
        }

        private bool TryResolvePlayerPosition(out Vector3 playerPos)
        {
            playerPos = Vector3.zero;

            ResolveInputAuthority();
            if (_inputAuthority != null)
            {
                playerPos = _inputAuthority.transform.position;
                return true;
            }

            try
            {
                var tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null)
                {
                    playerPos = tagged.transform.position;
                    return true;
                }
            }
            catch { }

            return false;
        }

        private static void EnsureEventSystem()
        {
            try
            {
                if (EventSystem.current != null)
                    return;

                var go = new GameObject("[EventSystem]", typeof(EventSystem));
                DontDestroyOnLoad(go);

                // Prefer the legacy StandaloneInputModule for broad compatibility.
                try { go.AddComponent<StandaloneInputModule>(); } catch { }
            }
            catch { }
        }

        private static Canvas ResolveCanvas()
        {
            try
            {
#if UNITY_2022_2_OR_NEWER
                var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
#else
                var canvases = FindObjectsOfType<Canvas>();
#endif
                if (canvases != null)
                {
                    for (int i = 0; i < canvases.Length; i++)
                    {
                        var c = canvases[i];
                        if (c == null) continue;
                        if (!c.gameObject.scene.IsValid()) continue;
                        if (!c.isActiveAndEnabled) continue;
                        return c;
                    }
                }
            }
            catch { }

            return null;
        }

        private static Canvas CreateRuntimeCanvas()
        {
            var go = new GameObject("[CookingCanvas]", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            DontDestroyOnLoad(go);

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            return canvas;
        }

        public void SetUiPrefab(CookingUIController prefab)
        {
            cookingUiPrefab = prefab;
        }

        public void SetRecipes(IEnumerable<CookingRecipeSO> recipeAssets)
        {
            recipes.Clear();
            if (recipeAssets == null)
                return;

            foreach (var r in recipeAssets)
            {
                if (r != null)
                    recipes.Add(r);
            }
        }
    }
}
