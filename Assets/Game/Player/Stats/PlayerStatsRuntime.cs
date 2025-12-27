using System;
using System.Collections.Generic;
using Abyss.Equipment;
using Abyss.Items;
using UnityEngine;

using AbyssItemType = Abyss.Items.ItemType;
using StatType = Abyssbound.Loot.StatType;

namespace Abyssbound.Stats
{
    [DisallowMultipleComponent]
    public sealed class PlayerStatsRuntime : MonoBehaviour
    {
        [Header("Progression (Leveled)")]
        [SerializeField] private PlayerLeveledStats leveled;

        public PlayerLeveledStats Leveled => leveled;
        public PlayerPrimaryStats GearBonus { get; private set; }
        public PlayerPrimaryStats TotalPrimary { get; private set; }
        public PlayerDerivedStats Derived { get; private set; }

        public event Action<int> OnAttackLevelUp;
        public event Action<StatType, int> OnLevelUp;

        private PlayerEquipment _equipment;
        private PlayerCombatStats _combat;
        private PlayerHealth _health;

        private static Dictionary<string, ItemDefinition> s_DefById;

        private bool _dirty = true;
        private bool _loggedMissingLootBootstrapError;

        // Match PlayerHealth’s slot coverage for non-damage stats.
        private static readonly EquipmentSlot[] s_AllEquipSlots =
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
            try { _combat = GetComponent<PlayerCombatStats>(); } catch { _combat = null; }
            try { _health = GetComponent<PlayerHealth>(); } catch { _health = null; }

            EnsureDefaultLeveledStats();

            // Non-destructive: ensure Defence XP batching is available without prefab edits.
            try
            {
                if (GetComponent<PlayerDefenceXpFromDamageTaken>() == null)
                    gameObject.AddComponent<PlayerDefenceXpFromDamageTaken>();
            }
            catch { }

            EnsureEquipment();
            MarkDirty();
        }

        private void Reset()
        {
            EnsureDefaultLeveledStats();
        }

        private void OnEnable()
        {
            EnsureDefaultLeveledStats();
            EnsureEquipment();
            RebuildNow();
        }

        private void EnsureDefaultLeveledStats()
        {
            // Only set defaults when uninitialized; do not overwrite saved/serialized progression.
            if (leveled.IsAllZero())
                leveled = PlayerLeveledStats.DefaultStarting;
        }

        private void OnDisable()
        {
            if (_equipment != null)
                _equipment.Changed -= OnEquipmentChanged;
        }

        private void Update()
        {
            // Lightweight self-heal if PlayerEquipment is created later.
            if (_equipment == null)
                EnsureEquipment();

            if (_dirty)
                RebuildNow();
        }

        public void MarkDirty()
        {
            _dirty = true;
        }

        public void AddAttackXp(int amount)
        {
            AddXp(StatType.Attack, amount);
        }

        public void RecalculateAttackLevelFromXp()
        {
            RecalculateLevelFromXp(StatType.Attack);
        }

        public void AddXp(StatType stat, int amount)
        {
            if (amount == 0)
                return;

            EnsureDefaultLeveledStats();

            if (!IsSupportedProgressionStat(stat))
                return;

            int beforeLevel = GetLevel(stat);

            int xpBefore = GetXp(stat);
            int xpAfter;
            try { xpAfter = checked(xpBefore + amount); }
            catch { xpAfter = int.MaxValue; }
            xpAfter = Mathf.Max(0, xpAfter);
            SetXp(stat, xpAfter);

            RecalculateLevelFromXp(stat);

            int afterLevel = GetLevel(stat);
            if (afterLevel > beforeLevel)
            {
                try { OnLevelUp?.Invoke(stat, afterLevel); } catch { }

                // Back-compat event.
                if (stat == StatType.Attack)
                {
                    try { OnAttackLevelUp?.Invoke(afterLevel); } catch { }
                }
            }

            MarkDirty();
        }

        public int GetXp(StatType stat)
        {
            EnsureDefaultLeveledStats();

            switch (stat)
            {
                case StatType.Attack: return Mathf.Max(0, leveled.attackXp);
                case StatType.Strength: return Mathf.Max(0, leveled.strengthXp);
                case StatType.DefenseSkill: return Mathf.Max(0, leveled.defenceXp);
                case StatType.RangedSkill: return Mathf.Max(0, leveled.rangedXp);
                case StatType.MagicSkill: return Mathf.Max(0, leveled.magicXp);

                case StatType.Alchemy: return Mathf.Max(0, leveled.alchemyXp);
                case StatType.Mining: return Mathf.Max(0, leveled.miningXp);
                case StatType.Woodcutting: return Mathf.Max(0, leveled.woodcuttingXp);
                case StatType.Forging: return Mathf.Max(0, leveled.forgingXp);
                case StatType.Fishing: return Mathf.Max(0, leveled.fishingXp);
                case StatType.Cooking: return Mathf.Max(0, leveled.cookingXp);
                default: return 0;
            }
        }

        public int GetLevel(StatType stat)
        {
            EnsureDefaultLeveledStats();

            switch (stat)
            {
                case StatType.Attack: return Mathf.Max(1, leveled.attack);
                case StatType.Strength: return Mathf.Max(1, leveled.strength);
                case StatType.DefenseSkill: return Mathf.Max(1, leveled.defence);
                case StatType.RangedSkill: return Mathf.Max(1, leveled.ranged);
                case StatType.MagicSkill: return Mathf.Max(1, leveled.magic);

                case StatType.Alchemy: return Mathf.Max(1, leveled.alchemy);
                case StatType.Mining: return Mathf.Max(1, leveled.mining);
                case StatType.Woodcutting: return Mathf.Max(1, leveled.woodcutting);
                case StatType.Forging: return Mathf.Max(1, leveled.forging);
                case StatType.Fishing: return Mathf.Max(1, leveled.fishing);
                case StatType.Cooking: return Mathf.Max(1, leveled.cooking);
                default: return 1;
            }
        }

        private void SetXp(StatType stat, int xp)
        {
            xp = Mathf.Max(0, xp);
            switch (stat)
            {
                case StatType.Attack: leveled.attackXp = xp; break;
                case StatType.Strength: leveled.strengthXp = xp; break;
                case StatType.DefenseSkill: leveled.defenceXp = xp; break;
                case StatType.RangedSkill: leveled.rangedXp = xp; break;
                case StatType.MagicSkill: leveled.magicXp = xp; break;

                case StatType.Alchemy: leveled.alchemyXp = xp; break;
                case StatType.Mining: leveled.miningXp = xp; break;
                case StatType.Woodcutting: leveled.woodcuttingXp = xp; break;
                case StatType.Forging: leveled.forgingXp = xp; break;
                case StatType.Fishing: leveled.fishingXp = xp; break;
                case StatType.Cooking: leveled.cookingXp = xp; break;
            }
        }

        private void RecalculateLevelFromXp(StatType stat)
        {
            EnsureDefaultLeveledStats();

            int per = Mathf.Max(1, CombatXpTuning.XpPerLevel);
            int xp = GetXp(stat);
            int computed = 1 + (xp / per);
            computed = Mathf.Max(1, computed);

            switch (stat)
            {
                case StatType.Attack: leveled.attack = computed; break;
                case StatType.Strength: leveled.strength = computed; break;
                case StatType.DefenseSkill: leveled.defence = computed; break;
                case StatType.RangedSkill: leveled.ranged = computed; break;
                case StatType.MagicSkill: leveled.magic = computed; break;

                case StatType.Alchemy: leveled.alchemy = computed; break;
                case StatType.Mining: leveled.mining = computed; break;
                case StatType.Woodcutting: leveled.woodcutting = computed; break;
                case StatType.Forging: leveled.forging = computed; break;
                case StatType.Fishing: leveled.fishing = computed; break;
                case StatType.Cooking: leveled.cooking = computed; break;
            }
        }

        private static bool IsSupportedProgressionStat(StatType stat)
        {
            switch (stat)
            {
                case StatType.Attack:
                case StatType.Strength:
                case StatType.DefenseSkill:
                case StatType.RangedSkill:
                case StatType.MagicSkill:

                case StatType.Alchemy:
                case StatType.Mining:
                case StatType.Woodcutting:
                case StatType.Forging:
                case StatType.Fishing:
                case StatType.Cooking:
                    return true;
                default:
                    return false;
            }
        }

        public void RebuildNow()
        {
            _dirty = false;

            EnsureDefaultLeveledStats();

            // Regression guard: if the loot bootstrap is missing, avoid null-refs and
            // fall back to zeros (derived will still include base damage/HP if available).
            if (!HasLootBootstrap())
            {
                if (!_loggedMissingLootBootstrapError)
                {
                    _loggedMissingLootBootstrapError = true;
                    Debug.LogError("[PlayerStatsRuntime] Loot bootstrap missing (Resources/Loot/Bootstrap.asset). Stats will fall back to base-only/zeros. Run Tools/Abyssbound/Loot/Create Starter Loot Content.", this);
                }

                GearBonus = PlayerPrimaryStats.Zero;
                TotalPrimary = leveled.ToPrimaryStats();

                int baseDamage = _combat != null ? _combat.BaseDamage : 0;
                int baseMaxHp = _health != null ? _health.BaseMaxHealth : 1;
                Derived = StatCalculator.ComputeDerived(TotalPrimary, baseDamage, baseMaxHp, 0, 0, 0);
                return;
            }

            EnsureEquipment();

            var gearBonus = PlayerPrimaryStats.Zero;

            int equipDamageBonus = 0;
            int equipMaxHpBonus = 0;
            int equipDrFlat = 0;

            if (_equipment != null)
            {
                // Avoid double-counting the same itemId (e.g., two-handed weapons can occupy both hands).
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < s_AllEquipSlots.Length; i++)
                {
                    var slot = s_AllEquipSlots[i];
                    string itemId = null;
                    try { itemId = _equipment.Get(slot); }
                    catch { itemId = null; }

                    if (string.IsNullOrWhiteSpace(itemId))
                        continue;

                    if (!seen.Add(itemId))
                        continue;

                    AccumulateItem(slot, itemId, ref gearBonus, ref equipDamageBonus, ref equipMaxHpBonus, ref equipDrFlat);
                }
            }

            // Set bonuses: accumulate ALL non-percent StatMods from active tiers.
            AccumulateSetBonuses(ref gearBonus, ref equipDamageBonus, ref equipMaxHpBonus, ref equipDrFlat);

            GearBonus = gearBonus;

            // TotalPrimary = Leveled + GearBonus
            var total = leveled.ToPrimaryStats();
            total.attack += gearBonus.attack;
            total.strength += gearBonus.strength;
            total.defense += gearBonus.defense;
            total.ranged += gearBonus.ranged;
            total.magic += gearBonus.magic;
            total.alchemy += gearBonus.alchemy;
            total.mining += gearBonus.mining;
            total.woodcutting += gearBonus.woodcutting;
            total.forging += gearBonus.forging;
            total.fishing += gearBonus.fishing;
            total.cooking += gearBonus.cooking;

            TotalPrimary = total;

            int baseDmg = _combat != null ? _combat.BaseDamage : 0;
            int baseHp = _health != null ? _health.BaseMaxHealth : 1;
            Derived = StatCalculator.ComputeDerived(TotalPrimary, baseDmg, baseHp, equipDamageBonus, equipMaxHpBonus, equipDrFlat);
        }

        public void DumpToLog()
        {
            try
            {
                Debug.Log("[PlayerStatsRuntime] LEVELED\n" + leveled.ToMultilineString(), this);
                Debug.Log("[PlayerStatsRuntime] GEAR BONUS\n" + GearBonus.ToMultilineString(), this);
                Debug.Log("[PlayerStatsRuntime] TOTAL PRIMARY\n" + TotalPrimary.ToMultilineString(), this);
                Debug.Log("[PlayerStatsRuntime] DERIVED\n" + Derived.ToMultilineString(), this);
            }
            catch { }
        }

        private void EnsureEquipment()
        {
            if (_equipment != null)
                return;

            try { _equipment = PlayerEquipmentResolver.GetOrFindOrCreate(); }
            catch { _equipment = null; }

            if (_equipment != null)
            {
                _equipment.Changed -= OnEquipmentChanged;
                _equipment.Changed += OnEquipmentChanged;
            }
        }

        private void OnEquipmentChanged()
        {
            MarkDirty();
        }

        private static bool HasLootBootstrap()
        {
            // Keep this lightweight: Resources.Load returns null if missing.
            // LootRegistryRuntime already warns once, but we need a hard guard for callers.
            try
            {
                var bootstrap = Resources.Load<Abyssbound.Loot.LootRegistryBootstrapSO>("Loot/Bootstrap");
                return bootstrap != null;
            }
            catch
            {
                return false;
            }
        }

        private void AccumulateItem(
            EquipmentSlot slot,
            string itemId,
            ref PlayerPrimaryStats gearBonus,
            ref int equipDamageBonus,
            ref int equipMaxHpBonus,
            ref int equipDrFlat)
        {
            // Rolled loot instance support.
            try
            {
                var reg = Abyssbound.Loot.LootRegistryRuntime.GetOrCreate();
                if (reg != null)
                {
                    // If stored id is a rolled instance, pull mods.
                    if (reg.TryGetRolledInstance(itemId, out var inst) && inst != null)
                    {
                        AccumulateModsFromInstance(slot, reg, inst, ref gearBonus, ref equipDamageBonus, ref equipMaxHpBonus, ref equipDrFlat);
                        return;
                    }

                    // If stored id is a base item id, apply base stats directly.
                    if (reg.TryGetItem(itemId, out var baseItem) && baseItem != null)
                    {
                        AccumulateModsFromBaseItem(slot, baseItem.baseStats, ref gearBonus, ref equipDamageBonus, ref equipMaxHpBonus, ref equipDrFlat);
                        return;
                    }
                }
            }
            catch { }

            // Legacy fallback: ItemDefinition.
            var def = ResolveItemDefinition(itemId);
            if (def == null)
                return;

            // Damage bonuses are weapon-only and hand-only (match existing PlayerCombatStats behavior).
            if (slot == EquipmentSlot.LeftHand || slot == EquipmentSlot.RightHand)
            {
                try
                {
                    if (def.itemType == AbyssItemType.Weapon)
                        equipDamageBonus += Mathf.Max(0, def.DamageBonus);
                }
                catch { }
            }

            // Health/DR apply from any equipped item (match existing PlayerHealth behavior).
            try { equipMaxHpBonus += Mathf.Max(0, def.MaxHealthBonus); } catch { }
            try { equipDrFlat += Mathf.Max(0, def.DamageReductionFlat); } catch { }
        }

        private static void AccumulateModsFromInstance(
            EquipmentSlot slot,
            Abyssbound.Loot.LootRegistryRuntime registry,
            Abyssbound.Loot.ItemInstance inst,
            ref PlayerPrimaryStats gearBonus,
            ref int equipDamageBonus,
            ref int equipMaxHpBonus,
            ref int equipDrFlat)
        {
            if (registry == null || inst == null)
                return;

            List<Abyssbound.Loot.StatMod> mods = null;
            try { mods = inst.GetAllStatMods(registry); } catch { mods = null; }

            AccumulateModsFromBaseItem(slot, mods, ref gearBonus, ref equipDamageBonus, ref equipMaxHpBonus, ref equipDrFlat);
        }

        private static void AccumulateModsFromBaseItem(
            EquipmentSlot slot,
            IList<Abyssbound.Loot.StatMod> mods,
            ref PlayerPrimaryStats gearBonus,
            ref int equipDamageBonus,
            ref int equipMaxHpBonus,
            ref int equipDrFlat)
        {
            if (mods == null || mods.Count == 0)
                return;

            for (int i = 0; i < mods.Count; i++)
            {
                var m = mods[i];

                // Percent stacking is intentionally ignored until defined.
                if (m.percent)
                    continue;

                int v;
                try { v = Mathf.RoundToInt(m.value); }
                catch { continue; }

                v = Mathf.Max(0, v);
                if (v == 0)
                    continue;

                // Gear bonuses: accumulate OSRS-style primary stats only.
                // IMPORTANT: Gear/affixes/sets must never modify leveled stats.
                gearBonus.Add(m.stat, v);

                // Derived compatibility stats:
                switch (m.stat)
                {
                    case Abyssbound.Loot.StatType.MaxHealth:
                        equipMaxHpBonus += v;
                        break;

                    case Abyssbound.Loot.StatType.Defense:
                        equipDrFlat += v;
                        break;

                    case Abyssbound.Loot.StatType.MeleeDamage:
                    case Abyssbound.Loot.StatType.RangedDamage:
                    case Abyssbound.Loot.StatType.MagicDamage:
                        // Damage is hand-only (match existing PlayerCombatStats behavior).
                        if (slot == EquipmentSlot.LeftHand || slot == EquipmentSlot.RightHand)
                            equipDamageBonus += v;
                        break;
                }
            }
        }

        private static void AccumulateSetBonuses(
            ref PlayerPrimaryStats gearBonus,
            ref int equipDamageBonus,
            ref int equipMaxHpBonus,
            ref int equipDrFlat)
        {
            Abyssbound.Loot.EquippedSetTracker tracker = null;
            try { tracker = Abyssbound.Loot.EquippedSetTracker.GetOrCreate(); }
            catch { tracker = null; }

            if (tracker == null)
                return;

            try { tracker.ForceRebuild(); }
            catch { }

            var counts = tracker.GetAllEquippedSetCounts();
            if (counts == null || counts.Count == 0)
                return;

            foreach (var kvp in counts)
            {
                var set = kvp.Key;
                int equipped = kvp.Value;

                if (set == null || equipped <= 0)
                    continue;

                var tiers = set.bonuses;
                if (tiers == null || tiers.Count == 0)
                    continue;

                for (int ti = 0; ti < tiers.Count; ti++)
                {
                    var tier = tiers[ti];
                    if (tier == null) continue;

                    int required = tier.requiredPieces;
                    if (required <= 0) continue;
                    if (equipped < required) continue;

                    var mods = tier.modifiers;
                    if (mods == null || mods.Count == 0)
                        continue;

                    for (int mi = 0; mi < mods.Count; mi++)
                    {
                        var m = mods[mi];
                        if (m.percent) continue;

                        int v;
                        try { v = Mathf.RoundToInt(m.value); }
                        catch { continue; }

                        v = Mathf.Max(0, v);
                        if (v == 0)
                            continue;

                        // Set bonuses are treated as gear-like bonuses.
                        gearBonus.Add(m.stat, v);

                        // Derived compatibility accumulation.
                        switch (m.stat)
                        {
                            case Abyssbound.Loot.StatType.MaxHealth:
                                equipMaxHpBonus += v;
                                break;
                            case Abyssbound.Loot.StatType.Defense:
                                equipDrFlat += v;
                                break;
                            case Abyssbound.Loot.StatType.MeleeDamage:
                            case Abyssbound.Loot.StatType.RangedDamage:
                            case Abyssbound.Loot.StatType.MagicDamage:
                                equipDamageBonus += v;
                                break;
                        }
                    }
                }
            }
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
                    try { id = def.itemId; }
                    catch { id = null; }

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
}
