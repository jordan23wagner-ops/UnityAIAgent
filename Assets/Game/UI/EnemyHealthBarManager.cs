using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyHealthBarManager : MonoBehaviour
{
    public static EnemyHealthBarManager Instance { get; private set; }

    private static bool _loggedEnsure;

    [Header("Debug")]
    [SerializeField] private bool debugLogs;

    private readonly Dictionary<EnemyHealth, EnemyHealthBar> _active = new Dictionary<EnemyHealth, EnemyHealthBar>(128);
    private readonly Dictionary<EnemyHealth, Action<EnemyHealth>> _deathHandlers = new Dictionary<EnemyHealth, Action<EnemyHealth>>(128);
    private SimplePool<EnemyHealthBar> _pool;

    public static EnemyHealthBarManager EnsureExists()
    {
        if (Instance != null)
        {
            if (!_loggedEnsure)
            {
                _loggedEnsure = true;
                Debug.Log("[EnemyHealthBarManager] Found existing instance.");
            }
            return Instance;
        }

        var existing = FindFirstObjectByType<EnemyHealthBarManager>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            if (!_loggedEnsure)
            {
                _loggedEnsure = true;
                Debug.Log("[EnemyHealthBarManager] Found existing instance.");
            }
            return existing;
        }

        var root = WorldUiRoot.GetOrCreateRoot();
        var go = new GameObject(nameof(EnemyHealthBarManager));
        go.transform.SetParent(root, false);
        var created = go.AddComponent<EnemyHealthBarManager>();

        if (!_loggedEnsure)
        {
            _loggedEnsure = true;
            Debug.Log("[EnemyHealthBarManager] Created new instance.");
        }

        return created;
    }

    public static void EnsureFor(EnemyHealth enemy)
    {
        var mgr = EnsureExists();
        mgr.EnsureForInternal(enemy);
    }

    public static void ReleaseFor(EnemyHealth enemy)
    {
        if (Instance == null)
            return;

        Instance.ReleaseForInternal(enemy);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }

        Instance = this;
        _pool = new SimplePool<EnemyHealthBar>(CreateNew, initialCapacity: 32);

        Subscribe();
        RegisterExistingEnemies();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Unsubscribe();
            Instance = null;
        }
    }

    private void Subscribe()
    {
        EnemyHealth.AnyEnabled += HandleEnemyEnabled;
        EnemyHealth.AnyDisabled += HandleEnemyDisabled;
    }

    private void Unsubscribe()
    {
        EnemyHealth.AnyEnabled -= HandleEnemyEnabled;
        EnemyHealth.AnyDisabled -= HandleEnemyDisabled;
    }

    private void RegisterExistingEnemies()
    {
        var enemies = FindObjectsByType<EnemyHealth>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
            HandleEnemyEnabled(enemies[i]);
    }

    private void HandleEnemyEnabled(EnemyHealth enemy)
    {
        if (enemy == null)
            return;

        if (enemy.IsDead)
            return;

        EnsureForInternal(enemy);
    }

    private void HandleEnemyDisabled(EnemyHealth enemy)
    {
        ReleaseForInternal(enemy);
    }

    private void EnsureForInternal(EnemyHealth enemy)
    {
        if (enemy == null)
            return;

        if (_active.TryGetValue(enemy, out var existing) && existing != null)
            return;

        var bar = _pool.Get();
        _active[enemy] = bar;

        var parent = WorldUiRoot.GetOrCreateCanvasRoot();
        if (parent)
            bar.transform.SetParent(parent, false);
        bar.Bind(enemy);
        bar.gameObject.SetActive(true);

        if (!_deathHandlers.ContainsKey(enemy))
        {
            Action<EnemyHealth> handler = OnEnemyDeath;
            _deathHandlers[enemy] = handler;
            enemy.OnDeath += handler;
        }

        if (debugLogs)
            Debug.Log($"[EnemyHealthBarManager] Created/bound bar for '{enemy.name}'", enemy);
    }

    private void OnEnemyDeath(EnemyHealth enemy)
    {
        ReleaseForInternal(enemy);
    }

    private void ReleaseForInternal(EnemyHealth enemy)
    {
        if (enemy == null)
            return;

        if (_deathHandlers.TryGetValue(enemy, out var handler) && handler != null)
        {
            enemy.OnDeath -= handler;
            _deathHandlers.Remove(enemy);
        }

        if (!_active.TryGetValue(enemy, out var bar) || bar == null)
        {
            _active.Remove(enemy);
            return;
        }

        _active.Remove(enemy);

        bar.Unbind();
        bar.gameObject.SetActive(false);
        _pool.Release(bar);

        if (debugLogs)
            Debug.Log($"[EnemyHealthBarManager] Released bar for '{enemy.name}'", enemy);
    }

    private static EnemyHealthBar CreateNew()
    {
        var go = new GameObject("EnemyHealthBar");
        go.AddComponent<RectTransform>().localScale = Vector3.one;
        var bar = go.AddComponent<EnemyHealthBar>();
        go.SetActive(false);
        return bar;
    }
}
