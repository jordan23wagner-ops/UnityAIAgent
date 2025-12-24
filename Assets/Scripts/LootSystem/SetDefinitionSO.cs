using System.Collections.Generic;
using UnityEngine;

namespace Abyssbound.Loot
{
    [CreateAssetMenu(menuName = "Abyssbound/Loot/Set Definition", fileName = "Set_")]
    public sealed class SetDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public string id;
        public string displayName;

        [Header("Pieces (optional)")]
        public List<ItemDefinitionSO> pieces = new();

        [Tooltip("Optional override. If 0, tooltip will infer from Pieces (or show '?' if unknown).")]
        public int maxPieces = 0;

        public int GetMaxPieces()
        {
            if (maxPieces > 0) return maxPieces;
            return pieces != null ? pieces.Count : 0;
        }

        public override string ToString()
        {
            if (!string.IsNullOrWhiteSpace(displayName)) return displayName;
            if (!string.IsNullOrWhiteSpace(id)) return id;
            return name;
        }
    }
}
