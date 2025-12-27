using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

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

    [Header("Head Anchor")]
    [SerializeField] private float headPadding = 0.30f;
    [SerializeField] private float defaultHeadOffset = 1.25f;

    [Header("Offsets")]
    [SerializeField] private float damageHorizontalOffset = 0.60f;
    [SerializeField] private float xpRightOffset = 0.60f;

    [Header("Motion")]
    [SerializeField] private float driftUpSpeed = 1.0f;
    [SerializeField] private float driftSideSpeed = 0.35f;

    [Header("Level Up Text")]
    [SerializeField] private float levelUpExtraHeight = 2.0f;
    [SerializeField] private float levelUpJitterXZ = 0.03f;
    [SerializeField] private float levelUpSideDriftSpeed = 0.05f;
    [SerializeField] private float levelUpCooldownSeconds = 0.75f;

    [Header("Anti-Overlap")]
    [SerializeField] private float spawnRadiusXZ = 0.08f;

    // Legacy field (kept for compatibility with existing serialized scenes/prefabs)
    // Used as a minimum head offset for very small bounds.
    [SerializeField] private float verticalOffset = 1.25f;

    [Header("XP Text")]
    [SerializeField] private Color xpGainColor = new Color(0.2f, 1.0f, 0.2f, 1f);
    [SerializeField] private Color xpLevelUpColor = new Color(0.35f, 1.0f, 0.35f, 1f);

    private SimplePool<FloatingDamageText> _pool;
    private readonly HashSet<EnemyHealth> _tracked = new HashSet<EnemyHealth>();
    private readonly Dictionary<string, float> _lastLevelUpSpawnBySkill = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

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

    public static void SpawnText(string text, Vector3 worldPos)
    {
        EnsureExists().SpawnInternalText(text, worldPos);
    }

    public static void ShowXpGain(Vector3 worldPos, int amount, string skillName)
    {
        EnsureExists().ShowXpGainInternal(worldPos, amount, skillName);
    }

    public static void ShowSkillXpGain(EnemyHealth enemy, int amount, string skillName)
    {
        EnsureExists().ShowSkillXpGainInternal(enemy, amount, skillName);
    }

    public static void ShowSkillXpGain(Vector3 worldPos, int amount, string skillName)
    {
        EnsureExists().ShowSkillXpGainInternal(worldPos, amount, skillName);
    }

    public static void ShowXpGainCombined(EnemyHealth enemy, int attackXp, int styleXp, string styleSkillName)
    {
        EnsureExists().ShowXpGainCombinedInternal(enemy, attackXp, styleXp, styleSkillName);
    }

    public static void ShowLevelUp(Vector3 worldPos, string skillName, int newLevel)
    {
        EnsureExists().ShowLevelUpInternal(worldPos, skillName, newLevel);
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

        var anchor = ComputeHeadAnchor(enemy.transform, enemy.transform.position);
        var camRight = TryGetCameraRight();
        var finalPos = anchor + (-camRight * Mathf.Max(0f, damageHorizontalOffset));
        finalPos += GetJitterXZ();

        var vel = (Vector3.up * driftUpSpeed) + (-camRight * driftSideSpeed);
        SpawnInternalText(display.ToString(), finalPos, baseColorOverride: null, velocityOverride: vel);
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

        // Legacy entry-point used by older callsites: treat as damage.
        var camRight = TryGetCameraRight();
        var vel = (Vector3.up * driftUpSpeed) + (-camRight * driftSideSpeed);
        SpawnInternalText(amount.ToString(), worldPos, baseColorOverride: null, velocityOverride: vel);
    }

    private void SpawnInternalText(string textValue, Vector3 worldPos)
    {
        // Legacy entry-point used by older callsites: treat as damage/miss.
        var camRight = TryGetCameraRight();
        var vel = (Vector3.up * driftUpSpeed) + (-camRight * driftSideSpeed);
        SpawnInternalText(textValue, worldPos, baseColorOverride: null, velocityOverride: vel);
    }

    private void SpawnInternalText(string textValue, Vector3 worldPos, Color? baseColorOverride, Vector3? velocityOverride)
    {
        if (string.IsNullOrWhiteSpace(textValue))
            return;

        var text = _pool.Get();

        // Support both world TMP and UGUI TMP by detecting the TMP type up-front.
        TMP_Text tmp = null;
        try { tmp = text != null ? text.GetComponentInChildren<TMP_Text>(true) : null; } catch { tmp = null; }

        // Parent under a neutral non-canvas root so world-space text never inherits Canvas transforms/scales.
        Transform parent;
        if (tmp is TextMeshProUGUI)
            parent = WorldUiRoot.GetOrCreateCanvasRoot();
        else
            parent = WorldUiRoot.GetOrCreateWorldTextRoot();

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

        // Pool safety: always reset back to the default style before optionally applying an override.
        try { text.ResetToDefaultColor(); } catch { }
        if (baseColorOverride.HasValue)
        {
            try { text.SetBaseColor(baseColorOverride.Value); } catch { }
        }

        try { text.ResetVelocityToDefault(); } catch { }
        if (velocityOverride.HasValue)
        {
            try { text.SetVelocity(velocityOverride.Value); } catch { }
        }
        text.Init(textValue);

        // Force render-safe TMP defaults at spawn time (prevents "blue T" gizmo-only objects).
        if (tmp != null)
        {
            try
            {
                tmp.enabled = true;
                tmp.text = textValue;

                tmp.alpha = 1f;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.fontSize = Mathf.Max(tmp.fontSize, 36f);

                var c = tmp.color;
                if (c.a <= 0.01f)
                    c = Color.red;
                c.a = 1f;
                tmp.color = c;

                // Ensure scale isn't tiny.
                var ls = text.transform.localScale;
                if (ls.x < 0.1f || ls.y < 0.1f || ls.z < 0.1f)
                    text.transform.localScale = Vector3.one;

                // If this is UGUI TMP, ensure there is an enabled canvas with a sane sort order.
                var canvas = tmp.GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    canvas.enabled = true;
                    canvas.overrideSorting = true;
                    if (canvas.sortingOrder < 100)
                        canvas.sortingOrder = 100;
                }
            }
            catch { }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugLogs)
            {
                try
                {
                    var tmpType = tmp.GetType().Name;
                    Debug.Log($"[DMG] Spawned damage text type={tmpType} text='{tmp.text}' pos={worldPos}", text);
                }
                catch { }
            }
#endif
        }

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
            Debug.Log($"[DMG] Spawn '{textValue}' at {worldPos}", text);
    }

    private void ShowXpGainInternal(Vector3 worldPos, int amount, string skillName)
    {
        if (!Abyssbound.Stats.XpFloatingTextFlags.ShowXpFloatingText)
            return;

        if (amount <= 0)
            return;

        var basePos = worldPos + (Vector3.up * Mathf.Max(0f, defaultHeadOffset));
        var right = TryGetCameraRight();
        var xpPos = basePos + (right * Mathf.Max(0f, xpRightOffset));
        xpPos += GetJitterXZ();

        var vel = (Vector3.up * driftUpSpeed) + (right * driftSideSpeed);

        // Legacy helper: short format.
        SpawnInternalText($"+{amount} XP", xpPos, xpGainColor, vel);
    }

    private void ShowXpGainInternal(EnemyHealth enemy, int amount, string skillName)
    {
        if (enemy == null)
            return;

        var anchor = ComputeHeadAnchor(enemy.transform, enemy.transform.position);
        var right = TryGetCameraRight();
        var xpPos = anchor + (right * Mathf.Max(0f, xpRightOffset));
        xpPos += GetJitterXZ();

        var vel = (Vector3.up * driftUpSpeed) + (right * driftSideSpeed);
        SpawnInternalText($"+{amount} XP", xpPos, xpGainColor, vel);
    }

    private void ShowSkillXpGainInternal(EnemyHealth enemy, int amount, string skillName)
    {
        if (!Abyssbound.Stats.XpFloatingTextFlags.ShowXpFloatingText)
            return;

        if (enemy == null)
            return;

        if (amount <= 0)
            return;

        string label = string.IsNullOrWhiteSpace(skillName) ? "XP" : skillName.Trim() + " XP";

        var anchor = ComputeHeadAnchor(enemy.transform, enemy.transform.position);
        var right = TryGetCameraRight();
        var xpPos = anchor + (right * Mathf.Max(0f, xpRightOffset));
        xpPos += GetJitterXZ();

        var vel = (Vector3.up * driftUpSpeed) + (right * driftSideSpeed);
        SpawnInternalText($"+{amount} {label}", xpPos, xpGainColor, vel);
    }

    private void ShowSkillXpGainInternal(Vector3 worldPos, int amount, string skillName)
    {
        if (!Abyssbound.Stats.XpFloatingTextFlags.ShowXpFloatingText)
            return;

        if (amount <= 0)
            return;

        string label = string.IsNullOrWhiteSpace(skillName) ? "XP" : skillName.Trim() + " XP";

        var basePos = worldPos + (Vector3.up * Mathf.Max(0f, defaultHeadOffset));
        var right = TryGetCameraRight();
        var xpPos = basePos + (right * Mathf.Max(0f, xpRightOffset));
        xpPos += GetJitterXZ();

        var vel = (Vector3.up * driftUpSpeed) + (right * driftSideSpeed);
        SpawnInternalText($"+{amount} {label}", xpPos, xpGainColor, vel);
    }

    private void ShowXpGainCombinedInternal(EnemyHealth enemy, int attackXp, int styleXp, string styleSkillName)
    {
        if (!Abyssbound.Stats.XpFloatingTextFlags.ShowXpFloatingText)
            return;

        if (enemy == null)
            return;

        if (attackXp <= 0 && styleXp <= 0)
            return;

        string styleLabel = string.IsNullOrWhiteSpace(styleSkillName) ? "XP" : styleSkillName.Trim() + " XP";
        string text;

        if (attackXp > 0 && styleXp > 0)
            text = $"+{attackXp} Attack XP  |  +{styleXp} {styleLabel}";
        else if (attackXp > 0)
            text = $"+{attackXp} Attack XP";
        else
            text = $"+{styleXp} {styleLabel}";

        var anchor = ComputeHeadAnchor(enemy.transform, enemy.transform.position);
        var right = TryGetCameraRight();
        var xpPos = anchor + (right * Mathf.Max(0f, xpRightOffset));
        xpPos += GetJitterXZ();

        var vel = (Vector3.up * driftUpSpeed) + (right * driftSideSpeed);
        SpawnInternalText(text, xpPos, xpGainColor, vel);
    }

    private void ShowLevelUpInternal(Vector3 worldPos, string skillName, int newLevel)
    {
        if (!Abyssbound.Stats.XpFloatingTextFlags.ShowXpFloatingText)
            return;

        if (newLevel <= 0)
            return;

        // Anti-spam: per-skill cooldown.
        string key = string.IsNullOrWhiteSpace(skillName) ? "(skill)" : skillName.Trim();
        float now = Time.time;
        float cd = Mathf.Max(0f, levelUpCooldownSeconds);
        if (cd > 0.0001f)
        {
            if (_lastLevelUpSpawnBySkill.TryGetValue(key, out var lastTime))
            {
                if (now - lastTime < cd)
                    return;
            }
            _lastLevelUpSpawnBySkill[key] = now;
        }

        string label = string.IsNullOrWhiteSpace(skillName) ? "" : skillName.Trim();
        string msg = string.IsNullOrEmpty(label)
            ? $"Level {newLevel}!"
            : $"{label} Level {newLevel}!";

        // Distinct level-up spawn path: above the head anchor, higher than damage/XP.
        var target = TryGetPlayerTransform();
        var anchor = ComputeHeadAnchor(target, worldPos);
        var pos = anchor + Vector3.up * Mathf.Max(0f, levelUpExtraHeight);

        // Optional minimal jitter (smaller than combat jitter).
        float j = Mathf.Max(0f, levelUpJitterXZ);
        if (j > 0.0001f)
        {
            float x = UnityEngine.Random.Range(-j, j);
            float z = UnityEngine.Random.Range(-j, j);
            pos += new Vector3(x, 0f, z);
        }

        // Drift mostly upward so it exits cleanly.
        var right = TryGetCameraRight();
        var vel = (Vector3.up * driftUpSpeed) + (right * Mathf.Clamp(levelUpSideDriftSpeed, -0.25f, 0.25f));

        SpawnInternalText(msg, pos, xpLevelUpColor, vel);
    }

    private static Transform TryGetPlayerTransform()
    {
        try
        {
            var byTag = GameObject.FindWithTag("Player");
            if (byTag != null)
                return byTag.transform;
        }
        catch { }

        try
        {
            var byHealth = FindFirstObjectByType<PlayerHealth>();
            if (byHealth != null)
                return byHealth.transform;
        }
        catch { }

        return null;
    }

    private static Vector3 TryGetCameraRight()
    {
        try
        {
            var cam = Camera.main;
            if (cam != null)
                return cam.transform.right;
        }
        catch { }

        return Vector3.right;
    }

    private Vector3 ComputeHeadAnchor(Transform target, Vector3 fallbackWorldPos)
    {
        if (target == null)
            return fallbackWorldPos + Vector3.up * Mathf.Max(0f, Mathf.Max(defaultHeadOffset, verticalOffset));

        if (TryGetBounds(target, out var b))
        {
            float pad = Mathf.Max(0f, headPadding);
            // Use verticalOffset as a minimum head height so very small bounds still get readable text.
            float y = Mathf.Max(b.extents.y + pad, Mathf.Max(0f, verticalOffset));
            return b.center + Vector3.up * y;
        }

        return target.position + Vector3.up * Mathf.Max(0f, Mathf.Max(defaultHeadOffset, verticalOffset));
    }

    private static bool TryGetBounds(Transform target, out Bounds bounds)
    {
        bounds = default;
        if (target == null)
            return false;

        try
        {
            var c = target.GetComponentInChildren<Collider>();
            if (c != null)
            {
                bounds = c.bounds;
                return true;
            }
        }
        catch { }

        try
        {
            var r = target.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                bounds = r.bounds;
                return true;
            }
        }
        catch { }

        return false;
    }

    private Vector3 GetJitterXZ()
    {
        float j = Mathf.Max(0f, spawnRadiusXZ);
        if (j <= 0.0001f)
            return Vector3.zero;

        float x = UnityEngine.Random.Range(-j, j);
        float z = UnityEngine.Random.Range(-j, j);
        return new Vector3(x, 0f, z);
    }

    public static void ShowXpGain(EnemyHealth enemy, int amount, string skillName)
    {
        EnsureExists().ShowXpGainInternal(enemy, amount, skillName);
    }

    public static void ShowMiss(EnemyHealth enemy)
    {
        if (enemy == null)
            return;

        var mgr = EnsureExists();
        var anchor = mgr.ComputeHeadAnchor(enemy.transform, enemy.transform.position);
        var camRight = TryGetCameraRight();
        var pos = anchor + (-camRight * Mathf.Max(0f, mgr.damageHorizontalOffset));
        pos += mgr.GetJitterXZ();
        var vel = (Vector3.up * mgr.driftUpSpeed) + (-camRight * mgr.driftSideSpeed);
        mgr.SpawnInternalText("Miss", pos, baseColorOverride: null, velocityOverride: vel);
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
