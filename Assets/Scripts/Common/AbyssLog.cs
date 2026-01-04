using UnityEngine;

namespace Abyssbound.Common
{
    public static class AbyssLog
    {
        public static void Info(string message)
        {
            UnityEngine.Debug.Log(message);
        }

        public static void Warn(string message)
        {
            UnityEngine.Debug.LogWarning(message);
        }

        public static void Error(string message)
        {
            UnityEngine.Debug.LogError(message);
        }
    }
}
