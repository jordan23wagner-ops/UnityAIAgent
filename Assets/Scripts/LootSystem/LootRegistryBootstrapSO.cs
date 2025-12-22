using UnityEngine;

namespace Abyssbound.Loot
{
    [CreateAssetMenu(menuName = "Abyssbound/Loot/Registries/Loot Registry Bootstrap", fileName = "LootRegistryBootstrap")]
    public sealed class LootRegistryBootstrapSO : ScriptableObject
    {
        public ItemRegistrySO itemRegistry;
        public RarityRegistrySO rarityRegistry;
        public AffixRegistrySO affixRegistry;
    }
}
