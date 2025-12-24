using System.Collections.Generic;
using UnityEngine;

namespace Abyssbound.Loot
{
    [CreateAssetMenu(menuName = "Abyssbound/Loot/Affix Pool", fileName = "AffixPool_")]
    public sealed class AffixPoolSO : ScriptableObject
    {
        public string id;
        public List<AffixDefinitionSO> affixes = new();

        public override string ToString()
        {
            if (!string.IsNullOrWhiteSpace(id)) return id;
            return name;
        }
    }
}
