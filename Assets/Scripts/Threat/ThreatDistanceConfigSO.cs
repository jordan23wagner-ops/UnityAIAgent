using System;
using UnityEngine;

namespace Abyssbound.Threat
{
    [CreateAssetMenu(menuName = "Abyssbound/Threat/Distance Config", fileName = "Threat_DistanceConfig")]
    public sealed class ThreatDistanceConfigSO : ScriptableObject
    {
        [Header("Threat Steps")]
        [Min(0.01f)] public float step = 0.5f;

        [Tooltip("Meters at which each half-step is reached. Index 0 => threat=0.5, index 1 => 1.0, etc. Must be ascending.")]
        public float[] metersThresholds;

        public float MaxThreat
        {
            get
            {
                float s = Mathf.Max(0.01f, step);
                int n = metersThresholds != null ? metersThresholds.Length : 0;
                return Mathf.Max(0f, n * s);
            }
        }

        public int StepCount => metersThresholds != null ? metersThresholds.Length : 0;

        public float EvaluateThreat(float distanceMeters)
        {
            float s = Mathf.Max(0.01f, step);
            if (metersThresholds == null || metersThresholds.Length == 0)
                return 0f;

            int best = -1;
            for (int i = 0; i < metersThresholds.Length; i++)
            {
                if (distanceMeters >= metersThresholds[i]) best = i;
                else break;
            }

            float threat = (best + 1) * s;
            return Mathf.Clamp(threat, 0f, MaxThreat);
        }

        public int EvaluateStepIndex(float distanceMeters)
        {
            float threat = EvaluateThreat(distanceMeters);
            float s = Mathf.Max(0.01f, step);
            int idx = Mathf.RoundToInt(threat / s);
            return Mathf.Clamp(idx, 0, Mathf.Max(0, StepCount));
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            step = Mathf.Max(0.01f, step);
            if (metersThresholds == null || metersThresholds.Length == 0)
                return;

            for (int i = 0; i < metersThresholds.Length; i++)
                metersThresholds[i] = Mathf.Max(0f, metersThresholds[i]);

            Array.Sort(metersThresholds);
        }
#endif
    }
}
