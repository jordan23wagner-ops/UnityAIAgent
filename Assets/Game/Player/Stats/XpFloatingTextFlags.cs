using UnityEngine;

namespace Abyssbound.Stats
{
    public static class XpFloatingTextFlags
    {
        public static bool ShowXpFloatingText = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ShowXpFloatingText = true;
        }
    }
}
