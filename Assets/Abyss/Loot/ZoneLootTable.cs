using System;
using System.Collections.Generic;
using Abyss.Items;
using UnityEngine;
using UnityEngine.Serialization;

namespace Abyss.Loot
{
    [CreateAssetMenu(menuName = "Abyss/Loot/Zone Loot Table", fileName = "ZoneLootTable")]
    public sealed class ZoneLootTable : ScriptableObject
    {
        [Serializable]
        public struct RarityChances
        {
            [Range(0f, 1f)] public float common;
            [Range(0f, 1f)] public float uncommon;
            [Range(0f, 1f)] public float magic;
            [Range(0f, 1f)] public float rare;
            [Range(0f, 1f)] public float epic;
            [Range(0f, 1f)] public float legendary;
            [Range(0f, 1f)] public float set;
            [Range(0f, 1f)] public float radiant;

            public float Total =>
                common + uncommon + magic + rare + epic + legendary + set + radiant;
        }

        [Header("Pools")]
        [Tooltip("All zone items eligible for zone-wide drops (equipment + consumables + misc).")]
        public List<ItemDefinition> zonePool = new();

        [Tooltip("Additional materials pool; rolled separately if enabled.")]
        public List<ItemDefinition> materialsPool = new();

        [Tooltip("Boss/unique pool; typically rolled only for bosses.")]
        public List<ItemDefinition> bossUniquesPool = new();

        [Header("Rarity Chances (per roll)")]
        [Tooltip("Baseline odds for trash mobs (see master plan).")]
        [FormerlySerializedAs("fodderChances")] public RarityChances trashChances = new RarityChances
        {
            common = 0.05f,
            uncommon = 0.03f,
            magic = 0.02f,
            rare = 0.015f,
            epic = 0.005f,
            legendary = 0.001f,
            set = 0.0085f,
            radiant = 0.0001f,
        };

        [Tooltip("Slightly higher odds for normal enemies.")]
        public RarityChances normalChances = new RarityChances
        {
            common = 0.06f,
            uncommon = 0.035f,
            magic = 0.025f,
            rare = 0.02f,
            epic = 0.0075f,
            legendary = 0.0015f,
            set = 0.01f,
            radiant = 0.00012f,
        };

        [Tooltip("Higher odds for elite enemies.")]
        public RarityChances eliteChances = new RarityChances
        {
            common = 0.07f,
            uncommon = 0.04f,
            magic = 0.03f,
            rare = 0.025f,
            epic = 0.01f,
            legendary = 0.0025f,
            set = 0.0125f,
            radiant = 0.0002f,
        };

        [Tooltip("Mini-boss odds (not the main boss).")]
        public RarityChances miniBossChances = new RarityChances
        {
            common = 0.08f,
            uncommon = 0.045f,
            magic = 0.035f,
            rare = 0.03f,
            epic = 0.015f,
            legendary = 0.004f,
            set = 0.015f,
            radiant = 0.0004f,
        };

        [Header("Roll Counts")]
        [FormerlySerializedAs("zoneRollsFodder")] [Min(0)] public int zoneRollsTrash = 1;
        [Min(0)] public int zoneRollsNormal = 1;
        [Min(0)] public int zoneRollsElite = 1;
        [Min(0)] public int zoneRollsMiniBoss = 2;

        [Header("Materials Rolls (optional)")]
        public bool rollMaterials = true;
        [FormerlySerializedAs("materialsRollsFodder")] [Min(0)] public int materialsRollsTrash = 0;
        [Min(0)] public int materialsRollsNormal = 0;
        [Min(0)] public int materialsRollsElite = 1;
        [Min(0)] public int materialsRollsMiniBoss = 2;

        [Header("Boss Options (optional)")]
        [Tooltip("If enabled, bosses can roll uniques and enforce guaranteed minimum gear rarity.")]
        public bool enableBossOverrides = true;

        [Tooltip("When a boss dies, additional zone-wide rolls.")]
        [Min(0)] public int bossZoneRolls = 3;

        [Tooltip("When a boss dies, additional materials rolls.")]
        [Min(0)] public int bossMaterialsRolls = 3;

        [Tooltip("When a boss dies, number of rolls against bossUniquesPool.")]
        [Min(0)] public int bossUniqueRolls = 1;

        [Tooltip("Bosses always drop at least this rarity or higher (zonePool only).")]
        public ItemRarity bossGuaranteedMinRarity = ItemRarity.Rare;

        [Tooltip("How many attempts to satisfy the guaranteed rarity rule before giving up.")]
        [Min(1)] public int bossGuaranteedAttempts = 12;

        public RarityChances GetChancesForTier(EnemyTier tier)
        {
            return tier switch
            {
                EnemyTier.Trash => trashChances,
                EnemyTier.Normal => normalChances,
                EnemyTier.Elite => eliteChances,
                EnemyTier.MiniBoss => miniBossChances,
                _ => normalChances
            };
        }

        public int GetZoneRollsForTier(EnemyTier tier)
        {
            return tier switch
            {
                EnemyTier.Trash => zoneRollsTrash,
                EnemyTier.Normal => zoneRollsNormal,
                EnemyTier.Elite => zoneRollsElite,
                EnemyTier.MiniBoss => zoneRollsMiniBoss,
                _ => zoneRollsNormal
            };
        }

        public int GetMaterialsRollsForTier(EnemyTier tier)
        {
            if (!rollMaterials) return 0;

            return tier switch
            {
                EnemyTier.Trash => materialsRollsTrash,
                EnemyTier.Normal => materialsRollsNormal,
                EnemyTier.Elite => materialsRollsElite,
                EnemyTier.MiniBoss => materialsRollsMiniBoss,
                _ => materialsRollsNormal
            };
        }
    }
}
