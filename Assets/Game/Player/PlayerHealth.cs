using System;
using System.Collections.Generic;
using Abyss.Dev;
using Abyss.Equipment;
using Abyss.Items;
using UnityEngine;
using UnityEngine.Serialization;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    [FormerlySerializedAs("maxHealth")]
    [SerializeField] private int baseMaxHealth = 100;
    [SerializeField] private int currentHealth;

    [Header("Debug")]
    [SerializeField] private bool debugMitigationLogs;

    public int BaseMaxHealth => Mathf.Max(1, baseMaxHealth);
    public int EquipmentMaxHealthBonus { get; private set; }
    public int EquipmentDamageReductionFlat { get; private set; }
    public int TotalDamageReductionFlat => Mathf.Max(0, EquipmentDamageReductionFlat);
    public int MaxHealth => Mathf.Max(1, BaseMaxHealth + Mathf.Max(0, EquipmentMaxHealthBonus));
    public int CurrentHealth => Mathf.Clamp(currentHealth, 0, MaxHealth);
    public float Normalized => MaxHealth <= 0 ? 0f : (float)CurrentHealth / MaxHealth;
    public bool IsDead => CurrentHealth <= 0;

    public event Action<float> OnHealthChanged;
    public event Action<int, int> HealthChanged;

    private PlayerEquipment _equipment;
    private static Dictionary<string, ItemDefinition> s_DefById;

    private static readonly EquipmentSlot[] s_EquipSlots =
    {
        EquipmentSlot.Helm,
        EquipmentSlot.Chest,
        EquipmentSlot.Legs,
        EquipmentSlot.Belt,
        EquipmentSlot.Gloves,
        EquipmentSlot.Boots,
        EquipmentSlot.Cape,
        EquipmentSlot.Ammo,
        EquipmentSlot.LeftHand,
        EquipmentSlot.RightHand,
        EquipmentSlot.Ring1,
        EquipmentSlot.Ring2,
        EquipmentSlot.Amulet,
        EquipmentSlot.Artifact,
    };

    private void Awake()
    {
        if (currentHealth <= 0)
            currentHealth = MaxHealth;

        currentHealth = Mathf.Clamp(currentHealth, 0, MaxHealth);

        RaiseChanged();
    }

    private void OnEnable()
    {
        EnsureEquipment();
        RecomputeEquipmentBonusAndApply();
    }

    private void OnDisable()
    {
        if (_equipment != null)
            _equipment.Changed -= OnEquipmentChanged;
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

        int mitigated = Mathf.Max(1, amount - TotalDamageReductionFlat);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugMitigationLogs)
            Debug.Log($"[MIT] incoming={amount} flat={TotalDamageReductionFlat} final={mitigated}", this);
#endif

        currentHealth = Mathf.Max(0, currentHealth - mitigated);
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

    private void EnsureEquipment()
    {
        if (_equipment != null)
            return;

        try
        {
            _equipment = PlayerEquipmentResolver.GetOrFindOrCreate();
        }
        catch
        {
            _equipment = null;
        }

        if (_equipment != null)
        {
            _equipment.Changed -= OnEquipmentChanged;
            _equipment.Changed += OnEquipmentChanged;
        }
    }

    private void OnEquipmentChanged()
    {
        RecomputeEquipmentBonusAndApply();
    }

    private void RecomputeEquipmentBonusAndApply()
    {
        EnsureEquipment();

        int maxHpBonus = 0;
        int flatMitigation = 0;

        if (_equipment != null)
        {
            // Avoid double-counting the same itemId (e.g., two-handed weapons can occupy both hands).
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < s_EquipSlots.Length; i++)
            {
                string itemId = null;
                try { itemId = _equipment.Get(s_EquipSlots[i]); }
                catch { itemId = null; }

                if (string.IsNullOrWhiteSpace(itemId))
                    continue;

                if (!seen.Add(itemId))
                    continue;

                // Rolled loot instance support.
                try
                {
                    var reg = Abyssbound.Loot.LootRegistryRuntime.GetOrCreate();
                    if (reg != null && reg.TryGetRolledInstance(itemId, out var inst) && inst != null)
                    {
                        var mods = inst.GetAllStatMods(reg);
                        if (mods != null)
                        {
                            for (int mi = 0; mi < mods.Count; mi++)
                            {
                                var m = mods[mi];
                                if (m.percent) continue;

                                switch (m.stat)
                                {
                                    case Abyssbound.Loot.StatType.MaxHealth:
                                        maxHpBonus += Mathf.Max(0, Mathf.RoundToInt(m.value));
                                        break;
                                    case Abyssbound.Loot.StatType.Defense:
                                        flatMitigation += Mathf.Max(0, Mathf.RoundToInt(m.value));
                                        break;
                                }
                            }
                        }

                        continue;
                    }
                }
                catch { }

                var def = ResolveItemDefinition(itemId);
                if (def == null)
                    continue;

                try
                {
                    maxHpBonus += Mathf.Max(0, def.MaxHealthBonus);
                    flatMitigation += Mathf.Max(0, def.DamageReductionFlat);
                }
                catch { }
            }
        }

        EquipmentMaxHealthBonus = maxHpBonus;
        EquipmentDamageReductionFlat = flatMitigation;

        // Clamp whenever effective max changes.
        currentHealth = Mathf.Clamp(currentHealth, 0, MaxHealth);

        Debug.Log($"[HP] Base={BaseMaxHealth} EquipBonus={EquipmentMaxHealthBonus} Max={MaxHealth} Current={CurrentHealth}", this);
        Debug.Log($"[MIT] Flat={TotalDamageReductionFlat}", this);
        RaiseChanged();
    }

    private static ItemDefinition ResolveItemDefinition(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        s_DefById ??= BuildIndex();
        if (s_DefById != null && s_DefById.TryGetValue(itemId, out var def) && def != null)
            return def;

        // Best-effort: rebuild once (covers domain reload / asset load order).
        s_DefById = BuildIndex();
        if (s_DefById != null && s_DefById.TryGetValue(itemId, out var refreshed))
            return refreshed;

        return null;
    }

    private static Dictionary<string, ItemDefinition> BuildIndex()
    {
        var map = new Dictionary<string, ItemDefinition>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var defs = Resources.FindObjectsOfTypeAll<ItemDefinition>();
            if (defs == null)
                return map;

            for (int i = 0; i < defs.Length; i++)
            {
                var def = defs[i];
                if (def == null)
                    continue;

                string id = null;
                try { id = def.itemId; } catch { id = null; }

                if (string.IsNullOrWhiteSpace(id))
                    continue;

                if (!map.ContainsKey(id))
                    map[id] = def;
            }
        }
        catch { }

        return map;
    }
}
