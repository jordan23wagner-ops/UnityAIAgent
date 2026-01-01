using System;
using System.Reflection;
using Abyss.Waypoints;
using UnityEngine;

namespace Abyssbound.Threat
{
    [DisallowMultipleComponent]
    public sealed class ThreatDistanceProvider : MonoBehaviour
    {
        public static ThreatDistanceProvider Instance { get; private set; }

        private const string FarthestPrefsKey = "Abyssbound.Threat.FarthestDistanceMeters";

        [Header("Runtime")]
        [SerializeField, Tooltip("Distance updates per second (not every frame).")]
        private float updatesPerSecond = 8f;

        public float CurrentDistanceMeters { get; private set; }
        public float FarthestDistanceMeters { get; private set; }

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

            try { FarthestDistanceMeters = Mathf.Max(0f, PlayerPrefs.GetFloat(FarthestPrefsKey, 0f)); }
            catch { FarthestDistanceMeters = 0f; }
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

            Vector3 townAnchor = ResolveTownAnchorPosition();
            Vector3 p = _player.transform.position;

            float distance = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(townAnchor.x, townAnchor.z));
            CurrentDistanceMeters = Mathf.Max(0f, distance);

            if (CurrentDistanceMeters > FarthestDistanceMeters)
            {
                FarthestDistanceMeters = CurrentDistanceMeters;
                try { PlayerPrefs.SetFloat(FarthestPrefsKey, FarthestDistanceMeters); } catch { }
            }
        }

        public void ResetFarthestDistance()
        {
            FarthestDistanceMeters = 0f;
            try { PlayerPrefs.SetFloat(FarthestPrefsKey, 0f); } catch { }
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
                WarnOnce(ref _warnedMissingPlayer, "[Threat] No GameObject tagged 'Player' found; distance will not update.");
        }

        private Vector3 ResolveTownAnchorPosition()
        {
            // 1) Activated town waypoint spawn if available.
            try
            {
                var mgr = WaypointManager.Instance;
                if (mgr != null)
                {
                    var townWp = TryInvokePrivate<WaypointComponent>(mgr, "ResolveTownWaypoint");
                    if (townWp != null)
                        return GetWaypointSpawnPosition(townWp);

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
    }
}
