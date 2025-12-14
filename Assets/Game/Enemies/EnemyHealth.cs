using UnityEngine;
using UnityEngine.Events;

public class EnemyHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 10;
    [SerializeField] private int currentHealth;

    [Header("Death Events")]
    public UnityEvent OnDied;

    private bool isDead;

    private void Awake()
    {
        ResetHealth();
    }

    public void ResetHealth()
    {
        currentHealth = Mathf.Max(1, maxHealth);
        isDead = false;
    }

    public void TakeDamage(int amount)
    {
        if (isDead)
            return;

        if (amount <= 0)
            return;

        currentHealth -= amount;

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;

        // Call DropOnDeath if present (same object or parent)
        var dropOnDeath = GetComponent<DropOnDeath>();
        if (dropOnDeath == null)
        {
            dropOnDeath = GetComponentInParent<DropOnDeath>();
        }

        if (dropOnDeath != null)
        {
            dropOnDeath.OnDeath();
        }

        // Fire optional UnityEvent hook (VFX, gates, boss logic later)
        OnDied?.Invoke();

        // NOTE:
        // We intentionally do NOT destroy the GameObject yet.
        // Corpse handling, despawn timing, or pooling comes later.
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
