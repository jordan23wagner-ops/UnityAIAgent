using System;
using System.Collections.Generic;
using UnityEngine;

public enum EnemyTier
{
    Fodder,
    Normal,
    Elite,
    MiniBoss
}

[Serializable]
public class DropEntry
{
    public ItemDefinition item;

    [Range(0f, 1f)]
    public float dropChance;
}

[CreateAssetMenu(menuName="Abyssbound/Loot/Drop Table", fileName="NewDropTable")]
public class DropTable : ScriptableObject
{
    [Header("Drops by Enemy Tier")]
    public List<DropEntry> fodderDrops = new();
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
            EnemyTier.Fodder => fodderDrops,
            EnemyTier.Normal => normalDrops,
            EnemyTier.Elite => eliteDrops,
            EnemyTier.MiniBoss => miniBossDrops,
            _ => null
        };
    }
}
