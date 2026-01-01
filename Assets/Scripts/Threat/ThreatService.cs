using System;
using System.Reflection;
using Abyss.Waypoints;
using UnityEngine;

namespace Abyssbound.Threat
{
    [DisallowMultipleComponent]
    public sealed class ThreatService : MonoBehaviour
    {
        public static ThreatService Instance { get; private set; }

        [Header("Config")]
        [SerializeField] private float maxThreat = 5.0f;
        [SerializeField] private float step = 0.5f;
        [SerializeField] private float metersPerThreat = 50f;

        [Header("Distance Config (optional)")]
        [SerializeField] private ThreatDistanceConfigSO distanceConfig;
        [SerializeField] private ThreatDistanceProvider distanceProvider;

        [Header("Visual")]
        [SerializeField] private Gradient threatGradient;

        [Header("Runtime")]
        [SerializeField, Tooltip("Threat updates per second (not every frame).")]
        private float updatesPerSecond = 4f;

        public float CurrentThreat { get; private set; }
        public int CurrentThreatStepIndex { get; private set; }
        public Color CurrentThreatColor { get; private set; } = Color.white;

        public float CurrentDistanceMeters { get; private set; }
        public float FarthestDistanceMeters { get; private set; }

        public event Action<float> OnThreatChanged;

        private GameObject _player;
        private float _nextUpdateTime;

        private bool _warnedMissingPlayer;
        private bool _warnedUsingZeroTownAnchor;

        private static readonly BindingFlags ReflectFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (threatGradient == null)
                threatGradient = ThreatMath.CreateDefaultThreatGradient();

            TryResolveDistanceProvider();
            ApplyDistanceConfigToRuntimeFields();

            // Initialize state.
            CurrentThreat = 0f;
            CurrentThreatStepIndex = 0;
            CurrentThreatColor = ThreatMath.EvaluateThreatColor(threatGradient, 0f, maxThreat);
            CurrentDistanceMeters = 0f;
            FarthestDistanceMeters = 0f;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            float interval = updatesPerSecond <= 0f ? 0.25f : (1f / Mathf.Max(0.1f, updatesPerSecond));
            if (Time.unscaledTime < _nextUpdateTime)
                return;

            _nextUpdateTime = Time.unscaledTime + interval;

            EnsurePlayer();
            if (_player == null)
                return;

            TryResolveDistanceProvider();

            float distance = GetCurrentDistanceMeters();
            CurrentDistanceMeters = distance;
            FarthestDistanceMeters = Mathf.Max(FarthestDistanceMeters, distance);

            ApplyDistanceConfigToRuntimeFields();

            float quantized = QuantizeThreat(distance);
            int stepIndex = ThreatMath.ThreatToStepIndex(quantized, step, maxThreat);
            Color color = ThreatMath.EvaluateThreatColor(threatGradient, quantized, maxThreat);

            bool changed = !Mathf.Approximately(CurrentThreat, quantized) || stepIndex != CurrentThreatStepIndex;

            CurrentThreat = quantized;
            CurrentThreatStepIndex = stepIndex;
            CurrentThreatColor = color;

            if (changed)
            {
                try { OnThreatChanged?.Invoke(CurrentThreat); } catch { }
            }
        }

        private void EnsurePlayer()
        {
            if (_player != null)
                return;

            try
            {
                _player = GameObject.FindGameObjectWithTag("Player");
            }
            catch
            {
                _player = null;
            }

            if (_player == null)
                WarnOnce(ref _warnedMissingPlayer, "[Threat] No GameObject tagged 'Player' found; threat will not update.");
        }

        private Vector3 ResolveTownAnchorPosition()
        {
            // 1) Activated town waypoint spawn if available.
            try
            {
                var mgr = WaypointManager.Instance;
                if (mgr != null)
                {
                    // Preferred: internal resolver if present.
                    var townWp = TryInvokePrivate<WaypointComponent>(mgr, "ResolveTownWaypoint");
                    if (townWp != null)
                        return GetWaypointSpawnPosition(townWp);

                    // Best-effort fallback: scan for activated town waypoint.
                    var all = UnityEngine.Object.FindObjectsByType<WaypointComponent>(FindObjectsSortMode.None);
                    if (all != null)
                    {
                        for (int i = 0; i < all.Length; i++)
                        {
                            var wp = all[i];
                            if (wp == null) continue;
                            if (!wp.IsTown) continue;

                            try
                            {
                                if (mgr.IsActivated(wp.Id))
                                    return GetWaypointSpawnPosition(wp);
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }

            // 2) Tagged TownSpawn.
            try
            {
                var go = GameObject.FindGameObjectWithTag("TownSpawn");
                if (go != null)
                    return go.transform.position;
            }
            catch { }

            // 3) Zero with warn-once.
            WarnOnce(ref _warnedUsingZeroTownAnchor, "[Threat] Town anchor not resolved (waypoints/town tag missing). Using Vector3.zero.");
            return Vector3.zero;
        }

        private static Vector3 GetWaypointSpawnPosition(WaypointComponent wp)
        {
            if (wp == null)
                return Vector3.zero;

            try
            {
                var sp = wp.GetSpawnPoint();
                if (sp != null)
                    return sp.position;
            }
            catch { }

            try { return wp.transform.position; } catch { return Vector3.zero; }
        }

        private static T TryInvokePrivate<T>(object target, string methodName) where T : class
        {
            if (target == null || string.IsNullOrWhiteSpace(methodName))
                return null;

            try
            {
                var mi = target.GetType().GetMethod(methodName, ReflectFlags);
                if (mi == null)
                    return null;

                return mi.Invoke(target, null) as T;
            }
            catch
            {
                return null;
            }
        }

        private static void WarnOnce(ref bool gate, string msg)
        {
            if (gate)
                return;
            gate = true;
            try { Debug.LogWarning(msg); } catch { }
        }

        private void TryResolveDistanceProvider()
        {
            if (distanceProvider != null)
                return;

            try
            {
#if UNITY_2022_2_OR_NEWER
                distanceProvider = UnityEngine.Object.FindFirstObjectByType<ThreatDistanceProvider>(FindObjectsInactive.Exclude);
#else
                distanceProvider = UnityEngine.Object.FindObjectOfType<ThreatDistanceProvider>();
#endif
            }
            catch
            {
                distanceProvider = null;
            }
        }

        private void ApplyDistanceConfigToRuntimeFields()
        {
            if (distanceConfig == null)
                return;

            try
            {
                step = Mathf.Max(0.01f, distanceConfig.step);
                maxThreat = Mathf.Max(0f, distanceConfig.MaxThreat);
            }
            catch { }
        }

        private float GetCurrentDistanceMeters()
        {
            if (distanceProvider != null)
            {
                try { return Mathf.Max(0f, distanceProvider.CurrentDistanceMeters); }
                catch { }
            }

            Vector3 townAnchor = ResolveTownAnchorPosition();
            Vector3 p = _player.transform.position;
            float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(townAnchor.x, townAnchor.z));
            return Mathf.Max(0f, d);
        }

        private float QuantizeThreat(float distanceMeters)
        {
            if (distanceConfig != null)
            {
                try { return distanceConfig.EvaluateThreat(distanceMeters); }
                catch { }
            }

            return ThreatMath.QuantizeClampThreat(distanceMeters, metersPerThreat, step, maxThreat);
        }
    }
}
