using System;
using UnityEngine;

namespace Abyss.Waypoints
{
    [DisallowMultipleComponent]
    public sealed class WaypointComponent : MonoBehaviour
    {
        public enum WaypointVisualStyle
        {
            PlatformOnly = 0,
            PlatformWithPillars = 1,
            StarOnly = 2,
        }

        private static readonly Color DefaultWaypointColor = new Color(0.15f, 0.95f, 0.95f, 1f); // cyan-ish
        private static readonly Color DefaultTownColor = new Color(0.95f, 0.80f, 0.15f, 1f); // gold-ish

        [Header("Waypoint")]
        [SerializeField] private string id;
        [SerializeField] private string displayName = "Waypoint";
        [SerializeField] private bool isTown;

        [Header("Registry")]
        [SerializeField] private WaypointRegistrySO registry;

        [Header("Visual")]
        public bool ShowVisual = true;
        public WaypointVisualStyle VisualStyle = WaypointVisualStyle.PlatformWithPillars;
        public Color VisualColor = default;

#if UNITY_EDITOR
        [Header("Runtime (debug)")]
        [SerializeField] private bool runtimeActivated;
        private float _nextRuntimePollTime;
#endif

        public string Id => id;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName;
        public bool IsTown => isTown;
        public WaypointRegistrySO Registry => registry;

        public Transform GetSpawnPoint()
        {
            var sp = transform.Find("SpawnPoint");
            return sp != null ? sp : transform;
        }

        private void Reset()
        {
            EnsureStableGuid();
            EnsureDefaultVisuals();
        }

        private void OnValidate()
        {
            EnsureStableGuid();
            EnsureDefaultVisuals();
        }

        private void EnsureStableGuid()
        {
            if (!string.IsNullOrWhiteSpace(id))
                return;

            id = Guid.NewGuid().ToString("N");
        }

        private void EnsureDefaultVisuals()
        {
            // Only assign a default if the color hasn't been set.
            if (VisualColor.a == 0f && VisualColor.r == 0f && VisualColor.g == 0f && VisualColor.b == 0f)
                VisualColor = isTown ? DefaultTownColor : DefaultWaypointColor;

            // If this was previously defaulted as a non-town waypoint but got flipped to town,
            // update to town color (still keeps custom colors intact).
            if (isTown && Approximately(VisualColor, DefaultWaypointColor))
                VisualColor = DefaultTownColor;
        }

        private static bool Approximately(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.001f && Mathf.Abs(a.g - b.g) < 0.001f && Mathf.Abs(a.b - b.b) < 0.001f && Mathf.Abs(a.a - b.a) < 0.001f;
        }

        private void OnEnable()
        {
            try { registry?.Register(this); } catch { }
        }

        private void OnDisable()
        {
            try { registry?.Unregister(this); } catch { }
        }

#if UNITY_EDITOR
        private void Update()
        {
            if (!Application.isPlaying)
                return;

            if (Time.unscaledTime < _nextRuntimePollTime)
                return;

            _nextRuntimePollTime = Time.unscaledTime + 0.5f;

            try
            {
                var mgr = WaypointManager.Instance;
                runtimeActivated = mgr != null && mgr.IsActivated(id);
            }
            catch { }
        }
#endif
    }
}
