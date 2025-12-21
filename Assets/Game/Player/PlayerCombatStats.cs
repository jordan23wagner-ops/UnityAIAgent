using System;
using System.Collections.Generic;
using Abyss.Equipment;
using Abyss.Items;
using UnityEngine;

using AbyssItemType = Abyss.Items.ItemType;

[DisallowMultipleComponent]
public sealed class PlayerCombatStats : MonoBehaviour
{
    [Header("Base")]
    [SerializeField] private int baseDamage = 3;

    public int BaseDamage => baseDamage;

    public int EquipmentDamageBonus { get; private set; }

    public int DamageFinal => Mathf.Max(1, BaseDamage + EquipmentDamageBonus);

    private PlayerEquipment _equipment;

    private static Dictionary<string, ItemDefinition> s_DefById;

    private void OnEnable()
    {
        EnsureEquipment();
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

    private void OnEquipmentChanged()
    {
        Recompute();
    }

    private void Recompute()
    {
        EnsureEquipment();

        int bonus = 0;

        if (_equipment != null)
        {
            // Simplest consistent rule:
            // - Sum DamageBonus across both hands.
            // - If a two-handed item is represented by the same itemId in both hands, count it once.
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AccumulateSlot(EquipmentSlot.RightHand, seen, ref bonus);
            AccumulateSlot(EquipmentSlot.LeftHand, seen, ref bonus);
        }

        EquipmentDamageBonus = bonus;
        Debug.Log($"[STATS] Base={BaseDamage} EquipBonus={EquipmentDamageBonus} Final={DamageFinal}", this);
    }

    private void AccumulateSlot(EquipmentSlot slot, HashSet<string> seen, ref int bonus)
    {
        if (_equipment == null)
            return;

        string itemId = null;
        try { itemId = _equipment.Get(slot); } catch { itemId = null; }

        if (string.IsNullOrWhiteSpace(itemId))
            return;

        if (seen != null && !seen.Add(itemId))
            return;

        var def = ResolveItemDefinition(itemId);
        if (def == null)
            return;

        try
        {
            if (def.itemType != AbyssItemType.Weapon)
                return;
        }
        catch { }

        try
        {
            bonus += Mathf.Max(0, def.DamageBonus);
        }
        catch { }
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
