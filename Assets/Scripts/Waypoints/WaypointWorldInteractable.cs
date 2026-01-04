using UnityEngine;
using Abyssbound.WorldInteraction;

namespace Abyss.Waypoints
{
    [DisallowMultipleComponent]
    public sealed class WaypointWorldInteractable : WorldInteractable
    {
        private WaypointComponent _wp;

        private void Awake()
        {
            _wp = GetComponent<WaypointComponent>();

            // Runtime-added components won't get Reset(); ensure highlight renderers are populated.
            try
            {
                var existing = HighlightRenderers;
                if (existing == null || existing.Length == 0)
                {
                    var rs = GetComponentsInChildren<Renderer>(includeInactive: true);
                    if (rs != null && rs.Length > 0)
                        SetHighlightRenderers(rs);
                }
            }
            catch { }

            // Keep name stable.
            try
            {
                if (_wp != null)
                    SetDisplayName(_wp.DisplayName);
            }
            catch { }
        }

        private void Reset()
        {
            SetDisplayName("Waypoint");
            SetRequiresRange(true);
            SetInteractionRange(3f);

            try
            {
                var rs = GetComponentsInChildren<Renderer>(includeInactive: true);
                if (rs != null && rs.Length > 0)
                    SetHighlightRenderers(rs);
            }
            catch { }
        }

        public override string GetHoverText()
        {
            if (_wp == null)
                _wp = GetComponent<WaypointComponent>();

            var name = _wp != null ? _wp.DisplayName : null;
            if (string.IsNullOrWhiteSpace(name))
                name = gameObject != null ? gameObject.name : "Waypoint";

            return $"Waypoint: {name}";
        }

        public override void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor, out _))
                return;

            var mgr = WaypointManager.Instance;
            if (mgr != null)
            {
                mgr.OpenMenu();
                return;
            }

            Debug.Log($"[Waypoint] Clicked {GetHoverText()} (placeholder)", this);
        }
    }
}
