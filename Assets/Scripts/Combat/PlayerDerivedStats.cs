using UnityEngine;
using System.Collections.Generic;
using Abyssbound.Loot;
using Abyss.Equipment;
using Abyss.Items;

namespace Abyssbound.Combat
{
    /// <summary>
    /// The "Stats Awakening" - Unifying OSRS Primary Stats (Str/Atk/Def) with Diablo Derived Stats (DMG/HP/DR).
    /// Inspired by SpawnPK mechanics: Untradeable power spikes through gear stats.
    /// </summary>
    public class PlayerDerivedStats : MonoBehaviour
    {
        [Header("Base Stats (Leveled)")]
        public int baseAttack = 1;
        public int baseStrength = 1;
        public int baseDefence = 1;

        [Header("Calculation Constants (Tuning)")]
        [Tooltip("How much Melee Damage each point of Strength adds.")]
        public float kStrengthToMeleeDamage = 0.5f;
        [Tooltip("Base hit chance before Attack/Defence scaling.")]
        public float baseHitChance = 0.60f;
        [Tooltip("How much each point of Attack advantage adds to hit chance.")]
        public float kAttackToHitChance = 0.03f;

        // Runtime Aggregations
        public int TotalAttack { get; private set; }
        public int TotalStrength { get; private set; }
        public int TotalDefence { get; private set; }
        
        public float FinalMeleeDamageScalar { get; private set; }
        public float FinalHitChance { get; private set; }

        private PlayerEquipment _equipment;
        private PlayerCombatStats _combatStats;
        private PlayerHealth _health;

        private void Awake()
        {
            _equipment = GetComponent<PlayerEquipment>();
            _combatStats = GetComponent<PlayerCombatStats>();
            _health = GetComponent<PlayerHealth>();
        }

        private void Start()
        {
            RefreshAllStats();
        }

        /// <summary>
        /// Call this whenever equipment changes or a level-up occurs.
        /// </summary>
        public void RefreshAllStats()
        {
            int gearAttack = 0;
            int gearStrength = 0;
            int gearDefence = 0;

            // 1. Accumulate stats from all equipment slots (Loot V2 + Sets)
            // Note: This logic will eventually move to a central StatAccumulator to support percent mods.
            var registry = LootRegistryRuntime.Instance;
            foreach (var slot in PlayerEquipment.AllSlots)
            {
                if (slot == EquipmentSlot.None) continue;
                string itemId = _equipment.Get(slot);
                if (string.IsNullOrEmpty(itemId)) continue;

                if (registry != null && registry.TryGetRolledInstance(itemId, out var inst))
                {
                    var mods = inst.GetAllStatMods(registry);
                    foreach (var m in mods)
                    {
                        if (m.stat == StatType.Attack) gearAttack += (int)m.value;
                        if (m.stat == StatType.Strength) gearStrength += (int)m.value;
                        if (m.stat == StatType.DefenseSkill) gearDefence += (int)m.value;
                    }
                }
            }

            // 2. Set Bonus Application
            // TODO: Hook into SetBonusRuntime to pull primary stat bonuses

            TotalAttack = baseAttack + gearAttack;
            TotalStrength = baseStrength + gearStrength;
            TotalDefence = baseDefence + gearDefence;

            // 3. Derived Calculation
            // Formula from ABYSSBOUND_STATS_MODEL.md
            FinalMeleeDamageScalar = (TotalStrength - 1) * kStrengthToMeleeDamage;
            
            // Placeholder HitChance (vs a generic target for HUD display)
            FinalHitChance = Mathf.Clamp(baseHitChance + (TotalAttack * kAttackToHitChance), 0.05f, 0.95f);

            Debug.Log($"[Stats Awakening] Total Strength: {TotalStrength} (Gear: {gearStrength}) -> Bonus DMG: {FinalMeleeDamageScalar}");
        }
    }
}
