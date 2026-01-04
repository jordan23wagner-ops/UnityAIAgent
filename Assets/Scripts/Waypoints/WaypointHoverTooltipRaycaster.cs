using System.Collections.Generic;
using Abyssbound.WorldInteraction;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Abyss.Waypoints
{
    /// <summary>
    /// Hover-only tooltip support for 3D waypoint objects.
    /// Does not change waypoint activation/teleport logic and does not consume clicks.
    /// </summary>
    public sealed class WaypointHoverTooltipRaycaster : MonoBehaviour
    {
        [Header("Raycast")]
        [SerializeField] private Camera rayCamera;
        [SerializeField] private float maxDistance = 500f;

        [Header("Tooltip")]
        [SerializeField] private Vector2 tooltipScreenOffset = new Vector2(0f, 18f);

        [Header("Debug")]
        [SerializeField] private bool debugHover = false;

        private WaypointComponent _current;

        private void Awake()
        {
            if (rayCamera == null)
                rayCamera = Camera.main;
        }

        private void Update()
        {
            var cam = rayCamera != null ? rayCamera : Camera.main;
            if (cam == null)
                return;

            // If WorldInteraction already has a hovered target, let it own the tooltip.
            if (WorldHoverHighlighter.CurrentWorldHover != null)
            {
                Clear();
                return;
            }

            if (IsPointerOverInteractiveUI())
            {
                Clear();
                return;
            }

            var ray = cam.ScreenPointToRay(Input.mousePosition);
            var hits = Physics.RaycastAll(ray, maxDistance, ~0, QueryTriggerInteraction.Collide);
            if (hits == null || hits.Length == 0)
            {
                Clear();
                return;
            }

            WaypointComponent best = null;
            float bestDist = float.PositiveInfinity;
            Vector3 bestWorld = default;

            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.collider == null)
                    continue;

                var wp = hit.collider.GetComponentInParent<WaypointComponent>();
                if (wp == null)
                    wp = hit.collider.GetComponent<WaypointComponent>();

                if (wp == null)
                    continue;

                if (hit.distance < bestDist)
                {
                    best = wp;
                    bestDist = hit.distance;
                    bestWorld = hit.point;
                }
            }

            if (best == null)
            {
                Clear();
                return;
            }

            if (best != _current)
            {
                _current = best;
                if (debugHover)
                    Debug.Log($"[WaypointHover] wp={best.name} text='Waypoint: {best.DisplayName}'");
            }

            var sp = cam.WorldToScreenPoint(bestWorld);
            if (sp.z <= 0.01f)
            {
                Clear();
                return;
            }

            string name = best.DisplayName;
            if (string.IsNullOrWhiteSpace(name))
                name = best.gameObject != null ? best.gameObject.name : best.name;

            WorldHoverHighlighter.ShowExternal($"Waypoint: {name}", new Vector2(sp.x, sp.y) + tooltipScreenOffset);
        }

        private void Clear()
        {
            if (_current != null)
                _current = null;

            WorldHoverHighlighter.HideExternal();
        }

        private static bool IsPointerOverInteractiveUI()
        {
            var es = EventSystem.current;
            if (es == null) return false;

            if (!es.IsPointerOverGameObject())
                return false;

            var eventData = new PointerEventData(es)
            {
                position = Input.mousePosition
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var existing = FindAnyObjectByType<WaypointHoverTooltipRaycaster>();
            if (existing != null) return;

            var go = new GameObject("WaypointHoverTooltipRaycaster");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            go.AddComponent<WaypointHoverTooltipRaycaster>();
        }
    }
}
