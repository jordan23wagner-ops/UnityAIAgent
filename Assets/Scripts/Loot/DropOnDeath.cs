using UnityEngine;

public class DropOnDeath : MonoBehaviour
{
    [Header("Loot")]
    public DropTable dropTable;
    public EnemyTier tier = EnemyTier.Normal;

    [Header("Destination")]
    public PlayerInventory playerInventory;

    [Header("Debug")]
    public bool logNoDrop = true;

    public void OnDeath()
    {
        TryRollAndGrantDrops();
    }

    [ContextMenu("TEST: Simulate Death (Roll Drops)")]
    private void TestSimulateDeath()
    {
        TryRollAndGrantDrops();
    }

    private void TryRollAndGrantDrops()
    {
        if (dropTable == null)
        {
            Debug.LogError($"[DropOnDeath] No DropTable assigned on '{name}'.");
            return;
        }

        if (playerInventory == null)
            playerInventory = FindFirstObjectByType<PlayerInventory>();

        if (playerInventory == null)
        {
            Debug.LogError("[DropOnDeath] No PlayerInventory found. Add PlayerInventory to Player_Hero or assign it on DropOnDeath.");
            return;
        }

        System.Action<string> logError = msg => Debug.LogError($"{msg} (source='{name}')");

        var drops = DropTableRoller.Roll(dropTable, tier, null, logError);

        if (dropTable.matsTable != null)
        {
            if (ReferenceEquals(dropTable.matsTable, dropTable))
                Debug.LogWarning($"[DropOnDeath] '{name}' matsTable references itself. Skipping.");
            else
                drops.AddRange(DropTableRoller.Roll(dropTable.matsTable, tier, null, logError));
        }

        if (dropTable.specialDropsTable != null)
        {
            if (ReferenceEquals(dropTable.specialDropsTable, dropTable))
                Debug.LogWarning($"[DropOnDeath] '{name}' specialDropsTable references itself. Skipping.");
            else
                drops.AddRange(DropTableRoller.Roll(dropTable.specialDropsTable, tier, null, logError));
        }

        if (drops == null || drops.Count == 0)
        {
            if (logNoDrop) Debug.Log($"[DropOnDeath] No drops for '{name}' (tier: {tier}).");
            return;
        }

        foreach (var item in drops)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.itemId)) continue;
            playerInventory.Add(item.itemId, 1);
        }

        Debug.Log($"[DropOnDeath] '{name}' dropped {drops.Count} item(s) (tier: {tier}).");
    }
}
