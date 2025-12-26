using Abyssbound.Loot;
using Abyssbound.Loot.SetDrops;
using System.Collections.Generic;
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
        bool hasQaOverride = LootQaSettings.TryGetItemLevelOverride(out lvl, out lvlSource);
        if (!hasQaOverride)
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
        ItemInstance inst = null;

        // Zone1 tuning (rarity + itemLevel range) is applied when available.
        // QA override still controls itemLevel, but rarity weights can still come from tuning.
        var tuning = Zone1LootTuning.GetConfig();
        if (tuning != null && Zone1LootTuning.IsZone1Table(table))
        {
            var tier = Zone1LootTuning.ResolveTierFromTable(table);
            if (!hasQaOverride)
            {
                // Replace the zone-derived fixed itemLevel with the tuned per-tier range roll.
                lvlSource = "Zone1Tuning";
                try
                {
                    var r = tuning.GetItemLevelRange(tier);
                    var rng = seed.HasValue ? new System.Random(seed.Value ^ 0x5f3759df) : null;
                    lvl = Mathf.Max(1, rng != null ? rng.Next(r.ClampMin(), r.ClampMax() + 1) : Random.Range(r.ClampMin(), r.ClampMax() + 1));
                }
                catch { }
            }

            var inst2 = LootRollerV2.RollItem(table, lvl, seed, lvlSource, logCreate, rarityWeightsOverride: tuning.GetRarityWeights(tier));
            if (inst2 == null) return;
            inst = inst2;
        }
        else
        {
            inst = LootRollerV2.RollItem(table, lvl, seed, lvlSource, logCreate);
        }
        if (inst == null) return;

        // Optional Zone1 set-drop injection (Abyssal Initiate) — data-driven via SetDropConfigSO.
        // Adds extra rolled instances; does not replace the main roll.
        List<ItemInstance> extra = null;
        try
        {
            extra = SetDropRuntime.TryRollExtraSetDrops(
                table,
                itemLevel: lvl,
                seed: seed,
                rarityIdFallback: inst.rarityId,
                logPity: logDrop);
        }
        catch { extra = null; }

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

            if (extra != null && extra.Count > 0)
            {
                for (int i = 0; i < extra.Count; i++)
                {
                    var e = extra[i];
                    if (e == null) continue;
                    var eid = reg.RegisterRolledInstance(e);
                    if (string.IsNullOrWhiteSpace(eid)) continue;
                    inv.Add(eid, 1);
                }
            }

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

        if (extra != null && extra.Count > 0)
        {
            for (int i = 0; i < extra.Count; i++)
            {
                var e = extra[i];
                if (e == null) continue;

                Vector3 p = transform.position;
                if (scatterRadius > 0f)
                {
                    var o2 = Random.insideUnitCircle * scatterRadius;
                    p += new Vector3(o2.x, 0f, o2.y);
                }

                var p2 = Instantiate(pickupPrefab, p, Quaternion.identity);
                if (p2 != null)
                    p2.Initialize(e);
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
