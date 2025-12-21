using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Abyss.Shop
{
    /// <summary>
    /// Runtime hover highlight: only highlights door click targets (not whole buildings).
    /// </summary>
    public sealed class MerchantDoorHoverHighlighter : MonoBehaviour
    {
        private Camera _cam;
        private MerchantDoorClickTarget _current;

        private Game.Input.PlayerInputAuthority _input;

        private TextMeshPro _label;
        private static readonly Color LabelColor = Color.blue;
        [SerializeField] private float labelFontSize = 14f;

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
            if (_input == null)
            {
#if UNITY_2022_2_OR_NEWER
                _input = FindFirstObjectByType<Game.Input.PlayerInputAuthority>();
#else
                _input = FindObjectOfType<Game.Input.PlayerInputAuthority>();
#endif
            }

            // If any UI has locked gameplay input (inventory/equipment/shop/etc), don't show world hover labels.
            if (_input != null && _input.IsUiInputLocked)
            {
                Clear();
                return;
            }

            // If the shop UI is open, don't highlight anything.
            if (MerchantShopUI.IsOpen)
            {
                Clear();
                return;
            }

            // If pointer is over interactive UI, don't highlight world.
            // (Non-interactive overlays like HUD panels should not block highlighting.)
            if (IsPointerOverInteractiveUI())
            {
                Clear();
                return;
            }

            EnsureCamera();
            if (_cam == null) return;

            if (!TryGetMousePosition(out var mousePos))
                return;

            var ray = _cam.ScreenPointToRay(mousePos);

            // RaycastAll so nearby colliders (ground/terrain) don't block hover targets.
            var hits = Physics.RaycastAll(ray, 500f, ~0, QueryTriggerInteraction.Collide);
            if (hits == null || hits.Length == 0)
            {
                Clear();
                return;
            }

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            MerchantDoorClickTarget target = null;
            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.collider == null) continue;

                // Prefer direct component on collider object.
                target = hit.collider.GetComponent<MerchantDoorClickTarget>();
                if (target == null)
                    target = hit.collider.GetComponentInParent<MerchantDoorClickTarget>();

                if (target != null)
                    break;
            }
            if (target == _current) return;

            if (_current != null)
                _current.SetHighlighted(false);

            _current = target;

            if (_current != null)
            {
                _current.SetHighlighted(true);
                ShowLabelFor(_current);
            }
            else
            {
                HideLabel();
            }
        }

        private void EnsureCamera()
        {
            if (_cam != null && _cam.isActiveAndEnabled)
                return;

            var main = Camera.main;
            if (main != null && main.isActiveAndEnabled)
            {
                _cam = main;
                return;
            }

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

            if (!es.IsPointerOverGameObject())
                return false;

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

                if (go.GetComponentInParent<Selectable>() != null)
                    return true;
            }

            return false;
        }

        private void Clear()
        {
            if (_current != null)
            {
                _current.SetHighlighted(false);
                _current = null;
            }

            HideLabel();
        }

        private void EnsureLabel()
        {
            if (_label != null) return;

            var go = new GameObject("MerchantHoverLabel");
            go.transform.SetParent(transform, false);
            _label = go.AddComponent<TextMeshPro>();
            _label.text = string.Empty;
            _label.fontSize = labelFontSize;
            _label.alignment = TextAlignmentOptions.Center;
            _label.color = LabelColor;
            _label.textWrappingMode = TextWrappingModes.NoWrap;
            _label.gameObject.SetActive(false);
        }

        private void ShowLabelFor(MerchantDoorClickTarget target)
        {
            EnsureLabel();

            string name = target != null ? target.GetDisplayName() : string.Empty;
            if (string.IsNullOrWhiteSpace(name))
                name = "Shop";

            _label.text = name;

            if (target != null && target.TryGetBounds(out var b))
            {
                var p = b.center;
                p.y = b.max.y + 0.6f;
                _label.transform.position = p;
            }

            _label.gameObject.SetActive(true);
        }

        private void HideLabel()
        {
            if (_label != null)
                _label.gameObject.SetActive(false);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var existing = FindAnyObjectByType<MerchantDoorHoverHighlighter>();
            if (existing != null) return;

            var go = new GameObject("MerchantDoorHoverHighlighter");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            go.AddComponent<MerchantDoorHoverHighlighter>();
        }
    }
}
