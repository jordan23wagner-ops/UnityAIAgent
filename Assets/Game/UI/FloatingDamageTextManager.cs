using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FloatingDamageTextManager : MonoBehaviour
{
    public static FloatingDamageTextManager Instance { get; private set; }

    private static bool _loggedEnsure;
    private static bool _loggedPoolReady;
    private static bool _loggedDebugBlast;
    private static bool _loggedWarnParentCanvas;
    private static bool _loggedWarnTooSmallScale;
    private static bool _loggedWarnMissingRenderer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _loggedEnsure = false;
        _loggedPoolReady = false;
        _loggedDebugBlast = false;
        _loggedWarnParentCanvas = false;
        _loggedWarnTooSmallScale = false;
        _loggedWarnMissingRenderer = false;
    }

    [Header("Debug")]
    [SerializeField] private bool debugLogs;
    [SerializeField] private bool debugForceVisible = true;

    [Header("Defaults")]
    [SerializeField] private float defaultLifetimeSeconds = 0.9f;
    [SerializeField] private float defaultRiseSpeed = 1.0f;

    [Header("Spawn")]
    [SerializeField] private float spawnRadiusXZ = 0.20f;
    [SerializeField] private float verticalOffset = 1.25f;

    private SimplePool<FloatingDamageText> _pool;
    private readonly HashSet<EnemyHealth> _tracked = new HashSet<EnemyHealth>();

    public static FloatingDamageTextManager EnsureExists()
    {
        if (Instance != null)
        {
            if (!_loggedEnsure)
            {
                _loggedEnsure = true;
                Debug.Log("[FloatingDamageTextManager] Found existing instance.");
            }
            return Instance;
        }

        var existing = FindFirstObjectByType<FloatingDamageTextManager>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            if (!_loggedEnsure)
            {
                _loggedEnsure = true;
                Debug.Log("[FloatingDamageTextManager] Found existing instance.");
            }
            return existing;
        }

        var root = WorldUiRoot.GetOrCreateRoot();
        var go = new GameObject(nameof(FloatingDamageTextManager));
        go.transform.SetParent(root, false);
        var created = go.AddComponent<FloatingDamageTextManager>();

        if (!_loggedEnsure)
        {
            _loggedEnsure = true;
            Debug.Log("[FloatingDamageTextManager] Created new instance.");
        }

        return created;
    }

    public static void Spawn(int amount, Vector3 worldPos)
    {
        EnsureExists().SpawnInternal(amount, worldPos);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }

        Instance = this;
        _pool = new SimplePool<FloatingDamageText>(CreateNew, initialCapacity: 64);

        // TEMP: force on so we can prove damage text is rendering.
        debugForceVisible = true;

        Subscribe();
        RegisterExistingEnemies();

        if (!_loggedPoolReady)
        {
            _loggedPoolReady = true;
            Debug.Log("[FloatingDamageTextManager] Active + subscribed (pool ready)");
        }

        if (debugForceVisible && !_loggedDebugBlast)
        {
            _loggedDebugBlast = true;
            // Spawn at player (if present)
            var pos = TryGetPlayerPosition() + Vector3.up * 2f;
            SpawnInternal(77, pos);

            // Also spawn in front of camera to guarantee in-view
            var cam = Camera.main;
            if (cam != null)
            {
                var camPos = cam.transform.position + cam.transform.forward * 4f;
                SpawnInternal(777, camPos);
            }
        }
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

        if (_tracked.Contains(enemy))
            return;

        _tracked.Add(enemy);
        enemy.OnDamaged += OnEnemyDamaged;
        enemy.OnDeath += OnEnemyDeath;
    }

    private void HandleEnemyDisabled(EnemyHealth enemy)
    {
        if (enemy == null)
            return;

        if (!_tracked.Remove(enemy))
            return;

        enemy.OnDamaged -= OnEnemyDamaged;
        enemy.OnDeath -= OnEnemyDeath;
    }

    private void OnEnemyDamaged(EnemyHealth enemy, float amount)
    {
        if (enemy == null)
            return;

        int display = Mathf.Max(0, Mathf.RoundToInt(amount));

        // Spawn near enemy center (renderer bounds center if possible).
        var r = enemy.GetComponentInChildren<Renderer>();
        var center = r != null ? r.bounds.center : enemy.transform.position;

        center += Vector3.up * verticalOffset;

        float radius = Mathf.Max(0f, spawnRadiusXZ);
        center += new Vector3(UnityEngine.Random.Range(-radius, radius), 0f, UnityEngine.Random.Range(-radius, radius));

        SpawnInternal(display, center);
    }

    private void OnEnemyDeath(EnemyHealth enemy)
    {
        // No-op: damage text already spawned by OnDamaged before lethal death.
        // Keep subscription cleanup via AnyDisabled.
    }

    private void SpawnInternal(int amount, Vector3 worldPos)
    {
        if (amount <= 0)
            return;

        var text = _pool.Get();

        // Parent under a neutral non-canvas root so world-space text never inherits Canvas transforms/scales.
        var parent = WorldUiRoot.GetOrCreateWorldTextRoot();
        if (parent)
            text.transform.SetParent(parent, false);
        text.transform.position = worldPos;
        text.transform.localScale = Vector3.one * 0.12f;
        text.gameObject.layer = LayerMask.NameToLayer("Default");
        text.SetDefaults(defaultLifetimeSeconds, defaultRiseSpeed);
        text.Finished = Release;

        text.DebugForceVisible = debugForceVisible;

        var spawnRenderer = text.GetComponent<Renderer>();
        if (spawnRenderer != null)
        {
            try { spawnRenderer.sortingLayerName = "Default"; } catch { }
            spawnRenderer.sortingOrder = 3000;
        }

        text.gameObject.SetActive(true);
        text.Init(amount);

        // Guard rails (warn once total, no spam)
        if (!_loggedWarnParentCanvas)
        {
            var parentCanvas = text.GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                _loggedWarnParentCanvas = true;
                Debug.LogWarning("[DMG] DamageText is parented under a Canvas (unexpected)", text);
            }
        }

        if (!_loggedWarnTooSmallScale)
        {
            var mag = text.transform.lossyScale.magnitude;
            if (mag < 0.005f)
            {
                _loggedWarnTooSmallScale = true;
                Debug.LogWarning($"[DMG] DamageText scale too small (lossyScale.magnitude={mag})", text);
            }
        }

        if (!_loggedWarnMissingRenderer)
        {
            if (spawnRenderer == null)
            {
                _loggedWarnMissingRenderer = true;
                Debug.LogWarning("[DMG] DamageText has no Renderer (may be invisible)", text);
            }
        }

        if (debugLogs)
            Debug.Log($"[DMG] Spawn {amount} at {worldPos}", text);
    }

    private static Vector3 TryGetPlayerPosition()
    {
        try
        {
            var byTag = GameObject.FindWithTag("Player");
            if (byTag != null)
                return byTag.transform.position;
        }
        catch { }

        try
        {
            var byHealth = FindFirstObjectByType<PlayerHealth>();
            if (byHealth != null)
                return byHealth.transform.position;
        }
        catch { }

        return Vector3.zero;
    }

    private void Release(FloatingDamageText floating)
    {
        if (floating == null)
            return;

        floating.Finished = null;
        floating.gameObject.SetActive(false);
        _pool.Release(floating);
    }

    private static FloatingDamageText CreateNew()
    {
        var go = new GameObject("DamageText");
        go.layer = LayerMask.NameToLayer("Default");
        go.transform.localScale = Vector3.one;

        // Try TextMeshPro (via reflection) first; fallback to TextMesh.
        var tmpType = Type.GetType("TMPro.TextMeshPro, Unity.TextMeshPro");
        if (tmpType != null)
        {
            var tmp = go.AddComponent(tmpType);

            // Optional: set TMP fontSize if available (reflection, no compile-time TMPro dependency).
            try
            {
                // Prefer TMP_Text.fontSize property.
                var tmpTextType = Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro");
                var fontSizeProp = tmpTextType != null
                    ? tmpTextType.GetProperty("fontSize", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                    : null;

                if (fontSizeProp != null)
                {
                    // Small, sane world-space size (TMP world-space units differ from TextMesh).
                    fontSizeProp.SetValue(tmp, 2.0f);
                }
            }
            catch
            {
                // Ignore: TMP present but reflection surface differs.
            }
        }
        else
        {
            var tm = go.AddComponent<TextMesh>();
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 32;
            tm.characterSize = 0.07f;
            tm.color = Color.red;
            tm.richText = false;
        }

        // Force high sorting order (TextMesh uses MeshRenderer; TMP often does too).
        var r = go.GetComponent<Renderer>();
        if (r != null)
        {
            try { r.sortingLayerName = "Default"; } catch { }
            r.sortingOrder = 3000;
        }
        else
        {
            if (!_loggedWarnMissingRenderer)
            {
                _loggedWarnMissingRenderer = true;
                Debug.LogWarning("[DMG] DamageText created without a Renderer (may be invisible)", go);
            }
        }

        var floating = go.AddComponent<FloatingDamageText>();
        go.SetActive(false);
        return floating;
    }

}
