using System.Collections.Generic;
using UnityEngine;

namespace Abyssbound.Loot
{
    [CreateAssetMenu(menuName = "Abyssbound/Loot/Registries/Affix Registry", fileName = "AffixRegistry")]
    public sealed class AffixRegistrySO : ScriptableObject
    {
        public List<AffixDefinitionSO> affixes = new();
    }
}
