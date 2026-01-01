using UnityEngine;

namespace Abyssbound.Loot
{
    [CreateAssetMenu(menuName = "Abyssbound/Loot/Tier Loot Config", fileName = "TierLootConfig")]
    public sealed class TierLootConfigSO : ScriptableObject
    {
        public TierLootBucketSO tier1;
        public TierLootBucketSO tier2;
        public TierLootBucketSO tier3;
        public TierLootBucketSO tier4;
        public TierLootBucketSO tier5;

        public TierLootBucketSO GetBucket(int tier)
        {
            int t = Mathf.Clamp(tier, 1, 5);
            return t switch
            {
                1 => this.tier1,
                2 => this.tier2,
                3 => this.tier3,
                4 => this.tier4,
                5 => this.tier5,
                _ => this.tier1,
            };
        }
    }
}
