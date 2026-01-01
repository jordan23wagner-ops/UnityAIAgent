using UnityEngine;

namespace Abyssbound.Combat.Tiering
{
    [DisallowMultipleComponent]
    public sealed class EnemyLootContext : MonoBehaviour
    {
        private EnemyTierApplier _tierApplier;

        public int LootTier
        {
            get
            {
                if (_tierApplier == null)
                    return 1;

                return Mathf.Clamp(_tierApplier.AppliedTierNumber, 1, 999);
            }
        }

        public string DebugSummary
        {
            get
            {
                if (_tierApplier == null)
                    return $"{name} lootTier=1 (no EnemyTierApplier)";

                int tier = Mathf.Clamp(_tierApplier.AppliedTierNumber, 1, 999);
                return $"{name} tier={_tierApplier.AppliedTierNumber} dist={_tierApplier.AppliedDistance:F2} lootTier={tier}";
            }
        }

        private void Awake()
        {
            _tierApplier = GetComponent<EnemyTierApplier>();
        }
    }
}
