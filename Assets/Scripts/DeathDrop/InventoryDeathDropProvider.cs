using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Abyssbound.DeathDrop
{
    public sealed class InventoryDeathDropProvider : IDeathDropProvider
    {
        private readonly PlayerInventory _inv;

        public InventoryDeathDropProvider(PlayerInventory inv)
        {
            _inv = inv;
        }

        public IEnumerable<DroppedItem> GetAllItems()
        {
            if (_inv == null)
                yield break;

            IReadOnlyDictionary<string, int> snap = null;
            try { snap = _inv.GetAllItemsSnapshot(); } catch { snap = null; }

            if (snap == null)
                yield break;

            foreach (var kvp in snap)
            {
                var id = kvp.Key;
                var count = kvp.Value;
                if (string.IsNullOrWhiteSpace(id) || count <= 0)
                    continue;

                yield return new DroppedItem(id, count);
            }
        }

        public bool Contains(string itemId, int amount)
        {
            if (_inv == null) return false;
            if (string.IsNullOrWhiteSpace(itemId) || amount <= 0) return false;
            try { return _inv.Has(itemId, amount); } catch { return false; }
        }

        public int GetCount(string itemId)
        {
            if (_inv == null) return 0;
            if (string.IsNullOrWhiteSpace(itemId)) return 0;
            try { return _inv.Count(itemId); } catch { return 0; }
        }

        public bool TryRemove(string itemId, int amount)
        {
            if (_inv == null) return false;
            if (string.IsNullOrWhiteSpace(itemId) || amount <= 0) return false;

            // Prefer quiet removal to avoid log spam.
            if (TryRemoveQuietlyByReflection(_inv, itemId, amount))
                return true;

            try { return _inv.TryRemove(itemId, amount); }
            catch { return false; }
        }

        private static bool TryRemoveQuietlyByReflection(PlayerInventory inv, string itemId, int amount)
        {
            try
            {
                var fi = typeof(PlayerInventory).GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic);
                if (fi == null)
                    return false;

                if (fi.GetValue(inv) is not Dictionary<string, int> dict || dict == null)
                    return false;

                if (!dict.TryGetValue(itemId, out var cur) || cur <= 0)
                    return true;

                int next = cur - amount;
                if (next <= 0) dict.Remove(itemId);
                else dict[itemId] = next;

                // Raise Changed event via backing field (event can't be invoked externally).
                try
                {
                    var changedField = typeof(PlayerInventory).GetField("Changed", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (changedField != null && changedField.GetValue(inv) is Action a)
                        a.Invoke();
                }
                catch { }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
