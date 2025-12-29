using UnityEngine;

namespace Abyssbound.Threat
{
    public static class ThreatMath
    {
        public static float QuantizeClampThreat(float distanceMeters, float metersPerThreat, float step, float maxThreat)
        {
            metersPerThreat = Mathf.Max(0.0001f, metersPerThreat);
            step = Mathf.Max(0.0001f, step);
            maxThreat = Mathf.Max(0f, maxThreat);

            var raw = distanceMeters / metersPerThreat;
            var quantized = Mathf.Round(raw / step) * step;
            return Mathf.Clamp(quantized, 0f, maxThreat);
        }

        public static int ThreatToStepIndex(float threat, float step, float maxThreat)
        {
            step = Mathf.Max(0.0001f, step);
            maxThreat = Mathf.Max(0f, maxThreat);

            int maxIndex = Mathf.RoundToInt(maxThreat / step);
            int idx = Mathf.RoundToInt(Mathf.Clamp(threat, 0f, maxThreat) / step);
            return Mathf.Clamp(idx, 0, Mathf.Max(0, maxIndex));
        }

        public static Gradient CreateDefaultThreatGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    // Explicit green → yellow → orange → red progression.
                    new GradientColorKey(new Color(0.10f, 1.00f, 0.25f, 1f), 0.00f), // green
                    new GradientColorKey(new Color(1.00f, 0.95f, 0.20f, 1f), 0.33f), // yellow
                    new GradientColorKey(new Color(1.00f, 0.55f, 0.10f, 1f), 0.66f), // orange
                    new GradientColorKey(new Color(1.00f, 0.18f, 0.18f, 1f), 1.00f), // red
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f),
                }
            );
            return g;
        }

        public static Color EvaluateThreatColor(Gradient gradient, float threat, float maxThreat)
        {
            if (gradient == null)
                return Color.white;

            maxThreat = Mathf.Max(0.0001f, maxThreat);
            float t = Mathf.Clamp01(threat / maxThreat);
            try { return gradient.Evaluate(t); }
            catch { return Color.white; }
        }
    }
}
