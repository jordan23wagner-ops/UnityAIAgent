using UnityEngine;

namespace Abyss.Waypoints
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class WaypointTrigger3D : MonoBehaviour
    {
        private WaypointComponent _waypoint;

        private void OnValidate()
        {
            try
            {
                var col = GetComponent<Collider>();
                if (col != null)
                    col.isTrigger = true;
            }
            catch { }
        }

        private void Awake()
        {
            _waypoint = GetComponent<WaypointComponent>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null)
                return;

            if (!other.CompareTag("Player"))
                return;

            var mgr = WaypointManager.Instance;
            if (mgr == null)
                return;

            if (_waypoint == null)
                _waypoint = GetComponent<WaypointComponent>();
            if (_waypoint == null)
                return;

            // Activation is id-based and must be stable across scenes/saves.
            mgr.Activate(_waypoint.Id, _waypoint);
        }
    }
}
