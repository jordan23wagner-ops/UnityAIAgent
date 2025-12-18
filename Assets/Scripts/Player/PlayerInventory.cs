using System.Collections.Generic;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    private readonly Dictionary<string, int> _items = new();

    public void Add(string itemId, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return;
        if (amount <= 0) return;

        int next = _items.TryGetValue(itemId, out var cur) ? (cur + amount) : amount;
        _items[itemId] = next;

        Debug.Log($"[Inventory] Added {amount}x {itemId}. Now: {next}", this);
    }

    public bool Has(string itemId, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0) return false;
        return _items.TryGetValue(itemId, out var count) && count >= amount;
    }

    public int Count(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return 0;
        return _items.TryGetValue(itemId, out var count) ? count : 0;
    }

    public bool TryConsume(string itemId, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0) return false;
        if (!_items.TryGetValue(itemId, out var count)) return false;
        if (count < amount) return false;

        int newCount = count - amount;
        if (newCount <= 0) _items.Remove(itemId);
        else _items[itemId] = newCount;

        Debug.Log($"[Inventory] Consumed {amount}x {itemId}. Now: {Count(itemId)}");
        return true;
    }

    public bool TryRemove(string itemId, int amount = 1)
    {
        // Alias for clarity in systems like merchant selling.
        return TryConsume(itemId, amount);
    }

    public IReadOnlyDictionary<string, int> GetAllItemsSnapshot()
    {
        // Snapshot to prevent callers from mutating internal state.
        return new Dictionary<string, int>(_items);
    }
}
