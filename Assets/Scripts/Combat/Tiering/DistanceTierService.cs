using System;
using UnityEngine;

namespace Abyssbound.Combat.Tiering
{
    /// <summary>
    /// Computes distance from a configured Town origin and maps that distance to an enemy tier.
    /// This component must be explicitly wired (no global scene searches).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DistanceTierService : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform townOrigin;
        [SerializeField] private EnemyTierConfigSO config;

        /// <summary>
        /// Computes 2D distance (XZ plane) from town origin to a player position.
        /// Returns 0 if townOrigin is not assigned.
        /// </summary>
        public float GetDistance(Vector3 playerPosition)
        {
            if (townOrigin == null)
                return 0f;

            Vector3 a = townOrigin.position;
            Vector3 b = playerPosition;
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        /// <summary>
        /// Returns a 1-based tier number (1..N). Defaults to 1 if config is missing or invalid.
        /// </summary>
        public int GetTierIndex(float distance)
        {
            var def = GetTierDefinition(distance);

            // If config missing, GetTierDefinition returns default (tier 1 semantics).
            if (config == null || config.tiers == null || config.tiers.Count == 0)
                return 1;

            // Find matching index.
            for (int i = 0; i < config.tiers.Count; i++)
            {
                var t = config.tiers[i];
                if (Matches(distance, t))
                    return i + 1;
            }

            // If none match, fall back to tier 1.
            return 1;
        }

        /// <summary>
        /// Returns the tier definition for the given distance.
        /// Defaults to Tier 1 multipliers if config is missing.
        /// </summary>
        public EnemyTierConfigSO.TierDefinition GetTierDefinition(float distance)
        {
            if (config == null || config.tiers == null || config.tiers.Count == 0)
            {
                return new EnemyTierConfigSO.TierDefinition
                {
                    minDistance = 0f,
                    maxDistance = -1f,
                    hpMult = 1f,
                    dmgMult = 1f,
                };
            }

            for (int i = 0; i < config.tiers.Count; i++)
            {
                var t = config.tiers[i];
                if (Matches(distance, t))
                    return t;
            }

            // If distance doesn't fit due to invalid config, return first tier as safe default.
            return config.tiers[0];
        }

        private static bool Matches(float distance, EnemyTierConfigSO.TierDefinition t)
        {
            if (distance < t.minDistance)
                return false;

            if (t.maxDistance < 0f)
                return true;

            return distance < t.maxDistance;
        }

        private void Awake()
        {
            // Optional: register for consumers that want a manually-set singleton, without creating duplicates.
            DistanceTierServiceInstance.TryRegister(this);
        }

        private void OnDestroy()
        {
            DistanceTierServiceInstance.TryUnregister(this);
        }

        /// <summary>
        /// Optional helper for projects that want a manually-registered instance.
        /// This never creates objects and will not search the scene.
        /// Prefer explicit reference injection.
        /// </summary>
        public static class DistanceTierServiceInstance
        {
            /// <summary>
            /// The most recently registered instance (if any). May be null.
            /// </summary>
            public static DistanceTierService Instance { get; private set; }

            internal static void TryRegister(DistanceTierService svc)
            {
                if (svc == null)
                    return;

                if (Instance != null && Instance != svc)
                {
                    // Do not spam; just warn once per registration attempt.
                    Debug.LogWarning("[DistanceTierService] Multiple instances detected; prefer explicit wiring.", svc);
                }

                Instance = svc;
            }

            internal static void TryUnregister(DistanceTierService svc)
            {
                if (svc == null)
                    return;

                if (Instance == svc)
                    Instance = null;
            }
        }
    }
}
