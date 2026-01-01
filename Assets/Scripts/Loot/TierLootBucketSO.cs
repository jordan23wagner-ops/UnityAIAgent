using System;
using UnityEngine;

namespace Abyssbound.Loot
{
    [CreateAssetMenu(menuName = "Abyssbound/Loot/Tier Loot Bucket", fileName = "TierLootBucket_")]
    public sealed class TierLootBucketSO : ScriptableObject
    {
        [Serializable]
        public struct WeightedEntry
        {
            public UnityEngine.Object itemRef;
            public int weight;
            public int minQty;
            public int maxQty;
        }

        public int tier = 1;
        public WeightedEntry[] entries;
    }
}
