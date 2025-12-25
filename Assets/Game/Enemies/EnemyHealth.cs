using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// NOTE (Dec 2025): Combat feedback self-healing
// - Ensures Abyss runtime world UI + managers exist when any enemy enables (guards against bootstrap/load-order quirks)
// - Keeps EnemyHealth authoritative: raises events; managers react (no scene wiring required)

public class EnemyHealth : MonoBehaviour
{
    // PLAN (combat feedback + death lifecycle)
    // 1) EnemyHealth is authoritative: TakeDamage stores last hit position, fires OnDamaged, then (if lethal) fires OnDeath exactly once.
    // 2) UI/reactors (damage text + health bars + combat loop) subscribe to EnemyHealth events; EnemyHealth does not spawn UI.
    // 3) Death disables colliders/visuals/likely-AI, then deactivates for pooling after a short delay; OnEnable restores initial component states.

    [Header("Health")]
    [SerializeField] private int maxHealth = 10;
    [SerializeField] private int currentHealth;

    [Header("Death Events")]
    public UnityEvent OnDied;

    [Header("Pooling")]
    [SerializeField] private float despawnDelaySeconds = 0.35f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs;

    private bool isDead;

    public bool IsDead => isDead;

    public int MaxHealth => Mathf.Max(1, maxHealth);
    public int CurrentHealth => Mathf.Clamp(currentHealth, 0, MaxHealth);

    public void SetMaxHealthForQa(int newMaxHealth)
    {
        maxHealth = Mathf.Max(1, newMaxHealth);
        ResetHealth();
    }

    // New (required) code-first events.
    public event Action<EnemyHealth, float> OnDamaged;
    public event Action<EnemyHealth> OnDeath;

    // Legacy events kept for compatibility with existing scripts.
    public event Action<int, Vector3> Damaged;
    public event Action Died;

    // Optional registry events so runtime managers can subscribe without scene wiring.
    public static event Action<EnemyHealth> AnyEnabled;
    public static event Action<EnemyHealth> AnyDisabled;

    public Vector3 LastHitWorldPos { get; private set; }

    private Coroutine _despawnRoutine;

    private static bool _loggedEnsuredCombatFeedback;

    private Collider[] _cachedColliders;
    private bool[] _cachedColliderEnabled;
    private Renderer[] _cachedRenderers;
    private bool[] _cachedRendererEnabled;
    private Behaviour[] _cachedBehaviours;
    private bool[] _cachedBehaviourEnabled;

    private static readonly string[] DisableKeywords =
    {
        "AI",
        "Controller",
        "Mover",
        "Nav",
        "Chase",
    };

    private void Awake()
    {
        CacheInitialComponentStates();
        ResetHealth();
    }

    private void OnEnable()
    {
        // When pooled objects are re-enabled, ensure health is valid.
        if (isDead || currentHealth <= 0)
            ResetHealth();

        // Hard failsafe: ensure combat feedback systems exist even if bootstrap didn't run.
        if (Application.isPlaying)
        {
            try
            {
                WorldUiRoot.GetOrCreateRoot();
                FloatingDamageTextManager.EnsureExists();
                EnemyHealthBarManager.EnsureExists();

                if (!_loggedEnsuredCombatFeedback)
                {
                    _loggedEnsuredCombatFeedback = true;
                    Debug.Log("[EnemyHealth] Ensured combat feedback systems.");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EnemyHealth] Combat feedback ensure failed (non-fatal): {e.Message}", this);
            }
        }

        AnyEnabled?.Invoke(this);
    }

    private void OnDisable()
    {
        AnyDisabled?.Invoke(this);

        if (_despawnRoutine != null)
        {
            StopCoroutine(_despawnRoutine);
            _despawnRoutine = null;
        }
    }

    public void ResetHealth()
    {
        currentHealth = MaxHealth;
        isDead = false;

        LastHitWorldPos = default;

        // Restore initial enabled states for pooling reuse.
        RestoreInitialComponentStates();
    }

    public void TakeDamage(int amount)
    {
        TakeDamage((float)amount, default);
    }

    public void TakeDamage(float amount, Vector3 hitWorldPos = default)
    {
        if (isDead)
            return;

        if (amount <= 0f)
            return;

        int appliedInt = Mathf.CeilToInt(amount);
        if (appliedInt <= 0)
            return;

        currentHealth -= appliedInt;

        LastHitWorldPos = (hitWorldPos == default)
            ? (transform.position + Vector3.up * 1.2f)
            : hitWorldPos;

        // Fire damage events BEFORE death handling (even if lethal).
        OnDamaged?.Invoke(this, appliedInt);
        Damaged?.Invoke(appliedInt, LastHitWorldPos);

        // Optional additive AI hook: if EnemyAggroChase exists, force aggro on damage.
        // Attacker is not currently threaded through TakeDamage, so we use a safe tag lookup
        // ONLY on the damage event (never per-frame).
        try
        {
            var aggro = GetComponent<EnemyAggroChase>();
            if (aggro != null)
            {
                Transform attacker = null;
                try
                {
                    var playerGo = GameObject.FindGameObjectWithTag("Player");
                    attacker = playerGo != null ? playerGo.transform : null;
                }
                catch
                {
                    attacker = null;
                }

                aggro.ForceAggro(attacker);
            }
        }
        catch
        {
            // Intentionally silent: avoid console spam.
        }

        if (debugLogs)
            Debug.Log($"[EnemyHealth] Damaged '{name}' for {appliedInt}. HP={CurrentHealth}/{MaxHealth}", this);

        if (currentHealth <= 0)
            Die();
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;

        if (debugLogs)
            Debug.Log($"[EnemyHealth] '{name}' death fired (once)", this);

        // Call DropOnDeath if present (same object or parent)
        var dropOnDeath = GetComponent<DropOnDeath>();
        if (dropOnDeath == null)
        {
            dropOnDeath = GetComponentInParent<DropOnDeath>();
        }

        if (dropOnDeath != null && dropOnDeath.enabled)
        {
            dropOnDeath.OnDeath();
        }

        // Fire optional UnityEvent hook (VFX, gates, boss logic later)
        OnDied?.Invoke();

        // Raise code-level death hooks.
        OnDeath?.Invoke(this);
        Died?.Invoke();

        // Centralized cleanup if present (preferred).
        // Fallback to legacy pooling cleanup if not.
        var cleanup = GetComponent<EnemyDeathCleanup>();
        if (cleanup != null)
        {
            cleanup.Run();
        }
        else
        {
            // Disable gameplay-facing parts, then deactivate for pooling shortly after.
            DisableForPooling();
        }
    }

    private void DisableForPooling()
    {
        SetChildCollidersEnabled(false);
        DisableLikelyAiOrMovementBehaviours();
        SetChildRenderersEnabled(false);

        if (_despawnRoutine != null)
            StopCoroutine(_despawnRoutine);
        _despawnRoutine = StartCoroutine(DespawnAfterDelay(despawnDelaySeconds));
    }

    private IEnumerator DespawnAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(Mathf.Max(0.05f, seconds));
        _despawnRoutine = null;
        gameObject.SetActive(false);
    }

    private void SetChildCollidersEnabled(bool enabled)
    {
        var cols = _cachedColliders ?? GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null)
                cols[i].enabled = enabled;
        }
    }

    private void SetChildRenderersEnabled(bool enabled)
    {
        var rends = _cachedRenderers ?? GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++)
        {
            if (rends[i] != null)
                rends[i].enabled = enabled;
        }
    }

    private void DisableLikelyAiOrMovementBehaviours()
    {
        var behaviours = _cachedBehaviours ?? GetComponentsInChildren<Behaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            var b = behaviours[i];
            if (b == null || b == this)
                continue;

            // Keep DropOnDeath enabled so pooling does not break existing drop logic.
            if (b is DropOnDeath)
                continue;

            // Don't blanket-disable everything; only best-effort likely movement/AI.
            var typeName = b.GetType().Name;
            for (int k = 0; k < DisableKeywords.Length; k++)
            {
                if (typeName.IndexOf(DisableKeywords[k], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    b.enabled = false;
                    break;
                }
            }
        }
    }

    private void CacheInitialComponentStates()
    {
        _cachedColliders = GetComponentsInChildren<Collider>(true);
        _cachedColliderEnabled = new bool[_cachedColliders.Length];
        for (int i = 0; i < _cachedColliders.Length; i++)
            _cachedColliderEnabled[i] = _cachedColliders[i] != null && _cachedColliders[i].enabled;

        _cachedRenderers = GetComponentsInChildren<Renderer>(true);
        _cachedRendererEnabled = new bool[_cachedRenderers.Length];
        for (int i = 0; i < _cachedRenderers.Length; i++)
            _cachedRendererEnabled[i] = _cachedRenderers[i] != null && _cachedRenderers[i].enabled;

        _cachedBehaviours = GetComponentsInChildren<Behaviour>(true);
        _cachedBehaviourEnabled = new bool[_cachedBehaviours.Length];
        for (int i = 0; i < _cachedBehaviours.Length; i++)
            _cachedBehaviourEnabled[i] = _cachedBehaviours[i] != null && _cachedBehaviours[i].enabled;
    }

    private void RestoreInitialComponentStates()
    {
        if (_cachedColliders != null && _cachedColliderEnabled != null)
        {
            for (int i = 0; i < _cachedColliders.Length && i < _cachedColliderEnabled.Length; i++)
            {
                if (_cachedColliders[i] != null)
                    _cachedColliders[i].enabled = _cachedColliderEnabled[i];
            }
        }

        if (_cachedRenderers != null && _cachedRendererEnabled != null)
        {
            for (int i = 0; i < _cachedRenderers.Length && i < _cachedRendererEnabled.Length; i++)
            {
                if (_cachedRenderers[i] != null)
                    _cachedRenderers[i].enabled = _cachedRendererEnabled[i];
            }
        }

        if (_cachedBehaviours != null && _cachedBehaviourEnabled != null)
        {
            for (int i = 0; i < _cachedBehaviours.Length && i < _cachedBehaviourEnabled.Length; i++)
            {
                var b = _cachedBehaviours[i];
                if (b == null || b == this)
                    continue;

                b.enabled = _cachedBehaviourEnabled[i];
            }
        }
    }

#if UNITY_EDITOR
    // -------------------------
    // DEBUG HELPERS (Editor Only)
    // -------------------------

    [ContextMenu("TEST: Deal 1 Damage")]
    private void DebugDeal1Damage()
    {
        TakeDamage(1);
    }

    [ContextMenu("TEST: Deal 5 Damage")]
    private void DebugDeal5Damage()
    {
        TakeDamage(5);
    }

    [ContextMenu("TEST: Kill")]
    private void DebugKill()
    {
        TakeDamage(999999);
    }

    [ContextMenu("TEST: Reset Health")]
    private void DebugReset()
    {
        ResetHealth();
    }
#endif
}
