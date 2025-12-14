using System.Collections.Generic;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    private readonly Dictionary<string, int> _items = new();

    public void Add(string itemId, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0) return;

        if (_items.ContainsKey(itemId)) _items[itemId] += amount;
        else _items[itemId] = amount;

        Debug.Log($"[Inventory] Added {amount}x {itemId}. Now: {_items[itemId]}");
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
}
