using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.EventSystems;

using Object = UnityEngine.Object;

namespace Abyssbound.WorldInteraction
{
    public sealed class WorldInteractionRaycaster : MonoBehaviour
    {
        private const bool DebugDistanceGating = false;
        private const bool DEBUG_HOVER_HIT_LOG = false;

        private struct HitCache
        {
            public WorldInteractable target;
            public Collider collider;
            public Vector3 point;
            public float distance;

            public bool hasTargetPos;
            public Vector3 targetPos;
            public string colliderName;
        }

        [Header("Raycast")]
        [SerializeField] private Camera rayCamera;
        [SerializeField] private float maxDistance = 200f;

        // Uses DefaultRaycastLayers so built-in "Ignore Raycast" layer is excluded.
        [FormerlySerializedAs("raycastLayers")]
        [SerializeField] private LayerMask interactableMask = Physics.DefaultRaycastLayers;

        [Header("Debug")]
        [SerializeField] private bool debugHits = false;
        [SerializeField] private bool debugClicks = false;
        [SerializeField] private bool debugHover = false;
        [SerializeField] private bool debugClickResolve = false;
        [SerializeField] private bool debugHoverTrace = false;

        [Header("Interactor")]
        [SerializeField] private string playerTag = "Player";

        [Header("Wiring")]
        [SerializeField] private WorldHoverHighlighter hoverHighlighter;
        [SerializeField] private GameObject interactorOverride;

        private WorldInteractable _hovered;
        private WorldInteractable _lastHovered;
        private float _lastHoveredTime;

        private HitCache _hoverHit;
        private HitCache _lastHoverHit;

        private string _lastNearestColliderName;

        private WorldInteractable lastHoverLogged;
        private float nextHitLogTime;

        private readonly RaycastHit[] _hitsBuffer = new RaycastHit[32];

        private string _lastHoverHitLoggedKey;

        private string _lastHoverTraceKey;

        private string _lastInteractorSource;

        private void Awake()
        {
            if (rayCamera == null)
                rayCamera = Camera.main;

            if (hoverHighlighter == null)
            {
                hoverHighlighter = GetComponent<WorldHoverHighlighter>();
                if (hoverHighlighter == null)
                    hoverHighlighter = gameObject.AddComponent<WorldHoverHighlighter>();
            }
        }

        private void Reset()
        {
            rayCamera = Camera.main;
            hoverHighlighter = GetComponent<WorldHoverHighlighter>();
        }

        private void Update()
        {
            var cam = rayCamera != null ? rayCamera : Camera.main;
            if (cam == null)
                return;

            var ray = cam.ScreenPointToRay(Input.mousePosition);

            if (debugHits)
                Debug.DrawRay(ray.origin, ray.direction * maxDistance, Color.cyan);

            WorldInteractable best = null;
            float bestDistance = float.PositiveInfinity;
            Vector3 bestPoint = default;
            Collider bestCollider = null;
            Collider nearestCollider = null;
            float nearestColliderDistance = float.PositiveInfinity;

            var hitCount = Physics.RaycastNonAlloc(ray, _hitsBuffer, maxDistance, interactableMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < hitCount; i++)
            {
                var h = _hitsBuffer[i];
                if (h.collider == null)
                    continue;

                if (h.distance < nearestColliderDistance)
                {
                    nearestColliderDistance = h.distance;
                    nearestCollider = h.collider;
                }

                WorldInteractable candidate = h.collider.GetComponentInParent<WorldInteractable>();
                if (candidate == null)
                    candidate = h.collider.GetComponent<WorldInteractable>();

                // Merchant integration: merchants should participate in the SAME WorldInteractable hover pipeline
                // (WorldInteractable.GetHoverText()) rather than injecting external tooltips.
                if (candidate == null)
                {
                    try
                    {
                        var door = h.collider.GetComponentInParent<Abyss.Shop.MerchantDoorClickTarget>();
                        if (door != null)
                        {
                            var provider = door.GetComponent<Abyss.Shop.MerchantTooltipWorldInteractable>();
                            if (provider == null)
                                provider = door.gameObject.AddComponent<Abyss.Shop.MerchantTooltipWorldInteractable>();
                            candidate = provider;
                        }
                    }
                    catch
                    {
                        // ignore
                    }
                }
                if (candidate == null)
                    continue;

                if (h.distance < bestDistance)
                {
                    bestDistance = h.distance;
                    bestPoint = h.point;
                    bestCollider = h.collider;
                    best = candidate;
                }
            }

            // Merchants may be intentionally excluded from the WorldInteractable layer (scene validator protects them),
            // so they won't be hit by interactableMask when that mask is restricted.
            // Fallback: raycast all layers to find MerchantDoorClickTarget and resolve a WorldInteractable tooltip provider.
            if (best == null)
            {
                try
                {
                    int anyHitCount = Physics.RaycastNonAlloc(ray, _hitsBuffer, maxDistance, ~0, QueryTriggerInteraction.Collide);
                    Abyss.Shop.MerchantDoorClickTarget bestDoor = null;
                    RaycastHit bestDoorHit = default;
                    float bestDoorDist = float.PositiveInfinity;

                    for (int i = 0; i < anyHitCount; i++)
                    {
                        var h = _hitsBuffer[i];
                        if (h.collider == null) continue;

                        var door = h.collider.GetComponentInParent<Abyss.Shop.MerchantDoorClickTarget>();
                        if (door == null)
                            continue;

                        if (h.distance < bestDoorDist)
                        {
                            bestDoor = door;
                            bestDoorDist = h.distance;
                            bestDoorHit = h;
                        }
                    }

                    if (bestDoor != null)
                    {
                        var provider = bestDoor.GetComponent<Abyss.Shop.MerchantTooltipWorldInteractable>();
                        if (provider == null)
                            provider = bestDoor.gameObject.AddComponent<Abyss.Shop.MerchantTooltipWorldInteractable>();

                        best = provider;
                        bestDistance = bestDoorDist;
                        bestPoint = bestDoorHit.point;
                        bestCollider = bestDoorHit.collider;
                    }
                }
                catch
                {
                    // ignore
                }
            }

            _lastNearestColliderName = nearestCollider != null ? nearestCollider.name : null;

            if (debugHoverTrace)
            {
                var focusCollider = bestCollider != null ? bestCollider : nearestCollider;
                if (focusCollider == null)
                {
                    if (!string.Equals(_lastHoverTraceKey, "(no hit)", StringComparison.Ordinal))
                    {
                        _lastHoverTraceKey = "(no hit)";
                        UnityEngine.Debug.Log("[WorldInteractionTrace] hoverHit=<none>", this);
                    }
                }
                else
                {
                    string colliderName = string.Empty;
                    string rootName = string.Empty;
                    int layer = -1;
                    string layerName = string.Empty;
                    string hitPoint = string.Empty;
                    string resolvedInteractableType = "<none>";

                    try { colliderName = focusCollider.name; } catch { colliderName = "<unknown>"; }
                    try { rootName = focusCollider.transform != null && focusCollider.transform.root != null ? focusCollider.transform.root.name : "<no root>"; } catch { rootName = "<no root>"; }
                    try { layer = focusCollider.gameObject != null ? focusCollider.gameObject.layer : -1; } catch { layer = -1; }
                    try { layerName = layer >= 0 ? LayerMask.LayerToName(layer) : string.Empty; } catch { layerName = string.Empty; }
                    try { hitPoint = bestCollider != null ? bestPoint.ToString("F3") : "(n/a)"; } catch { hitPoint = "(n/a)"; }
                    try { resolvedInteractableType = best != null ? best.GetType().Name : "<none>"; } catch { resolvedInteractableType = "<error>"; }

                    var key = colliderName + "|" + rootName + "|" + layer + "|" + resolvedInteractableType;
                    if (!string.Equals(_lastHoverTraceKey, key, StringComparison.Ordinal))
                    {
                        _lastHoverTraceKey = key;

                        UnityEngine.Debug.Log($"[WorldInteractionTrace] hoverHit collider='{colliderName}' root='{rootName}' point={hitPoint} layer={layer}('{layerName}') interactableType={resolvedInteractableType}", this);

                        try
                        {
                            var mbs = focusCollider.GetComponents<MonoBehaviour>();
                            if (mbs == null || mbs.Length == 0)
                            {
                                UnityEngine.Debug.Log("[WorldInteractionTrace] colliderGO MonoBehaviours: <none>", this);
                            }
                            else
                            {
                                var names = new System.Text.StringBuilder();
                                for (int i = 0; i < mbs.Length; i++)
                                {
                                    if (i > 0) names.Append(", ");
                                    var mb = mbs[i];
                                    names.Append(mb != null ? mb.GetType().Name : "<missing script>");
                                }

                                UnityEngine.Debug.Log($"[WorldInteractionTrace] colliderGO MonoBehaviours: {names}", this);
                            }
                        }
                        catch
                        {
                            UnityEngine.Debug.Log("[WorldInteractionTrace] colliderGO MonoBehaviours: <error>", this);
                        }

                        try
                        {
                            var root = focusCollider.transform != null ? focusCollider.transform.root : null;
                            var rootGo = root != null ? root.gameObject : null;
                            if (rootGo == null)
                            {
                                UnityEngine.Debug.Log("[WorldInteractionTrace] rootGO MonoBehaviours: <no root>", this);
                            }
                            else
                            {
                                var mbs = rootGo.GetComponents<MonoBehaviour>();
                                if (mbs == null || mbs.Length == 0)
                                {
                                    UnityEngine.Debug.Log("[WorldInteractionTrace] rootGO MonoBehaviours: <none>", this);
                                }
                                else
                                {
                                    var names = new System.Text.StringBuilder();
                                    for (int i = 0; i < mbs.Length; i++)
                                    {
                                        if (i > 0) names.Append(", ");
                                        var mb = mbs[i];
                                        names.Append(mb != null ? mb.GetType().Name : "<missing script>");
                                    }

                                    UnityEngine.Debug.Log($"[WorldInteractionTrace] rootGO MonoBehaviours: {names}", this);
                                }
                            }
                        }
                        catch
                        {
                            UnityEngine.Debug.Log("[WorldInteractionTrace] rootGO MonoBehaviours: <error>", this);
                        }
                    }
                }
            }

            if (debugHits && Time.unscaledTime >= nextHitLogTime)
            {
                nextHitLogTime = Time.unscaledTime + 0.5f;
                if (nearestCollider != null)
                {
                    Debug.Log($"[WorldInteraction] Hit: {nearestCollider.name} (Layer={LayerMask.LayerToName(nearestCollider.gameObject.layer)})");
                }
                else
                {
                    Debug.Log("[WorldInteraction] Hit: no hits");
                }
            }

            if (DEBUG_HOVER_HIT_LOG)
            {
                var hitName = bestCollider != null ? bestCollider.name : (nearestCollider != null ? nearestCollider.name : "(no hit)");
                var interactableName = best != null ? best.name : "(null)";
                var hoverText = best != null ? best.GetHoverText() : string.Empty;
                var key = hitName + "|" + interactableName + "|" + hoverText;
                if (!string.Equals(_lastHoverHitLoggedKey, key, StringComparison.Ordinal))
                {
                    _lastHoverHitLoggedKey = key;
                    Debug.Log($"[Hover] hit={hitName} wi={interactableName} text={hoverText}");
                }
            }

            // Per-collider fishing tooltip: the interactable may live on a shared parent ([FishingSpots])
            // but we still want Shrimp/Trout based on the actual baked collider that was hit.
            if (Application.isPlaying && bestCollider != null)
            {
                if (best is FishingSpotInteractable fishing)
                {
                    var inferred = InferFishingSpotTypeFromColliderName(bestCollider.name);
                    if (!string.IsNullOrWhiteSpace(inferred))
                        fishing.SetSpotType(inferred);
                }
            }

            if (debugHover && best != lastHoverLogged)
            {
                lastHoverLogged = best;
                Debug.Log(best != null
                    ? $"[WorldInteraction] Hover: {best.name} ({best.GetHoverText()})"
                    : "[WorldInteraction] Hover: (none)");
            }

            // Track hovered and a short-lived buffered hovered target to keep clicks reliable.
            _hovered = best;
            if (best != null)
            {
                _lastHovered = best;
                _lastHoveredTime = Time.unscaledTime;

                bool hasTargetPos = false;
                Vector3 targetPos = default;
                string colliderName = null;
                if (bestCollider != null)
                {
                    try
                    {
                        targetPos = bestCollider.bounds.center;
                        colliderName = bestCollider.name;
                        hasTargetPos = true;
                    }
                    catch { hasTargetPos = false; }
                }

                _hoverHit = new HitCache
                {
                    target = best,
                    collider = bestCollider,
                    point = bestPoint,
                    distance = bestDistance
                    ,
                    hasTargetPos = hasTargetPos,
                    targetPos = targetPos,
                    colliderName = colliderName
                };

                _lastHoverHit = _hoverHit;
            }

            if (hoverHighlighter != null)
            {
                hoverHighlighter.UpdateHoverCandidate(best, bestPoint, bestDistance, cam);
            }

            if (Input.GetMouseButtonDown(0))
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                    return;

                if (debugClickResolve)
                {
                    var hoveredName = _hovered != null ? _hovered.name : "null";
                    var hitColliderName = !string.IsNullOrWhiteSpace(_lastNearestColliderName) ? _lastNearestColliderName : "(no hit)";
                    Debug.Log($"[WorldInteraction] Click hovered={hoveredName} hitCollider={hitColliderName}");
                }

                var (target, source, clickHit) = ResolveClickTarget(cam);
                if (target == null)
                {
                    if (debugClicks)
                        Debug.Log("[WorldInteraction] Click: no interactable");
                    return;
                }

                var interactor = ResolveInteractor();

                // Range gating should use the actual hovered/clicked collider bounds center (world), not the
                // interactable's transform. Some interactables live on grouping parents (e.g. [FishingSpots])
                // which makes transform.position incorrect for distance checks.
                var targetPos = ResolveTargetPosition(target, clickHit, out var targetPosSource, out var targetPosColliderName);
                var interactorPos = interactor != null ? interactor.transform.position : Vector3.zero;
                var dist = Vector3.Distance(targetPos, interactorPos);
                bool inRange = !target.RequiresRange || dist <= target.InteractionRange;

                if (debugClicks)
                {
                    var ipos = interactor != null ? interactor.transform.position : Vector3.zero;
                    var cname = string.IsNullOrWhiteSpace(targetPosColliderName) ? "(no collider)" : targetPosColliderName;
                    Debug.Log($"[WorldInteraction] Click({source}): interactable={target.name} collider={cname} interactor=({(_lastInteractorSource ?? "unknown")}) {interactor?.name} ipos={ipos} tpos={targetPos} dist={dist:0.0}m range={target.InteractionRange:0.0}m src={targetPosSource}");
                }

                if (!inRange)
                {
                    if (DebugDistanceGating || debugClicks)
                    {
                        Debug.Log($"[WorldInteraction] Click rejected by range. interactable={target.name} collider={targetPosColliderName} ipos={interactorPos} tpos={targetPos} dist={dist:0.0}m > {target.InteractionRange:0.0}m src={targetPosSource}", target);
                    }
                    return;
                }

                if (!target.CanInteract(interactor, out var reason))
                {
                    // Preserve tool gating and custom logic, but do NOT let the legacy range check
                    // (based on target.transform.position) reject interactions.
                    bool rangeOnlyRejection = !string.IsNullOrWhiteSpace(reason) && reason.IndexOf("Too far", System.StringComparison.OrdinalIgnoreCase) >= 0;
                    if (rangeOnlyRejection)
                    {
                        // We've already validated range using the hit collider center.
                        reason = null;
                    }
                    else
                    {
                        if (DebugDistanceGating || debugClicks)
                        {
                            var tpos = targetPos;
                            var ipos = interactorPos;
                            var cname = string.IsNullOrWhiteSpace(targetPosColliderName) ? "(no collider)" : targetPosColliderName;
                            Debug.Log($"[WorldInteraction] Click rejected by {target.name}. Reason='{reason}'. collider={cname} ipos={ipos} tpos={tpos} dist={dist:0.0}m src={targetPosSource}", target);
                        }

                        return;
                    }

                    if (debugClicks)
                    {
                        var cname = string.IsNullOrWhiteSpace(targetPosColliderName) ? "(no collider)" : targetPosColliderName;
                        Debug.Log($"[WorldInteraction] Click passed hit-range; ignoring base range rejection. interactable={target.name} collider={cname} ipos={interactorPos} tpos={targetPos} dist={dist:0.0}m src={targetPosSource}", target);
                    }
                }

                target.Interact(interactor);
            }
        }

        private (WorldInteractable target, string source, HitCache hit) ResolveClickTarget(Camera cam)
        {
            if (_hovered != null)
                return (_hovered, "hovered", _hoverHit);

            if (IsValidBufferedTarget(_lastHovered) && (Time.unscaledTime - _lastHoveredTime) <= 0.15f)
                return (_lastHovered, "buffered", _lastHoverHit);

            var ray = cam.ScreenPointToRay(Input.mousePosition);
            var hitCount = Physics.RaycastNonAlloc(ray, _hitsBuffer, maxDistance, interactableMask, QueryTriggerInteraction.Collide);
            WorldInteractable best = null;
            RaycastHit bestHit = default;
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                var h = _hitsBuffer[i];
                if (h.collider == null)
                    continue;

                var candidate = h.collider.GetComponentInParent<WorldInteractable>();
                if (candidate == null)
                    continue;

                if (h.distance < bestDistance)
                {
                    bestDistance = h.distance;
                    best = candidate;
                    bestHit = h;
                }
            }

            if (best != null)
            {
                bool hasTargetPos = false;
                Vector3 targetPos = default;
                string colliderName = null;
                if (bestHit.collider != null)
                {
                    try
                    {
                        targetPos = bestHit.collider.bounds.center;
                        colliderName = bestHit.collider.name;
                        hasTargetPos = true;
                    }
                    catch { hasTargetPos = false; }
                }

                return (best, "raycast", new HitCache
                {
                    target = best,
                    collider = bestHit.collider,
                    point = bestHit.point,
                    distance = bestHit.distance
                    ,
                    hasTargetPos = hasTargetPos,
                    targetPos = targetPos,
                    colliderName = colliderName
                });
            }

            return (null, "none", default);
        }

        private static bool IsValidBufferedTarget(WorldInteractable target)
        {
            return target != null && target.isActiveAndEnabled && target.gameObject.activeInHierarchy;
        }

        private static Vector3 ResolveTargetPosition(WorldInteractable interactable, HitCache hit, out string source, out string colliderName)
        {
            // 1) Always prefer the cached hover/click hit collider bounds center.
            if (hit.hasTargetPos)
            {
                source = "hoverHit";
                colliderName = hit.colliderName;
                return hit.targetPos;
            }

            // 2) Fallback: use the interactable's own colliders (child colliders), not its transform.
            if (interactable != null)
            {
                try
                {
                    var c = interactable.GetComponentInChildren<Collider>(true);
                    if (c != null)
                    {
                        source = "fallbackCollider";
                        colliderName = c.name;
                        return c.bounds.center;
                    }
                }
                catch { }
            }

            // 3) Last resort: transform position.
            source = "transform";
            colliderName = null;
            if (interactable != null)
            {
                try { return interactable.transform.position; }
                catch { }
            }

            return Vector3.zero;
        }

        private static string InferFishingSpotTypeFromColliderName(string colliderName)
        {
            if (string.IsNullOrWhiteSpace(colliderName))
                return null;

            // Zone1 baked fishing spot collider names.
            if (colliderName.IndexOf("FishingSpot_0", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Shrimp Spot";

            if (colliderName.IndexOf("FishingSpot_1", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Trout Spot";

            return null;
        }

        private GameObject ResolveInteractor()
        {
            _lastInteractorSource = null;

            if (interactorOverride != null)
            {
                _lastInteractorSource = "override";
                return interactorOverride;
            }

            // Prefer the project's authoritative player root over tag-based lookup.
            // If we fall back to this raycaster object (often at world origin), range checks can report
            // huge distances like ~430m even while standing next to the interactable.
            try
            {
#if UNITY_2022_2_OR_NEWER
                var auth = Object.FindFirstObjectByType<Game.Input.PlayerInputAuthority>();
#else
                var auth = Object.FindObjectOfType<Game.Input.PlayerInputAuthority>();
#endif
                if (auth != null)
                {
                    _lastInteractorSource = "PlayerInputAuthority";
                    return auth.gameObject;
                }
            }
            catch { }

            try
            {
#if UNITY_2022_2_OR_NEWER
                var stats = Object.FindFirstObjectByType<Abyssbound.Stats.PlayerStatsRuntime>();
#else
                var stats = Object.FindObjectOfType<Abyssbound.Stats.PlayerStatsRuntime>();
#endif
                if (stats != null)
                {
                    _lastInteractorSource = "PlayerStatsRuntime";
                    return stats.gameObject;
                }
            }
            catch { }

            GameObject player = null;
            try
            {
                if (!string.IsNullOrEmpty(playerTag))
                    player = GameObject.FindGameObjectWithTag(playerTag);
            }
            catch
            {
                player = null;
            }

            if (player != null)
            {
                _lastInteractorSource = "tag";
                return player;
            }

            _lastInteractorSource = "fallback";
            return gameObject;
        }

        public Camera RayCamera
        {
            get => rayCamera;
            set => rayCamera = value;
        }

        public LayerMask InteractableMask
        {
            get => interactableMask;
            set => interactableMask = value;
        }

        public bool DebugHover
        {
            get => debugHover;
            set => debugHover = value;
        }

        public bool DebugClicks
        {
            get => debugClicks;
            set => debugClicks = value;
        }

        public bool DebugHits
        {
            get => debugHits;
            set => debugHits = value;
        }

        public void SetHighlighter(WorldHoverHighlighter highlighter)
        {
            hoverHighlighter = highlighter;
        }
    }
}
