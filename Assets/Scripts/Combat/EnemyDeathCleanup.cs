using System;
using System.Collections;
using UnityEngine;

// Centralized death cleanup for enemies.
// - Disables interaction/targeting immediately.
// - Plays optional animator trigger.
// - Deactivates the enemy root after a short delay (pooling-friendly).
[DisallowMultipleComponent]
public sealed class EnemyDeathCleanup : MonoBehaviour
{
    [Header("Despawn")]
    [SerializeField] private float deactivateDelaySeconds = 0.25f;

    [Header("Animator (optional)")]
    [SerializeField] private string dieTriggerName = "Die";

    private EnemyHealth _health;
    private bool _ran;
    private Coroutine _routine;

    public event Action OnDied;

    private void Awake()
    {
        _health = GetComponent<EnemyHealth>();
    }

    private void OnEnable()
    {
        _ran = false;

        if (_health == null)
            _health = GetComponent<EnemyHealth>();

        if (_health != null)
        {
            _health.OnDeath += OnHealthDeath;
            _health.Died += OnHealthDiedLegacy;

            // If re-enabled from a pool in a weird state, don't auto-cleanup.
        }
    }

    private void OnDisable()
    {
        if (_health != null)
        {
            _health.OnDeath -= OnHealthDeath;
            _health.Died -= OnHealthDiedLegacy;
        }

        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
    }

    private void OnHealthDeath(EnemyHealth dead)
    {
        if (dead == null)
            return;

        if (!ReferenceEquals(dead, _health))
            return;

        Run();
    }

    private void OnHealthDiedLegacy()
    {
        if (_health != null && _health.IsDead)
            Run();
    }

    // Allows EnemyHealth to call directly (optional).
    public void Run()
    {
        if (_ran)
            return;

        _ran = true;

        // Immediately stop interaction + targeting.
        DisableAllColliders();
        DisableEnemyBehaviours();

        // Optional animation.
        TryTriggerDieAnimation();

        // Clear player target references if they point at this.
        TryClearPlayerTargets();

        OnDied?.Invoke();

        if (_routine != null)
            StopCoroutine(_routine);

        _routine = StartCoroutine(DeactivateAfterDelay());
    }

    private IEnumerator DeactivateAfterDelay()
    {
        float delay = Mathf.Max(0.01f, deactivateDelaySeconds);
        yield return new WaitForSeconds(delay);

        _routine = null;

        // Pooling-friendly: deactivate instead of destroying.
        gameObject.SetActive(false);
    }

    private void DisableAllColliders()
    {
        try
        {
            var cols3d = GetComponentsInChildren<Collider>(true);
            if (cols3d != null)
            {
                for (int i = 0; i < cols3d.Length; i++)
                {
                    if (cols3d[i] != null)
                        cols3d[i].enabled = false;
                }
            }
        }
        catch { }

        try
        {
            var cols2d = GetComponentsInChildren<Collider2D>(true);
            if (cols2d != null)
            {
                for (int i = 0; i < cols2d.Length; i++)
                {
                    if (cols2d[i] != null)
                        cols2d[i].enabled = false;
                }
            }
        }
        catch { }
    }

    private void DisableEnemyBehaviours()
    {
        // Disable known components explicitly.
        var aggro = GetComponent<EnemyAggroChase>();
        if (aggro != null) aggro.enabled = false;

        var melee = GetComponent<EnemyMeleeAttack>();
        if (melee != null) melee.enabled = false;

        // Disable likely AI/brain/targeting scripts by name (root + children).
        // Keep EnemyHealth enabled so pooling reset can work when reactivated.
        // Keep Animator enabled to allow death animation.
        try
        {
            var behaviours = GetComponentsInChildren<Behaviour>(true);
            if (behaviours == null) return;

            for (int i = 0; i < behaviours.Length; i++)
            {
                var b = behaviours[i];
                if (b == null) continue;

                if (b is EnemyHealth) continue;
                if (ReferenceEquals(b, this)) continue;
                if (b is Animator) continue;

                var typeName = b.GetType().Name;

                if (ContainsAny(typeName,
                    "Aggro",
                    "Chase",
                    "AI",
                    "Brain",
                    "Controller",
                    "Mover",
                    "Nav",
                    "Attack",
                    "Selectable",
                    "Targetable",
                    "ClickToAttack",
                    "ClickTo",
                    "Interact"))
                {
                    b.enabled = false;
                }
            }
        }
        catch
        {
            // silent
        }
    }

    private void TryTriggerDieAnimation()
    {
        if (string.IsNullOrEmpty(dieTriggerName))
            return;

        try
        {
            var anim = GetComponentInChildren<Animator>(true);
            if (anim == null) return;

            var ps = anim.parameters;
            if (ps == null) return;

            for (int i = 0; i < ps.Length; i++)
            {
                if (ps[i].name == dieTriggerName)
                {
                    anim.SetTrigger(dieTriggerName);
                    return;
                }
            }
        }
        catch
        {
            // silent
        }
    }

    private void TryClearPlayerTargets()
    {
        if (_health == null)
            return;

        // CombatLoopController already subscribes to EnemyHealth death and clears its target.
        // This is an extra safety net for any other systems that only track SimplePlayerCombat.SelectedTarget.
        try
        {
#if UNITY_2022_2_OR_NEWER
            var combat = UnityEngine.Object.FindFirstObjectByType<SimplePlayerCombat>(FindObjectsInactive.Exclude);
#else
            var combat = UnityEngine.Object.FindObjectOfType<SimplePlayerCombat>();
#endif
            if (combat != null && combat.SelectedTarget == _health)
                combat.SelectedTarget = null;
        }
        catch
        {
            // silent
        }
    }

    private static bool ContainsAny(string haystack, params string[] needles)
    {
        if (string.IsNullOrEmpty(haystack) || needles == null)
            return false;

        for (int i = 0; i < needles.Length; i++)
        {
            var n = needles[i];
            if (string.IsNullOrEmpty(n))
                continue;

            if (haystack.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }
}
