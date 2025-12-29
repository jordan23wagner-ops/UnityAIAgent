using System.Collections.Generic;

namespace Abyssbound.DeathDrop
{
    public interface IDeathDropProvider
    {
        IEnumerable<DroppedItem> GetAllItems();
        bool Contains(string itemId, int amount);
        int GetCount(string itemId);

        /// <summary>
        /// Removes items from this provider (inventory stack decrement or equipment unequip).
        /// </summary>
        bool TryRemove(string itemId, int amount);
    }
}
