using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class SimplePool<T> where T : Component
{
    private readonly Queue<T> _pool;
    private readonly HashSet<T> _inPool;
    private readonly Func<T> _create;

    public SimplePool(Func<T> create, int initialCapacity = 0)
    {
        _create = create ?? throw new ArgumentNullException(nameof(create));
        _pool = new Queue<T>(Mathf.Max(0, initialCapacity));
        _inPool = new HashSet<T>();
    }

    public T Get()
    {
        while (_pool.Count > 0)
        {
            var item = _pool.Dequeue();
            if (item == null)
                continue;

            _inPool.Remove(item);
            return item;
        }

        return _create();
    }

    public void Release(T item)
    {
        if (item == null)
            return;

        if (_inPool.Contains(item))
            return;

        _inPool.Add(item);
        _pool.Enqueue(item);
    }
}

public static class IntStringCache
{
    // Avoid per-hit allocations for common damage values.
    private const int MaxCached = 9999;
    private static readonly string[] Cache = new string[MaxCached + 1];

    public static string Get(int value)
    {
        if (value < 0)
            return value.ToString();

        if (value > MaxCached)
            return value.ToString();

        var s = Cache[value];
        if (s != null)
            return s;

        s = value.ToString();
        Cache[value] = s;
        return s;
    }
}
