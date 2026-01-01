using Abyssbound.Loot;
using Abyssbound.Loot.SetDrops;
using Abyssbound.BagUpgrades;
using Abyssbound.Threat;
using Abyssbound.Combat.Tiering;
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LootDropOnDeath : MonoBehaviour
{
    private const string DefaultLootTableResourcesPath = "Loot/Tables/Zone1_Trash";
    private const string TierLootConfigResourcesPath = "Loot/TierLootConfig";

    [Header("Loot")]
    public LootTableSO lootTable;
    [Min(0)] public int itemLevel = 0;
    public int? seed;

    [Header("Tier Content (Optional)")]
    public TierLootConfigSO tierLootConfig;

    [Header("Elite / Boss Bonus Rolls")]
    [SerializeField] private int eliteBonusRolls = 1;
    [SerializeField] private int bossBonusRolls = 2;

    [Header("Pickup")]
    public WorldItemPickup pickupPrefab;
    [Min(0f)] public float scatterRadius = 0.35f;

    [Header("Debug")]
    public bool logDrop;
    public bool logLootTierRolls;
    public bool logTierContentDrops;

    [SerializeField] private bool logEliteBonusRolls;

    private static bool s_WarnedMissingDefaultTable;

    private static bool s_TriedLoadTierLootConfig;
    private static TierLootConfigSO s_CachedTierLootConfig;
    private static bool s_WarnedMissingTierLootConfig;

    private struct ThreatLootContext
    {
        public GameObject enemyGO;
        public string label;
        public int lootTier;
        public int tierBucketRolls;
        public int eliteBonusRolls;
        public int bossBonusRolls;
        public bool computed;
    }

    private static ThreatLootContext s_ThreatLootContext;

    private EnemyHealth _health;

    private void OnEnable()
    {
        EnsureTierLootConfigAssigned();

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
        List<ItemInstance> extra = null;

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
                    lvl = Mathf.Max(1, rng != null ? rng.Next(r.ClampMin(), r.ClampMax() + 1) : UnityEngine.Random.Range(r.ClampMin(), r.ClampMax() + 1));
                }
                catch { }
            }

            var weights = tuning.GetRarityWeights(tier);

            // Threat -> loot scaling hook (single point): adjust rarity weights before rolling.
            s_ThreatLootContext = new ThreatLootContext
            {
                enemyGO = dead != null ? dead.gameObject : null,
                label = "Trash",
                lootTier = 1,
                tierBucketRolls = 1,
                eliteBonusRolls = eliteBonusRolls,
                bossBonusRolls = bossBonusRolls,
                computed = false,
            };
            ApplyThreatLootScaling(ref weights, out int bonusRolls, out float bonusRollChance);

            int lootTier = 1;
            try
            {
                var enemyGO = dead != null ? dead.gameObject : null;
                if (enemyGO != null)
                {
                    var ctx = enemyGO.GetComponent<EnemyLootContext>();
                    if (ctx != null)
                        lootTier = ctx.LootTier;
                }
            }
            catch { lootTier = 1; }

            ApplyLootTierScaling(ref weights, lootTier);

            var inst2 = LootRollerV2.RollItem(table, lvl, seed, lvlSource, logCreate, rarityWeightsOverride: weights);
            if (inst2 == null) return;
            inst = inst2;

            if (logLootTierRolls)
            {
                Debug.Log($"[LootTier] enemy='{(dead != null ? dead.name : name)}' lootTier={lootTier} weights=(C:{weights.common:0.###} U:{weights.uncommon:0.###} R:{weights.rare:0.###} E:{weights.epic:0.###}) chosen={inst.rarityId}");
            }

            // Optional extra rolls (additive, same table) based on threat.
            // These are treated like additional "extra" drops later, so they don't interfere with set-drop injection.
            if (bonusRolls > 0 && bonusRollChance > 0f)
            {
                if (extra == null) extra = new List<ItemInstance>(bonusRolls);
                for (int i = 0; i < bonusRolls; i++)
                {
                    bool doRoll;
                    try { doRoll = UnityEngine.Random.value <= Mathf.Clamp01(bonusRollChance); }
                    catch { doRoll = false; }
                    if (!doRoll) continue;

                        int? derivedSeed = null;
                        if (seed.HasValue)
                        {
                            // 0x9e3779b9 doesn't fit in signed int; do an unchecked cast for deterministic mixing.
                            uint mix = unchecked(0x9e3779b9u + (uint)i);
                            derivedSeed = unchecked(seed.Value ^ (int)mix);
                        }

                        var rolled = LootRollerV2.RollItem(table, lvl, derivedSeed, lvlSource, logCreation: false, rarityWeightsOverride: weights);
                    if (rolled != null)
                    {
                        extra.Add(rolled);

                        if (logLootTierRolls)
                        {
                            Debug.Log($"[LootTier] enemy='{(dead != null ? dead.name : name)}' lootTier={lootTier} weights=(C:{weights.common:0.###} U:{weights.uncommon:0.###} R:{weights.rare:0.###} E:{weights.epic:0.###}) chosen={rolled.rarityId} (extra)");
                        }
                    }
                }
            }
        }
        else
        {
            inst = LootRollerV2.RollItem(table, lvl, seed, lvlSource, logCreate);
        }
        if (inst == null) return;

        // Tier-based loot content (additive): one bucket roll after the main roll + Elite/Boss extra rolls.
        var enemyGO2 = dead != null ? dead.gameObject : gameObject;

        int tierContentLootTier = 1;
        string label2 = "Trash";
        int totalTierBucketRolls = 1;

        // Prefer the context computed inside ApplyThreatLootScaling (confirmed live path).
        if (s_ThreatLootContext.computed && s_ThreatLootContext.enemyGO == enemyGO2)
        {
            tierContentLootTier = Mathf.Max(1, s_ThreatLootContext.lootTier);
            label2 = string.IsNullOrWhiteSpace(s_ThreatLootContext.label) ? "Trash" : s_ThreatLootContext.label;
            totalTierBucketRolls = Mathf.Max(1, s_ThreatLootContext.tierBucketRolls);
        }
        else
        {
            try
            {
                var ctx = enemyGO2 != null ? enemyGO2.GetComponent<EnemyLootContext>() : null;
                if (ctx != null) tierContentLootTier = ctx.LootTier;
            }
            catch { tierContentLootTier = 1; }

            try
            {
                var profile = enemyGO2 != null ? enemyGO2.GetComponent<EnemyCombatProfile>() : null;
                label2 = profile != null ? profile.tier : "Trash";
            }
            catch { label2 = "Trash"; }

            int extraRolls2 = 0;
            if (string.Equals(label2, "Elite", StringComparison.OrdinalIgnoreCase)) extraRolls2 = 1;
            else if (string.Equals(label2, "Boss", StringComparison.OrdinalIgnoreCase)) extraRolls2 = 2;
            totalTierBucketRolls = 1 + Mathf.Max(0, extraRolls2);
        }

        if (tierLootConfig != null)
        {
            for (int i = 0; i < totalTierBucketRolls; i++)
            {
                int? derivedSeed = null;
                if (seed.HasValue)
                {
                    uint mix = unchecked(0x85ebca6bu + (uint)i * 0x27d4eb2du);
                    derivedSeed = unchecked(seed.Value ^ (int)mix);
                }

                TryAddTierContentDrop(dead, tierContentLootTier, label2, i, lvl, derivedSeed, ref extra);
            }
        }

        // Optional Zone1 set-drop injection (Abyssal Initiate) — data-driven via SetDropConfigSO.
        // Adds extra rolled instances; does not replace the main roll.
        try
        {
            var setExtra = SetDropRuntime.TryRollExtraSetDrops(
                table,
                itemLevel: lvl,
                seed: seed,
                rarityIdFallback: inst.rarityId,
                logPity: logDrop);

            if (setExtra != null && setExtra.Count > 0)
            {
                if (extra == null) extra = setExtra;
                else extra.AddRange(setExtra);
            }
        }
        catch { }

        // Bag upgrades (stackable consumables): additive extra drop based on current threat tier.
        string bagUpgradeBaseId = null;
        try
        {
            float threat = 0f;
            try { threat = ThreatService.Instance != null ? ThreatService.Instance.CurrentThreat : 0f; } catch { threat = 0f; }
            bagUpgradeBaseId = BagUpgradeDropRuntime.TryRollMonsterDropBaseId(threat);
        }
        catch { bagUpgradeBaseId = null; }

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

            if (!string.IsNullOrWhiteSpace(bagUpgradeBaseId))
            {
                try { inv.Add(bagUpgradeBaseId, 1); } catch { }
                if (logDrop)
                    Debug.Log($"[Drop] {bagUpgradeBaseId} x1 (bag upgrade)", this);
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
            var o = UnityEngine.Random.insideUnitCircle * scatterRadius;
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
                    var o2 = UnityEngine.Random.insideUnitCircle * scatterRadius;
                    p += new Vector3(o2.x, 0f, o2.y);
                }

                var p2 = Instantiate(pickupPrefab, p, Quaternion.identity);
                if (p2 != null)
                    p2.Initialize(e);
            }
        }

        if (!string.IsNullOrWhiteSpace(bagUpgradeBaseId))
        {
            Vector3 p = transform.position;
            if (scatterRadius > 0f)
            {
                var o2 = UnityEngine.Random.insideUnitCircle * scatterRadius;
                p += new Vector3(o2.x, 0f, o2.y);
            }

            var p2 = Instantiate(pickupPrefab, p, Quaternion.identity);
            if (p2 != null)
            {
                p2.Initialize(new ItemInstance
                {
                    baseItemId = bagUpgradeBaseId,
                    rarityId = "Common",
                    itemLevel = 1,
                    baseScalar = 1f,
                    affixes = new()
                });

                if (logDrop)
                    Debug.Log($"[Drop] {bagUpgradeBaseId} x1 (bag upgrade)", this);
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

    private static void ApplyThreatLootScaling(ref ZoneLootTuningSO.TierRarityWeights weights, out int bonusRolls, out float bonusRollChance)
    {
        bonusRolls = 0;
        bonusRollChance = 0f;

        float threat = 0f;
        float dist = 0f;
        try
        {
            if (ThreatService.Instance != null)
            {
                threat = ThreatService.Instance.CurrentThreat;
                dist = ThreatService.Instance.CurrentDistanceMeters;
            }
        }
        catch { threat = 0f; dist = 0f; }

        var cfg = ThreatLootScalingConfigSO.LoadOrNull();
        if (cfg == null)
            return;

        if (!cfg.TryGetTier(threat, out var tier))
            return;

        weights.common = Mathf.Max(0f, weights.common * tier.commonMultiplier);
        weights.uncommon = Mathf.Max(0f, weights.uncommon * tier.uncommonMultiplier);
        weights.magic = Mathf.Max(0f, weights.magic * tier.magicMultiplier);
        weights.rare = Mathf.Max(0f, weights.rare * tier.rareMultiplier);
        weights.epic = Mathf.Max(0f, weights.epic * tier.epicMultiplier);
        weights.legendary = Mathf.Max(0f, weights.legendary * tier.legendaryMultiplier);

        bonusRolls = Mathf.Max(0, tier.bonusRolls);
        bonusRollChance = Mathf.Clamp01(tier.bonusRollChance);

        // Compute tier-bucket context here (confirmed live log path). Actual spawning occurs later in the death pipeline.
        try
        {
            var enemyGO = s_ThreatLootContext.enemyGO;
            int lootTier = 1;
            var ctx = enemyGO != null ? enemyGO.GetComponent<EnemyLootContext>() : null;
            if (ctx != null) lootTier = ctx.LootTier;

            string label = "Trash";
            var profile = enemyGO != null ? enemyGO.GetComponent<EnemyCombatProfile>() : null;
            if (profile != null && !string.IsNullOrEmpty(profile.tier)) label = profile.tier;

            int extraRolls = 0;
            if (string.Equals(label, "Elite", StringComparison.OrdinalIgnoreCase)) extraRolls = s_ThreatLootContext.eliteBonusRolls;
            else if (string.Equals(label, "Boss", StringComparison.OrdinalIgnoreCase)) extraRolls = s_ThreatLootContext.bossBonusRolls;

            int totalRolls = 1 + extraRolls;

            s_ThreatLootContext.label = label;
            s_ThreatLootContext.lootTier = Mathf.Max(1, lootTier);
            s_ThreatLootContext.tierBucketRolls = totalRolls;
            s_ThreatLootContext.computed = true;
        }
        catch { }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        try
        {
            if (LootQaSettings.DebugLogsEnabled)
            {
                Debug.Log("[Loot][FINGERPRINT] FILE=LootDropOnDeath.cs METHOD=ApplyThreatLootScaling PATH=Assets/Scripts/LootSystem/LootDropOnDeath.cs");
                Debug.Log($"[Loot] Threat={threat:0.0} dist={dist:0}m scaledWeights=(C:{weights.common:0.###} U:{weights.uncommon:0.###} M:{weights.magic:0.###} R:{weights.rare:0.###} E:{weights.epic:0.###} L:{weights.legendary:0.###}) bonusRolls={bonusRolls} chance={bonusRollChance:0.##}");
            }
        }
        catch { }
#endif
    }

    private static void ApplyLootTierScaling(ref ZoneLootTuningSO.TierRarityWeights weights, int lootTier)
    {
        float commonMult;
        float uncommonMult;
        float rareMult;
        float epicMult;

        switch (lootTier)
        {
            default:
            case 1:
                commonMult = 1.00f;
                uncommonMult = 1.00f;
                rareMult = 1.00f;
                epicMult = 1.00f;
                break;
            case 2:
                commonMult = 0.95f;
                uncommonMult = 1.10f;
                rareMult = 1.20f;
                epicMult = 1.05f;
                break;
            case 3:
                commonMult = 0.90f;
                uncommonMult = 1.20f;
                rareMult = 1.45f;
                epicMult = 1.15f;
                break;
            case 4:
                commonMult = 0.80f;
                uncommonMult = 1.30f;
                rareMult = 1.75f;
                epicMult = 1.30f;
                break;
            case 5:
                commonMult = 0.65f;
                uncommonMult = 1.45f;
                rareMult = 2.20f;
                epicMult = 1.55f;
                break;
        }

        weights.common = Mathf.Max(0f, weights.common * commonMult);
        weights.uncommon = Mathf.Max(0f, weights.uncommon * uncommonMult);
        weights.rare = Mathf.Max(0f, weights.rare * rareMult);
        weights.epic = Mathf.Max(0f, weights.epic * epicMult);
    }

    private void TryAddTierContentDrop(EnemyHealth dead, int lootTier, string label, int rollIndex, int itemLevel, int? seed, ref List<ItemInstance> extra)
    {
        if (tierLootConfig == null)
            return;

        var bucket = tierLootConfig.GetBucket(lootTier);
        if (bucket == null || bucket.entries == null || bucket.entries.Length == 0)
            return;

        int totalWeight = 0;
        for (int i = 0; i < bucket.entries.Length; i++)
            totalWeight += Mathf.Max(0, bucket.entries[i].weight);
        if (totalWeight <= 0)
            return;

        System.Random rng = null;
        if (seed.HasValue)
            rng = new System.Random(unchecked(seed.Value ^ 0x6d2b79f5 ^ (lootTier * 1013) ^ (bucket.tier * 7919)));

        int roll = rng != null ? rng.Next(0, totalWeight) : UnityEngine.Random.Range(0, totalWeight);

        TierLootBucketSO.WeightedEntry chosen = default;
        bool found = false;
        int acc = 0;
        for (int i = 0; i < bucket.entries.Length; i++)
        {
            int w = Mathf.Max(0, bucket.entries[i].weight);
            if (w <= 0) continue;
            acc += w;
            if (roll < acc)
            {
                chosen = bucket.entries[i];
                found = true;
                break;
            }
        }

        var itemDef = chosen.itemRef as ItemDefinitionSO;
        if (!found || itemDef == null || string.IsNullOrWhiteSpace(itemDef.id))
            return;

        int minQty = Mathf.Max(1, chosen.minQty);
        int maxQty = Mathf.Max(minQty, chosen.maxQty);
        int qty = rng != null ? rng.Next(minQty, maxQty + 1) : UnityEngine.Random.Range(minQty, maxQty + 1);

        if (extra == null) extra = new List<ItemInstance>(qty);
        for (int i = 0; i < qty; i++)
        {
            extra.Add(new ItemInstance
            {
                baseItemId = itemDef.id,
                rarityId = "Common",
                itemLevel = Mathf.Max(1, itemLevel),
                baseScalar = 1f,
                affixes = new()
            });
        }

        if (logTierContentDrops)
        {
            var enemyName = dead != null ? dead.name : name;
            var itemName = string.IsNullOrWhiteSpace(itemDef.displayName) ? itemDef.id : itemDef.displayName;
            var safeLabel = string.IsNullOrWhiteSpace(label) ? "Trash" : label;
            Debug.Log($"[Loot][TierBucket] enemy={enemyName} label={safeLabel} tier={lootTier} rollIndex={rollIndex} item={itemName} qty={qty}", this);
        }
    }

    private void EnsureTierLootConfigAssigned()
    {
        if (tierLootConfig != null)
            return;

        var cfg = TryLoadTierLootConfig();
        if (cfg != null)
            tierLootConfig = cfg;
    }

    private static TierLootConfigSO TryLoadTierLootConfig()
    {
        if (s_CachedTierLootConfig != null)
            return s_CachedTierLootConfig;

        if (s_TriedLoadTierLootConfig)
            return null;

        s_TriedLoadTierLootConfig = true;
        try { s_CachedTierLootConfig = Resources.Load<TierLootConfigSO>(TierLootConfigResourcesPath); }
        catch { s_CachedTierLootConfig = null; }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (s_CachedTierLootConfig == null && !s_WarnedMissingTierLootConfig)
        {
            s_WarnedMissingTierLootConfig = true;
            Debug.LogWarning($"[LootTier] TierLootConfigSO not found at Resources/{TierLootConfigResourcesPath}.asset. Run Abyssbound/Loot/Setup Tier Loot (Create + Wire) to generate it.");
        }
#endif
        return s_CachedTierLootConfig;
    }
}
