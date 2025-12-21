using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Abyss.Shop
{
    /// <summary>
    /// Global click handler that opens MerchantShop when you click any collider under it.
    /// Avoids Unity OnMouseDown quirks.
    /// </summary>
    public sealed class MerchantClickRaycaster : MonoBehaviour
    {
        private Camera _cam;
        private Game.Input.PlayerInputAuthority _input;

        private void Awake()
        {
            EnsureCamera();
#if UNITY_2022_2_OR_NEWER
            _input = FindFirstObjectByType<Game.Input.PlayerInputAuthority>();
#else
            _input = FindObjectOfType<Game.Input.PlayerInputAuthority>();
#endif
        }

        private void Update()
        {
            // Left click only
            if (!WasLeftClickPressed()) return;

            if (_input == null)
            {
#if UNITY_2022_2_OR_NEWER
                _input = FindFirstObjectByType<Game.Input.PlayerInputAuthority>();
#else
                _input = FindObjectOfType<Game.Input.PlayerInputAuthority>();
#endif
            }

            // If any UI has locked gameplay input (inventory/equipment/shop/etc), don't process world clicks.
            if (_input != null && _input.IsUiInputLocked)
                return;

            // If the shop UI is open, DO NOT raycast into the world (prevents immediate reopen on Exit click).
            if (MerchantShopUI.IsOpen) return;

            // If pointer is over interactive UI, do not raycast into the world.
            // (Non-interactive overlays like HUD panels should not block world interaction.)
            if (IsPointerOverInteractiveUI())
                return;

            EnsureCamera();
            if (_cam == null) return;

            if (!TryGetMousePosition(out var mousePos))
                return;

            var ray = _cam.ScreenPointToRay(mousePos);

            // RaycastAll so terrain/ground colliders can't block merchants after town moves.
            var hits = Physics.RaycastAll(ray, 500f, ~0, QueryTriggerInteraction.Collide);
            if (hits == null || hits.Length == 0)
                return;

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.collider == null) continue;

                // Walk up parent chain to find MerchantShop
                var shop = hit.collider.GetComponentInParent<MerchantShop>();
                if (shop == null) continue;

                // Open the inspector-driven UI with the resolved shop reference.
                MerchantShopUI.Open(shop);
                return;
            }
        }

        private void EnsureCamera()
        {
            if (_cam != null && _cam.isActiveAndEnabled)
                return;

            // Prefer a tagged main camera.
            var main = Camera.main;
            if (main != null && main.isActiveAndEnabled)
            {
                _cam = main;
                return;
            }

            // Otherwise, pick the enabled camera most likely to render world/interactables.
            var cams = Camera.allCameras;
            if (cams == null || cams.Length == 0)
            {
                _cam = null;
                return;
            }

            int defaultLayer = 0;
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            int desiredMask = 1 << defaultLayer;
            if (interactableLayer >= 0)
                desiredMask |= 1 << interactableLayer;

            Camera best = null;
            for (int i = 0; i < cams.Length; i++)
            {
                var c = cams[i];
                if (c == null || !c.isActiveAndEnabled) continue;
                if ((c.cullingMask & desiredMask) == 0) continue;
                if (best == null || c.depth > best.depth) best = c;
            }

            if (best == null)
            {
                for (int i = 0; i < cams.Length; i++)
                {
                    var c = cams[i];
                    if (c == null || !c.isActiveAndEnabled) continue;
                    if (best == null || c.depth > best.depth) best = c;
                }
            }

            _cam = best;
        }

        private static bool WasLeftClickPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
                return Mouse.current.leftButton.wasPressedThisFrame;
#endif
            return Input.GetMouseButtonDown(0);
        }

        private static bool TryGetMousePosition(out Vector2 pos)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                pos = Mouse.current.position.ReadValue();
                return true;
            }
#endif
            pos = Input.mousePosition;
            return true;
        }

        private static bool IsPointerOverInteractiveUI()
        {
            var es = EventSystem.current;
            if (es == null) return false;

            // Fast path: if not over any UI, we're fine.
            if (!es.IsPointerOverGameObject())
                return false;

            // If over UI, only block if the top UI under mouse is actually interactive.
            var eventData = new PointerEventData(es)
            {
                position = TryGetMousePosition(out var p) ? p : (Vector2)Input.mousePosition
            };

            var results = new List<RaycastResult>(16);
            es.RaycastAll(eventData, results);
            for (int i = 0; i < results.Count; i++)
            {
                var go = results[i].gameObject;
                if (go == null) continue;

                // Buttons/toggles/input fields/etc.
                if (go.GetComponentInParent<Selectable>() != null)
                    return true;
            }

            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var existing = FindAnyObjectByType<MerchantClickRaycaster>();
            if (existing != null) return;

            var go = new GameObject("MerchantClickRaycaster");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            go.AddComponent<MerchantClickRaycaster>();
        }
    }
}
