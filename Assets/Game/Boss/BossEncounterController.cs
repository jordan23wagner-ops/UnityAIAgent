using UnityEngine;

public class BossEncounterController : MonoBehaviour
{
    [Header("Boss")]
    public GameObject bossPrefab;
    public Transform spawnPoint;

    [Header("State")]
    public bool spawnOncePerSigil = true;
    [SerializeField] private bool hasSpawnedThisSigil;

    [Header("Spawn Mode")]
    public bool activateExistingBossInsteadOfInstantiate = false;
    public GameObject existingBossRoot;

    private GameObject _spawnedBossInstance;

    public void TriggerBoss()
    {
        if (spawnOncePerSigil && hasSpawnedThisSigil)
        {
            Debug.Log("[BossEncounter] TriggerBoss ignored (already spawned this sigil).", this);
            return;
        }

        if (spawnPoint == null)
        {
            Debug.LogError("[BossEncounter] Missing spawnPoint reference.", this);
            return;
        }

        GameObject bossRoot;

        if (activateExistingBossInsteadOfInstantiate)
        {
            bossRoot = existingBossRoot;
            if (bossRoot == null)
            {
                Debug.LogError("[BossEncounter] activateExistingBossInsteadOfInstantiate is true but existingBossRoot is null.", this);
                return;
            }

            bossRoot.SetActive(true);
            _spawnedBossInstance = bossRoot;
            Debug.Log($"[BossEncounter] Boss activated: {bossRoot.name}", this);
        }
        else
        {
            if (bossPrefab == null)
            {
                Debug.LogError("[BossEncounter] Missing bossPrefab reference.", this);
                return;
            }

            bossRoot = Instantiate(bossPrefab, spawnPoint.position, spawnPoint.rotation);
            bossRoot.name = bossPrefab.name;
            _spawnedBossInstance = bossRoot;
            Debug.Log($"[BossEncounter] Boss spawned: {bossRoot.name} @ {spawnPoint.position}", this);
        }

        hasSpawnedThisSigil = true;

        ValidateBossLoot(bossRoot);
    }

    public void ResetForNewSigil()
    {
        hasSpawnedThisSigil = false;
        Debug.Log("[BossEncounter] ResetForNewSigil: hasSpawnedThisSigil=false", this);
    }

    private static void ValidateBossLoot(GameObject bossRoot)
    {
        if (bossRoot == null)
        {
            Debug.LogError("[BossEncounter] ValidateBossLoot: bossRoot is null.");
            return;
        }

        var dropOnDeath = bossRoot.GetComponentInChildren<DropOnDeath>(true);
        if (dropOnDeath == null)
        {
            Debug.LogError($"[BossEncounter] Boss '{bossRoot.name}' is missing DropOnDeath. Boss drops will not work.", bossRoot);
            return;
        }

        if (dropOnDeath.dropTable == null)
        {
            Debug.LogError($"[BossEncounter] Boss '{bossRoot.name}' DropOnDeath.dropTable is null.", bossRoot);
            return;
        }

        var table = dropOnDeath.dropTable;

        // Guaranteed rule check
        if (table.guaranteedEquipmentDrop == null || !table.guaranteedEquipmentDrop.enabled)
        {
            Debug.LogError($"[BossEncounter] Boss DropTable '{table.name}' must have guaranteedEquipmentDrop.enabled = true to guarantee Rare+ equipment.", table);
        }
        else
        {
            if (table.guaranteedEquipmentDrop.category != ItemType.Equipment)
                Debug.LogError($"[BossEncounter] DropTable '{table.name}' guaranteedEquipmentDrop.category must be Equipment.", table);

            if (table.guaranteedEquipmentDrop.minRarity < ItemRarity.Rare)
                Debug.LogError($"[BossEncounter] DropTable '{table.name}' guaranteedEquipmentDrop.minRarity must be Rare or higher.", table);

            if (table.guaranteedEquipmentDrop.rolls <= 0)
                Debug.LogError($"[BossEncounter] DropTable '{table.name}' guaranteedEquipmentDrop.rolls must be >= 1.", table);

            if (!HasEligiblePool(table, dropOnDeath.tier, ItemType.Equipment, ItemRarity.Rare))
                Debug.LogError($"[BossEncounter] DropTable '{table.name}' has guaranteedEquipmentDrop enabled but has no eligible Rare+ Equipment entries for tier {dropOnDeath.tier}.", table);
        }

        // Mats presence check
        if (table.matsTable == null && !HasAnyOfType(table, ItemType.Material))
            Debug.LogWarning($"[BossEncounter] Boss DropTable '{table.name}' has no matsTable and no Material items in any tier. Zone mats may be missing.", table);

        // Special drops presence check
        if (table.specialDropsTable == null && !HasAnyRarityAtLeast(table, ItemRarity.Legendary))
            Debug.LogWarning($"[BossEncounter] Boss DropTable '{table.name}' has no specialDropsTable and no Legendary+/Set/Radiant items in any tier. Special drops may be missing.", table);
    }

    private static bool HasEligiblePool(DropTable table, EnemyTier tier, ItemType type, ItemRarity minRarity)
    {
        if (table == null) return false;
        var list = table.GetDropsForTier(tier);
        if (list == null) return false;

        foreach (var entry in list)
        {
            var item = entry != null ? entry.item : null;
            if (item == null) continue;
            if (item.itemType != type) continue;
            if (item.rarity < minRarity) continue;
            return true;
        }

        return false;
    }

    private static bool HasAnyOfType(DropTable table, ItemType type)
    {
        if (table == null) return false;
        return HasAnyInList(table.fodderDrops, type) || HasAnyInList(table.normalDrops, type) || HasAnyInList(table.eliteDrops, type) || HasAnyInList(table.miniBossDrops, type);
    }

    private static bool HasAnyInList(System.Collections.Generic.List<DropEntry> list, ItemType type)
    {
        if (list == null) return false;
        foreach (var entry in list)
        {
            var item = entry != null ? entry.item : null;
            if (item == null) continue;
            if (item.itemType == type) return true;
        }
        return false;
    }

    private static bool HasAnyRarityAtLeast(DropTable table, ItemRarity minRarity)
    {
        if (table == null) return false;
        return HasAnyRarityInList(table.fodderDrops, minRarity) || HasAnyRarityInList(table.normalDrops, minRarity) || HasAnyRarityInList(table.eliteDrops, minRarity) || HasAnyRarityInList(table.miniBossDrops, minRarity);
    }

    private static bool HasAnyRarityInList(System.Collections.Generic.List<DropEntry> list, ItemRarity minRarity)
    {
        if (list == null) return false;
        foreach (var entry in list)
        {
            var item = entry != null ? entry.item : null;
            if (item == null) continue;
            if (item.rarity >= minRarity) return true;
        }
        return false;
    }
}
