using UnityEngine;

namespace Abyssbound.Stats
{
    public static class XpAwardFlags
    {
        public static bool AwardAttackXp = true;
        public static bool AwardDefenceXpFromDamageTaken = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            AwardAttackXp = true;
            AwardDefenceXpFromDamageTaken = true;
        }
    }
}
