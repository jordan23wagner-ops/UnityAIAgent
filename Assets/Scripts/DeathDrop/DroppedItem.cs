using System;
using UnityEngine;

namespace Abyssbound.DeathDrop
{
    [Serializable]
    public struct DroppedItem
    {
        public string itemId;
        public int amount;

        public DroppedItem(string itemId, int amount)
        {
            this.itemId = itemId;
            this.amount = amount;
        }

        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(itemId) && amount > 0;
        }

        public override string ToString()
        {
            return $"{itemId} x{amount}";
        }
    }
}
