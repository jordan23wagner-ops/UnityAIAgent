using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Abyss.Legacy;

public enum EnemyTier
{
    [InspectorName("Trash Mob")] Trash,
    Normal,
    Elite,
    MiniBoss
}

[Serializable]
public class DropEntry
{
    public LegacyItemDefinition item;

    [Range(0f, 1f)]
    public float dropChance;
}

[CreateAssetMenu(menuName="Abyssbound/Loot/Drop Table", fileName="NewDropTable")]
public class DropTable : ScriptableObject
{
    [Header("Drops by Enemy Tier")]
    [FormerlySerializedAs("fodderDrops")] public List<DropEntry> trashDrops = new();
    public List<DropEntry> normalDrops = new();
    public List<DropEntry> eliteDrops = new();
    public List<DropEntry> miniBossDrops = new();

    [Header("Boss Extensions (optional)")]
    public GuaranteedDropRule guaranteedEquipmentDrop = new();
    public DropTable matsTable;
    public DropTable specialDropsTable;

    public List<DropEntry> GetDropsForTier(EnemyTier tier)
    {
        return tier switch
        {
            EnemyTier.Trash => trashDrops,
            EnemyTier.Normal => normalDrops,
            EnemyTier.Elite => eliteDrops,
            EnemyTier.MiniBoss => miniBossDrops,
            _ => null
        };
    }
}
