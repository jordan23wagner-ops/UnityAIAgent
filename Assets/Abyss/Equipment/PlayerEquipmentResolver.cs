using UnityEngine;
using Game.Systems;

namespace Abyss.Equipment
{
    public static class PlayerEquipmentResolver
    {
        public static PlayerEquipment GetOrFindOrCreate()
        {
            var inv = PlayerInventoryResolver.GetOrFind();
            if (inv == null) return null;

            var existing = inv.GetComponent<PlayerEquipment>();
            if (existing != null) return existing;

            existing = inv.GetComponentInParent<PlayerEquipment>();
            if (existing != null) return existing;

            existing = inv.GetComponentInChildren<PlayerEquipment>(true);
            if (existing != null) return existing;

            // Create on the same GameObject as the inventory for consistency.
            return inv.gameObject.AddComponent<PlayerEquipment>();
        }
    }
}
