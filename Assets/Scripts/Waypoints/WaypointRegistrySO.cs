using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abyss.Waypoints
{
    [CreateAssetMenu(menuName = "Abyss/Waypoints/Waypoint Registry", fileName = "WaypointRegistry")]
    public sealed class WaypointRegistrySO : ScriptableObject
    {
        // Runtime-only list of scene waypoints.
        [NonSerialized] private readonly List<WaypointComponent> _runtimeWaypoints = new();
        [NonSerialized] private readonly Dictionary<string, WaypointComponent> _byId = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<WaypointComponent> RuntimeWaypoints => _runtimeWaypoints;

        public void Register(WaypointComponent wp)
        {
            if (wp == null)
                return;

            var id = wp.Id;
            if (string.IsNullOrWhiteSpace(id))
                return;

            if (_byId.TryGetValue(id, out var existing) && existing != null)
            {
                if (existing == wp)
                    return;

                // Duplicate ID in-scene: keep the first registered to stay deterministic.
                return;
            }

            _byId[id] = wp;
            _runtimeWaypoints.Add(wp);
        }

        public void Unregister(WaypointComponent wp)
        {
            if (wp == null)
                return;

            var id = wp.Id;
            if (string.IsNullOrWhiteSpace(id))
                return;

            if (_byId.TryGetValue(id, out var existing) && existing == wp)
                _byId.Remove(id);

            _runtimeWaypoints.Remove(wp);
        }

        public bool TryGet(string id, out WaypointComponent wp)
        {
            wp = null;
            if (string.IsNullOrWhiteSpace(id))
                return false;

            return _byId.TryGetValue(id, out wp) && wp != null;
        }

        public WaypointComponent FindActivatedTown(HashSet<string> activatedIds)
        {
            if (activatedIds == null || activatedIds.Count == 0)
                return null;

            for (int i = 0; i < _runtimeWaypoints.Count; i++)
            {
                var wp = _runtimeWaypoints[i];
                if (wp == null) continue;
                if (!wp.IsTown) continue;
                if (string.IsNullOrWhiteSpace(wp.Id)) continue;
                if (activatedIds.Contains(wp.Id))
                    return wp;
            }

            return null;
        }

        public void ClearRuntime()
        {
            _runtimeWaypoints.Clear();
            _byId.Clear();
        }
    }
}
