# ABYSSBOUND SPRINT — PRE-QA RECON (Evidence-Based)

Date: 2025-12-24

Scope constraints (per request): **recon only**. No tuning changes, no asset edits, no new gameplay behavior.

This project currently contains **two loot pipelines** that can both exist on enemies:
- **Legacy/ZoneLootTable pipeline** (`Abyss.Loot.ZoneLootTable` + `DropOnDeath`)
- **Loot V2 pipeline** (`Abyssbound.Loot.LootTableSO` + `LootDropOnDeath` + `LootRollerV2`)

Many QA tools intentionally prefer **Loot V2** because it supports **item level + affix tier scaling**.

---

## 1) Where is the Zone1 loot table defined?

### A. “Zone1 loot table” (legacy ZoneLootTable system)

**Asset (zone-wide master table):**
- `Assets/GameData/Loot/Zone1/Zone1_LootTable.asset`

**ScriptableObject type:**
- `Abyss.Loot.ZoneLootTable` in `Assets/Abyss/Loot/ZoneLootTable.cs` (161 lines)

**Fields controlling rarity weights / caps:**
- Per-roll rarity chances by enemy tier:
  - `trashChances`, `normalChances`, `eliteChances`, `miniBossChances`
  - each is a `RarityChances` struct with: `common`, `uncommon`, `magic`, `rare`, `epic`, `legendary`, `set`, `radiant`
  - `RarityChances.Total` is used as a normalization sum.
- Boss “caps/guarantees”:
  - `enableBossOverrides`
  - `bossGuaranteedMinRarity`
  - `bossGuaranteedAttempts`

**Fields controlling itemLevel min/max:**
- **Not present** in this system (`ZoneLootTable` has no item level fields; `ZoneLootRoller` doesn’t accept ilvl).

**Zone id / enemy tier mapping (if any):**
- Mapping is via `EnemyTier`:
  - `ZoneLootTable.GetChancesForTier(EnemyTier tier)`
  - `ZoneLootTable.GetZoneRollsForTier(EnemyTier tier)`
  - `ZoneLootTable.GetMaterialsRollsForTier(EnemyTier tier)`
- Runtime tier comes from `DropOnDeath.tier`.

**Roll counts / tier mapping:**
- `zoneRollsTrash`, `zoneRollsNormal`, `zoneRollsElite`, `zoneRollsMiniBoss`
- plus materials: `rollMaterials`, `materialsRollsTrash/Normal/Elite/MiniBoss`

**Roller implementation:**
- `Abyss.Loot.ZoneLootRoller` in `Assets/Abyss/Loot/ZoneLootRoller.cs` (180 lines)
  - `RollZone(ZoneLootTable table, EnemyTier tier, Random rng=null, Action<string> logError=null)`
  - `ApplyBossOverrides(...)` includes extra rolls + uniques + `ApplyGuaranteedMinRarity(...)`

**Runtime component using it:**
- `DropOnDeath` in `Assets/Scripts/Loot/DropOnDeath.cs` (207 lines)
  - If `zoneLootTable != null`, it is **preferred** over legacy `DropTable`.
  - If tier is `EnemyTier.MiniBoss`, it calls `ZoneLootRoller.ApplyBossOverrides(...)`.

**Editor content generator:**
- Menu: `Tools/Abyssbound/Content/Create Zone1 Loot Table (from ItemDefinitions)...`
- Script: `Assets/Editor/Loot/ZoneLootTableTools.cs` (67 lines)
  - Creates a `ZoneLootTable` and buckets item definitions into `zonePool` vs `materialsPool`.

### B. “Zone1 tables” (Loot V2 system)

**Assets (Resources):**
- `Assets/Resources/Loot/Tables/Zone1_Trash.asset`
- `Assets/Resources/Loot/Tables/Zone1_Elite.asset`
- `Assets/Resources/Loot/Tables/Zone1_Boss.asset`

**ScriptableObject type:**
- `Abyssbound.Loot.LootTableSO` in `Assets/Scripts/LootSystem/LootTableSO.cs` (33 lines)

**Fields controlling rarity weights / caps:**
- `LootTableSO.rarities` is a list of `WeightedRarityEntry { rarity, weight }`.
- There is no explicit “cap” field; selection is purely weighted.
- Affix count per rarity is enforced in code (see `LootRollerV2.GetDeterministicAffixCountOrFallback`).

**Fields controlling itemLevel min/max:**
- **Not present** in `LootTableSO`. Item level is supplied at roll time (see section 4).

**Zone id mapping:**
- There is no explicit zone id field in `LootTableSO`.
- Zone-specific behavior exists in `LootRollerV2.TryGetZoneAffixPool(...)` using **table id/name prefix** (currently only `"Zone1"`).

---

## 2) How does the loot simulation tool work?

### A. ZoneLootTable simulator window (legacy/zone system)

**Menu entry:**
- `Tools/Abyssbound/QA/Simulate Zone Loot Rates (Window)...`

**Implementation:**
- `Abyss.Loot.Editor.ZoneLootSimulatorWindow` in `Assets/Editor/Loot/ZoneLootSimulatorWindow.cs` (138 lines)

**Inputs:**
- `ZoneLootTable table`
- `EnemyTier tier`
- `applyBossOverrides`
- `iterations` (clamped 1..5,000,000)
- `seed`
- `includeTopItems`, `topItemsCount`

**Core simulation method:**
- `private string Run()`
  - loops `iterations`
  - calls `ZoneLootRoller.RollZone(table, tier, rng)`
  - optionally calls `ZoneLootRoller.ApplyBossOverrides(table, drops, rng)`

**Outputs:**
- Builds a text report string with rarity distribution and optional top items.
- Writes to Console via `Debug.Log("[ZoneLootSimulator]\n" + report)`.

### B. Loot V2: “Simulate 200 Drops (Loot V2 Current Table)”

**Menu entry:**
- `Tools/Abyssbound/QA/Simulate 200 Drops (Loot V2 Current Table)`

**Implementation:**
- `Assets/Editor/Abyssbound/Loot/QA/Simulate200DropsCurrentTable.cs` (117 lines)

**Requirements:**
- Must be in **Play Mode**.

**Inputs:**
- Uses `LootQaSettings.ItemLevel`.
- Table selection:
  - if selected hierarchy object has `LootDropOnDeath` with `lootTable` → uses that
  - else loads `Resources.Load<LootTableSO>("Loot/Tables/Zone1_Trash")`

**Outputs:**
- Console log summary with rarity counts, top 10 affixes, and % with AttackSpeed/MoveSpeed.

### C. Mixed: “Simulate 200 Drops (Selected Enemy)”

**Menu entry:**
- `Tools/Abyssbound/QA/Simulate 200 Drops (Selected Enemy)`

**Implementation:**
- `Assets/Editor/Abyssbound/Loot/QA/Simulate200DropsSelectedEnemy.cs` (287 lines)

**Behavior:**
- Must be in **Play Mode**.
- If selected enemy has `LootDropOnDeath` with table → uses Loot V2 simulation (`SimulateLootV2`).
- Else uses legacy `DropOnDeath`:
  - if `DropOnDeath.zoneLootTable != null` → simulates `ZoneLootRoller` and attempts “best-effort” legacy affix rolling with `Abyss.Loot.LootRoller.RollInstance(def, drop.affixPool, drop.affixRules, rng)`.
  - else falls back to legacy `DropTableRoller.Roll(...)`.

**Outputs:**
- Console log with rarity counts, top 10 affixes.
- Loot V2 path additionally logs avg affixes per rarity and “sample item” strings.

### D. Smoke-test automation includes deterministic 200-drop sim

**Menu entries:**
- `Tools/Abyssbound/QA/Smoke Test/Run Loot QA Smoke Test (Setup + Spawn + Equip + Open UI)`
- `Tools/Abyssbound/QA/Smoke Test/Run Loot QA Full Automation (Multi-slot + 200 Drops)`
- `Tools/Abyssbound/QA/Smoke Test/Simulate 200 Drops (Auto LootTableSO)`

**Implementation:**
- `Assets/Editor/Abyssbound/Loot/QA/LootQaSmokeTestMenu.cs` (683 lines)

---

## 3) Is there already an affix weight system per zone?

### Loot V2 affixes (yes)

**Affix definition:**
- `Abyssbound.Loot.AffixDefinitionSO` in `Assets/Scripts/LootSystem/AffixDefinitionSO.cs` (45 lines)
  - `public int weight = 100;` (<= 0 means never roll)
  - tiering by item level via `List<AffixTier> tiers` (`minItemLevel/maxItemLevel/minRoll/maxRoll`)

**Zone pools (Zone1) as assets:**
- `Assets/Resources/Loot/AffixPools/Zone1_ArmorAffixes.asset`
- `Assets/Resources/Loot/AffixPools/Zone1_JewelryAffixes.asset`
- `Assets/Resources/Loot/AffixPools/Zone1_WeaponAffixes_Melee.asset`
- `Assets/Resources/Loot/AffixPools/Zone1_WeaponAffixes_Ranged.asset`
- `Assets/Resources/Loot/AffixPools/Zone1_WeaponAffixes_Magic.asset`

**How the pool is chosen (priority order):**
- Implemented in `Abyssbound.Loot.LootRollerV2` in `Assets/Scripts/LootSystem/LootRollerV2.cs` (399 lines)
  1) If `LootTableSO.affixPoolOverride` is non-empty → use it.
  2) Else `TryGetZoneAffixPool(baseItem, table)` → currently only Zone1.
     - Zone detection is by table `id` or `name` starting with `"Zone1"`.
     - Pool picked based on `baseItem.allowedAffixTags`.
     - Pool is loaded from Resources under `"Loot/AffixPools"`.
  3) Else: fallback to registry-wide affixes from `LootRegistryRuntime.GetAllAffixes()`.

**Per-zone/per-tier weights:**
- Per-zone is achieved by selecting different pool assets per zone.
- Per-tier (trash/elite/boss) is **not** a direct affix-weight concept in Loot V2; it’s “which table is used” and “which pool is used”.

### Legacy affixes (weighted, but zone-specific only if you assign zone-specific pools)

**Affix definition:**
- `Assets/Scripts/Loot/AffixDefinition.cs` (85 lines)

**Pools + weights:**
- `Assets/Scripts/Loot/AffixPool.cs` (58 lines)

**Affix roll-count rules:**
- `Assets/Scripts/Loot/AffixRollRules.cs` (50 lines)

Legacy per-zone behavior depends on which `DropOnDeath.affixPool` is assigned on the enemy/prefab.

---

## 4) How is item level determined for drops right now?

### Loot V2 runtime (authoritative for item-level scaling)

**Runtime component:**
- `LootDropOnDeath` in `Assets/Scripts/LootSystem/LootDropOnDeath.cs` (150 lines)

**Item level resolution order (exact):**
1) `LootQaSettings.TryGetItemLevelOverride(out lvl, out lvlSource)`
2) else if `LootDropOnDeath.itemLevel > 0` → uses that (source = `"Zone"`)
3) else `lvl = 1` (source = `"Default"`)

**QA override storage:**
- `LootQaSettings` in `Assets/Scripts/LootSystem/LootQaSettings.cs` (97 lines)
  - PlayerPrefs keys:
    - `LootQA_ZoneItemLevel`
    - `LootQA_DebugLogs`
  - clamps item level to 1..20.

**Menu to set QA item level:**
- `Tools/Abyssbound/QA/Set QA ItemLevel/1..20`
- implemented in `Assets/Editor/Abyssbound/Loot/QA/SetZoneItemLevelMenu.cs` (61 lines)

### ZoneLootTable / legacy system
- No item-level concept exists in `ZoneLootTable` or `ZoneLootRoller`.
- The legacy `DropOnDeath` path can still roll legacy affixes into a `LootItemInstance`, but it passes a fixed quantity (and does not incorporate Loot V2 item level tiering).

---

## 5) What automation hooks already exist under Tools/Abyssbound?

This list focuses on loot/QA relevant commands (others exist, but omitted for brevity).

### Content creation / configuration
- `Tools/Abyssbound/Content/Create Starter Loot Content`
  - `Assets/Editor/Abyssbound/Loot/CreateStarterLootContent.cs`
  - Creates/updates registries + starter items/affixes/rarities + loot tables under `Assets/Resources/Loot/...`.

- `Tools/Abyssbound/Content/Create Zone1 Loot Table (from ItemDefinitions)...`
  - `Assets/Editor/Loot/ZoneLootTableTools.cs`
  - Creates a `ZoneLootTable` asset and buckets ItemDefinitions into pools.

### QA configuration
- `Tools/Abyssbound/QA/Auto-Configure Zone Drops (Zone1)`
  - `Assets/Editor/Abyssbound/Loot/QA/AutoConfigureZone1Drops.cs` (305 lines)
  - Iterates enemy prefabs and:
    - assigns appropriate ZoneLootTable by tier
    - clears legacy `DropTable` if ZoneLootTable assigned
    - **disables legacy `DropOnDeath`** (to avoid stacking and to allow Loot V2 itemLevel scaling)
    - ensures `LootDropOnDeath` exists, enabled, and assigned a Zone1 Loot V2 table by tier
    - disables `EnemyLoot` components

- `Tools/Abyssbound/QA/Set QA ItemLevel/...`
  - `Assets/Editor/Abyssbound/Loot/QA/SetZoneItemLevelMenu.cs`

### QA simulation
- `Tools/Abyssbound/QA/Simulate Zone Loot Rates (Window)...`
  - `Assets/Editor/Loot/ZoneLootSimulatorWindow.cs`

- `Tools/Abyssbound/QA/Simulate 200 Drops (Loot V2 Current Table)`
  - `Assets/Editor/Abyssbound/Loot/QA/Simulate200DropsCurrentTable.cs`

- `Tools/Abyssbound/QA/Simulate 200 Drops (Selected Enemy)`
  - `Assets/Editor/Abyssbound/Loot/QA/Simulate200DropsSelectedEnemy.cs`

- `Tools/Abyssbound/QA/Smoke Test/...`
  - `Assets/Editor/Abyssbound/Loot/QA/LootQaSmokeTestMenu.cs`

### Best “where to add” for your next automation requests

You asked for:
- (a) apply Zone1 tuning preset
- (b) run sim 200/1000
- (c) export sim report

Evidence-based best insertion points:
- **Apply Zone1 tuning preset:** `Assets/Editor/Abyssbound/Loot/CreateStarterLootContent.cs` already authors the Loot V2 assets and is the established place for “write default weights into assets”.
- **Run sim 200/1000:** the `Simulate200Drops*.cs` scripts hardcode 200; adding 1000 is most consistent by extending these scripts (same output format, same assumptions).
- **Export sim report:** both sim paths already generate a `StringBuilder` report before logging; exporting is simply writing that string to a file from those scripts.

---

## 6) Potential pitfalls (especially sim vs runtime deltas)

### A. The biggest runtime-vs-sim divergence: which dropper is authoritative
- `LootDropOnDeath` explicitly exits early if legacy `DropOnDeath` exists and is enabled (to avoid double drops).
  - If an enemy has both components but legacy is still enabled, **Loot V2 sims won’t match runtime**.
  - The `Tools/Abyssbound/QA/Auto-Configure Zone Drops (Zone1)` tool exists largely to prevent this mismatch.

### B. ZoneLoot simulator does not model Loot V2 (and vice versa)
- The ZoneLoot simulator window only runs `ZoneLootRoller` (ItemDefinitions, chance tables, boss overrides).
- Loot V2 sims create `ItemInstance` via `LootRollerV2` and include affixes + item-level tiering.

### C. Zone detection for Loot V2 affix pools is string-based
- `LootRollerV2.TryGetZoneAffixPool(...)` uses table `id/name` prefix `"Zone1"`.
- If your table id naming deviates, affix pools silently fall back to registry-wide affixes → **major distribution change**.

### D. Deterministic affix counts can surprise tuners
- For rarity ids `Common/Uncommon/Magic/Rare/Epic/Legendary`, affix counts are forced to 0/0/1/2/3/4 in `LootRollerV2`.
- Editing `RarityDefinitionSO.affixMin/affixMax` for those ids will not affect results.

### E. RNG determinism differs between sims and runtime
- The ZoneLoot sim window seeds and passes a `System.Random rng` (deterministic).
- Runtime drops often pass `rng=null` (falls back to `UnityEngine.Random`), so results are nondeterministic by design.

---

## Quick QA workflow (recommended, based on existing tools)

1) (Once) Run `Tools/Abyssbound/Content/Create Starter Loot Content` to ensure Loot V2 registries/tables exist.
2) Run `Tools/Abyssbound/QA/Auto-Configure Zone Drops (Zone1)` to standardize enemy prefabs so Loot V2 is used.
3) Enter Play Mode.
4) Use `Tools/Abyssbound/QA/Set QA ItemLevel/<n>` to set item-level scaling.
5) Run:
   - `Tools/Abyssbound/QA/Simulate 200 Drops (Selected Enemy)` (for a specific enemy)
   - or `Tools/Abyssbound/QA/Simulate 200 Drops (Loot V2 Current Table)` (for selected `LootDropOnDeath`)
6) (Legacy validation) Use `Tools/Abyssbound/QA/Simulate Zone Loot Rates (Window)...` only when validating ZoneLootTable rarity/roll-count math (not Loot V2 outcomes).

---

## Evidence index (scripts and sizes)

Line counts were pulled via PowerShell (for quick sanity / file size awareness):
- `Assets/Scripts/LootSystem/LootTableSO.cs` (33)
- `Assets/Scripts/LootSystem/LootRollerV2.cs` (399)
- `Assets/Scripts/LootSystem/LootDropOnDeath.cs` (150)
- `Assets/Scripts/LootSystem/LootQaSettings.cs` (97)
- `Assets/Abyss/Loot/ZoneLootTable.cs` (161)
- `Assets/Abyss/Loot/ZoneLootRoller.cs` (180)
- `Assets/Editor/Loot/ZoneLootSimulatorWindow.cs` (138)
- `Assets/Editor/Loot/ZoneLootTableTools.cs` (67)
- `Assets/Editor/Abyssbound/Loot/QA/SetZoneItemLevelMenu.cs` (61)
- `Assets/Editor/Abyssbound/Loot/QA/Simulate200DropsSelectedEnemy.cs` (287)
- `Assets/Editor/Abyssbound/Loot/QA/Simulate200DropsCurrentTable.cs` (117)
- `Assets/Editor/Abyssbound/Loot/QA/AutoConfigureZone1Drops.cs` (305)
- `Assets/Editor/Abyssbound/Loot/QA/LootQaSmokeTestMenu.cs` (683)
- `Assets/Editor/Abyssbound/Loot/CreateStarterLootContent.cs` (387)
- `Assets/Scripts/LootSystem/AffixDefinitionSO.cs` (45)
- `Assets/Scripts/LootSystem/AffixPoolSO.cs` (18)
- `Assets/Scripts/Loot/AffixPool.cs` (58)
- `Assets/Scripts/Loot/AffixDefinition.cs` (85)
- `Assets/Scripts/Loot/AffixRollRules.cs` (50)
- `Assets/Scripts/Loot/DropOnDeath.cs` (207)

---

## “Not found / not present” confirmations

These were explicitly checked in the referenced scripts:
- **No per-table itemLevel min/max fields** in `LootTableSO`.
- **No item level concept at all** in `ZoneLootTable` / `ZoneLootRoller`.
- **No explicit zone id field** in `LootTableSO`; zone-specific affix behavior is based on table id/name prefix (`"Zone1"`).
