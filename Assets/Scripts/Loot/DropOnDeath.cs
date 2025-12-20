using UnityEngine;
using Game.Systems;
using Abyss.Loot;
using Abyss.Shop;

public class DropOnDeath : MonoBehaviour
{
    [Header("Loot")]
    public DropTable dropTable;
    [Tooltip("New zone-wide loot table (preferred). If set, this will be used instead of the legacy DropTable.")]
    public ZoneLootTable zoneLootTable;
    public EnemyTier tier = EnemyTier.Normal;

    [Header("Gold (optional)")]
    public bool grantGold = true;
    [Min(0)] public int goldMin = 0;
    [Min(0)] public int goldMax = 0;

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
        if (playerInventory == null)
            playerInventory = PlayerInventoryResolver.GetOrFind();

        if (playerInventory == null)
        {
            Debug.LogError("[DropOnDeath] No PlayerInventory found. Add PlayerInventory to Player_Hero or assign it on DropOnDeath.");
            return;
        }

        TryGrantGold();

        System.Action<string> logError = msg => Debug.LogError($"{msg} (source='{name}')");

        // Preferred: zone-wide loot table using Abyss.Items.ItemDefinition.
        if (zoneLootTable != null)
        {
            var zoneDrops = ZoneLootRoller.RollZone(zoneLootTable, tier, null, logError);

            // Heuristic: treat MiniBoss tier as boss-like for now. Dedicated bosses can still use tier=MiniBoss.
            if (tier == EnemyTier.MiniBoss)
                ZoneLootRoller.ApplyBossOverrides(zoneLootTable, zoneDrops, null, logError);

            GrantItems(zoneDrops);
            return;
        }

        // Legacy fallback.
        if (dropTable == null)
        {
            Debug.LogError($"[DropOnDeath] No ZoneLootTable or DropTable assigned on '{name}'.");
            return;
        }

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

    private void TryGrantGold()
    {
        if (!grantGold) return;
        if (goldMax <= 0) return;

        int min = Mathf.Max(0, goldMin);
        int max = Mathf.Max(min, goldMax);

        // Inclusive range for designers.
        int amount = Random.Range(min, max + 1);
        if (amount <= 0) return;

        var wallet = PlayerGoldWallet.Instance;
        if (wallet == null) return;
        wallet.AddGold(amount);
    }

    private void GrantItems(System.Collections.Generic.List<Abyss.Items.ItemDefinition> drops)
    {
        if (drops == null || drops.Count == 0)
        {
            if (logNoDrop) Debug.Log($"[DropOnDeath] No drops for '{name}' (tier: {tier}).");
            return;
        }

        int granted = 0;
        foreach (var def in drops)
        {
            if (def == null || string.IsNullOrWhiteSpace(def.itemId)) continue;
            playerInventory.Add(def.itemId, 1);
            granted++;
        }

        if (granted > 0)
            Debug.Log($"[DropOnDeath] '{name}' dropped {granted} item(s) (tier: {tier}).");
        else if (logNoDrop)
            Debug.Log($"[DropOnDeath] No valid drops for '{name}' (tier: {tier}).");
    }
}
