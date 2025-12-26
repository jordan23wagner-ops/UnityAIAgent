using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abyssbound.Loot.SetDrops
{
    [CreateAssetMenu(menuName = "Abyssbound/Loot/Set Drops/Set Drop Config", fileName = "SetDropConfig_")]
    public sealed class SetDropConfigSO : ScriptableObject
    {
        [Serializable]
        public struct SetPieceRef
        {
            public ItemDefinitionSO piece;
        }

        public string setId;
        public List<SetPieceRef> pieces = new();

        [Header("Per-tier Set Roll Chance (Percent)")]
        [Min(0f)] public float trashSetRollChance;
        [Min(0f)] public float eliteSetRollChance;
        [Min(0f)] public float bossSetRollChance;

        [Header("Per-tier Pieces Rolled On Hit")]
        [Min(1)] public int trashPiecesToRoll = 1;
        [Min(1)] public int elitePiecesToRoll = 1;
        [Min(1)] public int bossPiecesToRoll = 1;

        [Header("Boss Pity (Optional)")]
        public bool bossPityEnabled;
        [Min(1)] public int bossPityThresholdKills = 10;
        public bool bossPityGuaranteeOnePiece = true;

        public bool HasPieces
        {
            get
            {
                if (pieces == null || pieces.Count == 0) return false;
                for (int i = 0; i < pieces.Count; i++)
                    if (pieces[i].piece != null) return true;
                return false;
            }
        }

        public float GetRollChancePercent(LootTier tier)
        {
            return tier switch
            {
                LootTier.Elite => eliteSetRollChance,
                LootTier.Boss => bossSetRollChance,
                _ => trashSetRollChance,
            };
        }

        public int GetPiecesToRollOnHit(LootTier tier)
        {
            int v = tier switch
            {
                LootTier.Elite => elitePiecesToRoll,
                LootTier.Boss => bossPiecesToRoll,
                _ => trashPiecesToRoll,
            };
            return Mathf.Max(1, v);
        }

        public List<ItemDefinitionSO> GetValidPieces()
        {
            var list = new List<ItemDefinitionSO>(pieces != null ? pieces.Count : 0);
            if (pieces == null) return list;
            for (int i = 0; i < pieces.Count; i++)
            {
                var p = pieces[i].piece;
                if (p != null) list.Add(p);
            }
            return list;
        }
    }

    public enum LootTier
    {
        Trash = 0,
        Elite = 1,
        Boss = 2,
    }
}
