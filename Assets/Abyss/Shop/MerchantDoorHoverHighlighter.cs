using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using Abyssbound.WorldInteraction;

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
        private UnityEngine.Object _current;
        private MerchantDoorClickTarget _currentDoor;
        private MerchantShop _currentShop;

        [Header("Debug")]
        [SerializeField] private bool logHover = false;

        private UnityEngine.Object _pending;
        private MerchantDoorClickTarget _pendingDoor;
        private MerchantShop _pendingShop;
        private float _pendingSince;
        private float _pendingBestDist;
        private int _pendingHitCount;

        private const float SwitchDebounceSeconds = 0.10f;

        private Game.Input.PlayerInputAuthority _input;

        // Legacy label (disabled when using unified tooltip)
        // NOTE: kept for backwards compatibility / inspector serialization.
        [SerializeField] private float labelFontSize = 14f;

        private static MerchantDoorHoverHighlighter s_instance;
        private UnityEngine.Object _lastHoverForTrace;
        private string _lastHoverNameForTrace;

        private void Awake()
        {
            s_instance = this;
            EnsureCamera();
#if UNITY_2022_2_OR_NEWER
            _input = FindFirstObjectByType<Game.Input.PlayerInputAuthority>();
#else
            _input = FindObjectOfType<Game.Input.PlayerInputAuthority>();
#endif
        }

        public static string DebugCurrentHoveredMerchantName
        {
            get
            {
                try { return s_instance != null ? s_instance._lastHoverNameForTrace : null; }
                catch { return null; }
            }
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

            // Choose ONE best hover target from all hits.
            // Prefer door click targets if present; otherwise fall back to MerchantShop so merchants can still tooltip.
            MerchantDoorClickTarget bestDoor = null;
            MerchantShop bestShop = null;
            float bestDoorDist = float.PositiveInfinity;
            float bestShopDist = float.PositiveInfinity;

            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.collider == null) continue;

                if (bestDoor == null || hit.distance < bestDoorDist)
                {
                    var t = hit.collider.GetComponent<MerchantDoorClickTarget>();
                    if (t == null)
                        t = hit.collider.GetComponentInParent<MerchantDoorClickTarget>();

                    if (t != null)
                    {
                        bestDoor = t;
                        bestDoorDist = hit.distance;
                        continue;
                    }
                }

                if (bestShop == null || hit.distance < bestShopDist)
                {
                    var shop = hit.collider.GetComponentInParent<MerchantShop>();
                    if (shop != null)
                    {
                        bestShop = shop;
                        bestShopDist = hit.distance;
                    }
                }
            }

            int hitCount = hits.Length;

            var bestObj = (UnityEngine.Object)(bestDoor != null ? bestDoor : (UnityEngine.Object)bestShop);
            float bestDist = bestDoor != null ? bestDoorDist : bestShopDist;

            // If we are still hovering the same target, keep the tooltip positioned near the cursor.
            if (bestObj != null && bestObj == _current)
            {
                _pending = null;
                TraceHoverIfChanged(bestObj, bestDoor, bestShop, traceSwitch: false);
                return;
            }

            // Debounce switching: require the new best to remain stable for 0.10s.
            if (bestObj != _pending)
            {
                _pending = bestObj;
                _pendingDoor = bestDoor;
                _pendingShop = bestShop;
                _pendingSince = Time.unscaledTime;
                _pendingBestDist = bestDist;
                _pendingHitCount = hitCount;
                return;
            }

            if (Time.unscaledTime - _pendingSince < SwitchDebounceSeconds)
            {
                return;
            }

            // Confirm switch.
            if (_currentDoor != null)
                _currentDoor.SetHighlighted(false);

            // Trace hover transitions based on the current->best switch.
            TraceHoverSwitch(_current, _currentDoor, _currentShop, bestObj, bestDoor, bestShop);

            _current = bestObj;
            _currentDoor = _pendingDoor;
            _currentShop = _pendingShop;
            _pending = null;
            _pendingDoor = null;
            _pendingShop = null;

            if (_currentDoor != null)
            {
                _currentDoor.SetHighlighted(true);
            }
            else
            {
                // No hover target.
            }

            if (logHover)
            {
                string bestName = _current != null ? _current.name : "<none>";
                float dist = float.IsPositiveInfinity(_pendingBestDist) ? -1f : _pendingBestDist;
                Debug.Log($"[Hover] best={bestName} dist={dist:0.###} hits={_pendingHitCount}");
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
            var prevObj = _current;
            var prevDoor = _currentDoor;
            var prevShop = _currentShop;

            if (_currentDoor != null)
            {
                _currentDoor.SetHighlighted(false);
            }

            _current = null;
            _currentDoor = null;
            _currentShop = null;

            _pending = null;
            _pendingDoor = null;
            _pendingShop = null;

            // Trace exit if we were hovering something.
            TraceHoverSwitch(prevObj, prevDoor, prevShop, null, null, null);
        }

        private void TraceHoverIfChanged(UnityEngine.Object bestObj, MerchantDoorClickTarget bestDoor, MerchantShop bestShop, bool traceSwitch)
        {
            if (!logHover)
                return;

            if (bestObj == _lastHoverForTrace)
                return;

            TraceHoverSwitch(_lastHoverForTrace, null, null, bestObj, bestDoor, bestShop);
        }

        private void TraceHoverSwitch(UnityEngine.Object fromObj, MerchantDoorClickTarget fromDoor, MerchantShop fromShop, UnityEngine.Object toObj, MerchantDoorClickTarget toDoor, MerchantShop toShop)
        {
            if (!logHover)
                return;

            string fromName = GetMerchantNameForTrace(fromDoor, fromShop, fromObj, out _);
            string toName = GetMerchantNameForTrace(toDoor, toShop, toObj, out string toHow);

            if (fromObj == null && toObj != null)
            {
                UnityEngine.Debug.Log($"[MerchantTrace] HOVER_ENTER name={toName} how={toHow}", this);
            }
            else if (fromObj != null && toObj == null)
            {
                UnityEngine.Debug.Log($"[MerchantTrace] HOVER_EXIT name={fromName}", this);
            }
            else if (fromObj != null && toObj != null)
            {
                UnityEngine.Debug.Log($"[MerchantTrace] HOVER_SWITCH from={fromName} to={toName}", this);
            }

            _lastHoverForTrace = toObj;
            _lastHoverNameForTrace = toName;
        }

        private static string GetMerchantNameForTrace(MerchantDoorClickTarget doorTarget, MerchantShop shopTarget, UnityEngine.Object obj, out string how)
        {
            how = "unknown";
            string name = null;

            try
            {
                if (doorTarget != null)
                {
                    name = doorTarget.GetDisplayName();
                    how = "doorTarget.GetDisplayName";
                }
                else if (shopTarget != null)
                {
                    name = shopTarget.MerchantName;
                    how = "shopTarget.MerchantName";
                }
            }
            catch
            {
                // ignore
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                try
                {
                    if (shopTarget != null && shopTarget.gameObject != null)
                    {
                        name = shopTarget.gameObject.name;
                        how = "shopTarget.gameObject.name";
                    }
                    else if (doorTarget != null && doorTarget.gameObject != null)
                    {
                        name = doorTarget.gameObject.name;
                        how = "doorTarget.gameObject.name";
                    }
                    else if (obj != null)
                    {
                        name = obj.name;
                        how = "obj.name";
                    }
                }
                catch
                {
                    // ignore
                }
            }

            if (string.IsNullOrWhiteSpace(name))
                name = "<unnamed>";

            return name;
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
