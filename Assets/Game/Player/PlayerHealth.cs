using System;
using UnityEngine;
using Abyss.Dev;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int currentHealth;

    public int MaxHealth => Mathf.Max(1, maxHealth);
    public int CurrentHealth => Mathf.Clamp(currentHealth, 0, MaxHealth);
    public float Normalized => MaxHealth <= 0 ? 0f : (float)CurrentHealth / MaxHealth;
    public bool IsDead => CurrentHealth <= 0;

    public event Action<float> OnHealthChanged;
    public event Action<int, int> HealthChanged;

    private void Awake()
    {
        if (currentHealth <= 0)
            currentHealth = MaxHealth;

        RaiseChanged();
    }

    public void ResetHealth()
    {
        currentHealth = MaxHealth;
        RaiseChanged();
    }

    public void TakeDamage(int amount)
    {
        if (IsDead) return;
        if (amount <= 0) return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (DevCheats.GodModeEnabled)
            return;
#endif

        currentHealth = Mathf.Max(0, currentHealth - amount);
        RaiseChanged();
    }

    public void Heal(int amount)
    {
        if (IsDead) return;
        if (amount <= 0) return;

        currentHealth = Mathf.Min(MaxHealth, currentHealth + amount);
        RaiseChanged();
    }

    private void RaiseChanged()
    {
        try { OnHealthChanged?.Invoke(Normalized); }
        catch (Exception ex) { Debug.LogError($"[PlayerHealth] OnHealthChanged event threw: {ex.Message}", this); }

        try { HealthChanged?.Invoke(CurrentHealth, MaxHealth); }
        catch (Exception ex) { Debug.LogError($"[PlayerHealth] HealthChanged event threw: {ex.Message}", this); }
    }
}
