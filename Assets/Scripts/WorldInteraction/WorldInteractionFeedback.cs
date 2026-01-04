using UnityEngine;

namespace Abyssbound.WorldInteraction
{
    public static class WorldInteractionFeedback
    {
        public static void LogBlocked(string reason, string objectName, Object context = null)
        {
            var r = string.IsNullOrWhiteSpace(reason) ? "Cannot." : reason.Trim();
            var n = string.IsNullOrWhiteSpace(objectName) ? "interact" : objectName.Trim();

            if (context != null)
                Debug.Log($"[WorldInteraction] Can't {n}: {r}", context);
            else
                Debug.Log($"[WorldInteraction] Can't {n}: {r}");
        }
    }
}
