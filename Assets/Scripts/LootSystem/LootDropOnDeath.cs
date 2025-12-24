using Abyssbound.Loot;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LootDropOnDeath : MonoBehaviour
{
    private const string DefaultLootTableResourcesPath = "Loot/Tables/Zone1_Trash";

    [Header("Loot")]
    public LootTableSO lootTable;
    [Min(0)] public int itemLevel = 0;
    public int? seed;

    [Header("Pickup")]
    public WorldItemPickup pickupPrefab;
    [Min(0f)] public float scatterRadius = 0.35f;

    [Header("Debug")]
    public bool logDrop;

    private static bool s_WarnedMissingDefaultTable;

    private EnemyHealth _health;

    private void OnEnable()
    {
        _health = GetComponentInParent<EnemyHealth>();
        if (_health != null)
        {
            _health.OnDeath -= OnEnemyDeath;
            _health.OnDeath += OnEnemyDeath;
        }
    }

    private void OnDisable()
    {
        if (_health != null)
            _health.OnDeath -= OnEnemyDeath;
    }

    private void OnEnemyDeath(EnemyHealth dead)
    {
        // If the legacy DropOnDeath component is present, it is authoritative for loot.
        // Avoid double-dropping when both systems are on the same enemy.
        try
        {
            var legacy = GetComponentInParent<DropOnDeath>();
            if (legacy != null && legacy.enabled)
                return;
        }
        catch { }

        var table = lootTable != null ? lootTable : TryLoadDefaultTable();
        if (table == null) return;

        int lvl;
        string lvlSource;

        // QA override takes precedence for ALL newly created drop instances.
        if (!LootQaSettings.TryGetItemLevelOverride(out lvl, out lvlSource))
        {
            if (itemLevel > 0)
            {
                lvl = itemLevel;
                lvlSource = "Zone";
            }
            else
            {
                lvl = 1;
                lvlSource = "Default";
            }
        }

        bool logCreate = LootQaSettings.DebugLogsEnabled;
        var inst = LootRollerV2.RollItem(table, lvl, seed, lvlSource, logCreate);
        if (inst == null) return;

        // If no pickup prefab is assigned, grant straight into the inventory.
        // This keeps QA unblocked and avoids forcing scene/prefab authoring.
        if (pickupPrefab == null)
        {
            var inv = Game.Systems.PlayerInventoryResolver.GetOrFind();
            if (inv == null) return;

            var reg = LootRegistryRuntime.GetOrCreate();
            var rolledId = reg.RegisterRolledInstance(inst);
            if (string.IsNullOrWhiteSpace(rolledId)) return;

            inv.Add(rolledId, 1);

            if (logDrop)
            {
                string itemName = inst.baseItemId;
                try
                {
                    if (reg != null && reg.TryGetItem(inst.baseItemId, out var baseItem) && baseItem != null)
                        itemName = string.IsNullOrWhiteSpace(baseItem.displayName) ? baseItem.id : baseItem.displayName;
                }
                catch { }

                string rarityId = string.IsNullOrWhiteSpace(inst.rarityId) ? "(none)" : inst.rarityId;
                Debug.Log($"[Drop] {itemName} rarity={rarityId} ilvl={Mathf.Max(1, inst.itemLevel)} stackable=false merged=false equipable=true", this);
            }

            return;
        }

        Vector3 pos = transform.position;
        if (scatterRadius > 0f)
        {
            var o = Random.insideUnitCircle * scatterRadius;
            pos += new Vector3(o.x, 0f, o.y);
        }

        var pickup = Instantiate(pickupPrefab, pos, Quaternion.identity);
        if (pickup != null)
        {
            pickup.Initialize(inst);

            if (logDrop)
            {
                string itemName = inst.baseItemId;
                try
                {
                    var reg = LootRegistryRuntime.GetOrCreate();
                    if (reg != null && reg.TryGetItem(inst.baseItemId, out var baseItem) && baseItem != null)
                        itemName = string.IsNullOrWhiteSpace(baseItem.displayName) ? baseItem.id : baseItem.displayName;
                }
                catch { }

                string rarityId = string.IsNullOrWhiteSpace(inst.rarityId) ? "(none)" : inst.rarityId;
                Debug.Log($"[Drop] {itemName} rarity={rarityId} ilvl={Mathf.Max(1, inst.itemLevel)} stackable=false merged=false equipable=true", this);
            }
        }
    }

    private static LootTableSO TryLoadDefaultTable()
    {
        LootTableSO table = null;
        try { table = Resources.Load<LootTableSO>(DefaultLootTableResourcesPath); } catch { table = null; }

        if (table == null && !s_WarnedMissingDefaultTable)
        {
            s_WarnedMissingDefaultTable = true;
            Debug.LogWarning($"[LootDropOnDeath] No lootTable assigned and default table missing at Resources/{DefaultLootTableResourcesPath}.asset. Run Tools/Abyssbound/Loot/Create Starter Loot Content.");
        }

        return table;
    }
}
