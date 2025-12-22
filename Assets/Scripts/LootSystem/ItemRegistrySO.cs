using System.Collections.Generic;
using UnityEngine;

namespace Abyssbound.Loot
{
    [CreateAssetMenu(menuName = "Abyssbound/Loot/Registries/Item Registry", fileName = "ItemRegistry")]
    public sealed class ItemRegistrySO : ScriptableObject
    {
        public List<ItemDefinitionSO> items = new();
    }
}
