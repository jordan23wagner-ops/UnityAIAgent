using System;
using System.Collections.Generic;
using Abyss.Equipment;
using Abyss.Items;
using UnityEngine;
using Abyssbound.Stats;

using AbyssItemType = Abyss.Items.ItemType;

[DisallowMultipleComponent]
public sealed class PlayerCombatStats : MonoBehaviour
{
    [Header("Base")]
    [SerializeField] private int baseDamage = 3;

    public int BaseDamage => baseDamage;

    public int EquipmentDamageBonus { get; private set; }

    public int StrengthMeleeDamageBonus { get; private set; }

    private int _damageFinal;

    public int DamageFinal => Mathf.Max(1, _damageFinal > 0 ? _damageFinal : (BaseDamage + EquipmentDamageBonus));

    private PlayerEquipment _equipment;

    private PlayerStatsRuntime _stats;

    private static Dictionary<string, ItemDefinition> s_DefById;

    private void OnEnable()
    {
        EnsureEquipment();
        EnsureStatsRuntime();
        Recompute();
    }

    private void OnDisable()
    {
        if (_equipment != null)
            _equipment.Changed -= OnEquipmentChanged;
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

    private void EnsureStatsRuntime()
    {
        if (_stats != null)
            return;

        try { _stats = GetComponent<PlayerStatsRuntime>(); }
        catch { _stats = null; }

        if (_stats == null)
        {
            // Keep integration compile-safe without scene edits.
            try { _stats = gameObject.AddComponent<PlayerStatsRuntime>(); }
            catch { _stats = null; }
        }
    }

    private void OnEquipmentChanged()
    {
        Recompute();
    }

    private void Recompute()
    {
        EnsureEquipment();

        EnsureStatsRuntime();

        int equipBonus = 0;
        int strBonus = 0;
        int final = Mathf.Max(1, BaseDamage);
        if (_stats != null)
        {
            try { _stats.RebuildNow(); }
            catch { }

            try
            {
                equipBonus = _stats.Derived.equipmentDamageBonus;
                strBonus = _stats.Derived.strengthMeleeDamageBonus;
                final = _stats.Derived.damageFinal;
            }
            catch
            {
                equipBonus = 0;
                strBonus = 0;
                final = Mathf.Max(1, BaseDamage);
            }
        }

        EquipmentDamageBonus = Mathf.Max(0, equipBonus);
        StrengthMeleeDamageBonus = Mathf.Max(0, strBonus);
        _damageFinal = Mathf.Max(1, final);

        Debug.Log($"[STATS] Base={BaseDamage} EquipBonus={EquipmentDamageBonus} StrBonus={StrengthMeleeDamageBonus} Final={DamageFinal}", this);
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
