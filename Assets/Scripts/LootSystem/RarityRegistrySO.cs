using System.Collections.Generic;
using UnityEngine;

namespace Abyssbound.Loot
{
    [CreateAssetMenu(menuName = "Abyssbound/Loot/Registries/Rarity Registry", fileName = "RarityRegistry")]
    public sealed class RarityRegistrySO : ScriptableObject
    {
        public List<RarityDefinitionSO> rarities = new();
    }
}
