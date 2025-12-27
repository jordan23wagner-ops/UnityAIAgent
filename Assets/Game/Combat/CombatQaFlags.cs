using UnityEngine;

namespace Abyssbound.Combat
{
    public static class CombatQaFlags
    {
        public static bool AlwaysHit;
        public static bool DrawAttackRanges;
        public static bool ProjectileVisualsEnabled = true;
        public static bool AttackDebugLogs;
        public static bool ProjectileDebug;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            AlwaysHit = false;
            DrawAttackRanges = false;
            ProjectileVisualsEnabled = true;
            AttackDebugLogs = false;
            ProjectileDebug = false;
        }
    }
}
