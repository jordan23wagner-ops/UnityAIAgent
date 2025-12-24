using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abyssbound.Loot
{
    [CreateAssetMenu(menuName = "Abyssbound/Loot/Item Set Definition", fileName = "ItemSet_")]
    public sealed class ItemSetDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public string setId;
        public string displayName;

        [Header("Pieces")]
        public List<ItemDefinitionSO> pieces = new();

        [Header("Bonuses (future use)")]
        public List<SetBonusTier> bonuses = new();

        public int GetTotalPieces()
        {
            return pieces != null ? pieces.Count : 0;
        }

        [Serializable]
        public sealed class SetBonusTier
        {
            public int requiredPieces;
            public string description;

            [Header("Stat Mods")]
            public List<StatMod> modifiers = new();
        }

        public override string ToString()
        {
            if (!string.IsNullOrWhiteSpace(displayName)) return displayName;
            if (!string.IsNullOrWhiteSpace(setId)) return setId;
            return name;
        }
    }
}
