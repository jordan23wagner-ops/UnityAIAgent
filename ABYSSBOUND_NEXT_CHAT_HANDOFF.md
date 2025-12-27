# ABYSSBOUND — Next Chat Handoff Pack (Dec 26, 2025)

## 1) Project snapshot
- **Unity**: 6.3 LTS
- **Primary scene**: [Assets/Scenes/Abyssbound_Zone1.unity](Assets/Scenes/Abyssbound_Zone1.unity)
- **Core loops confirmed working** (Zone1): combat, aggro/chase, loot drops (Loot V2), inventory UI, equipment UI, set bonus application.

## 2) Key systems + file paths

### Loot V2 (rolling + rarity + ilvl tuning)
- **Main roller**: [Assets/Scripts/LootSystem/LootRollerV2.cs](Assets/Scripts/LootSystem/LootRollerV2.cs)
  - Rolls base item + rarity + scalar + affixes. Supports optional per-tier rarity weights overrides.
- **Loot tables (ScriptableObject)**: [Assets/Scripts/LootSystem/LootTableSO.cs](Assets/Scripts/LootSystem/LootTableSO.cs)
  - `items` and `rarities` weighted lists.
- **Death hook / spawning pickup or granting to inventory**: [Assets/Scripts/LootSystem/LootDropOnDeath.cs](Assets/Scripts/LootSystem/LootDropOnDeath.cs)
  - Applies Zone1 tuning when available and injects optional Zone1 set drops.
- **Zone tuning SO + helpers**:
  - Type + accessors: [Assets/Scripts/LootSystem/Tuning/ZoneLootTuningSO.cs](Assets/Scripts/LootSystem/Tuning/ZoneLootTuningSO.cs)
  - Zone1 loader + tier classification: [Assets/Scripts/LootSystem/Tuning/Zone1LootTuning.cs](Assets/Scripts/LootSystem/Tuning/Zone1LootTuning.cs)

### Loot registry/runtime resolution (rolled-id → base item → display)
- **Runtime registry singleton**: [Assets/Scripts/LootSystem/LootRegistryRuntime.cs](Assets/Scripts/LootSystem/LootRegistryRuntime.cs)
  - Loads bootstrap from `Resources/Loot/Bootstrap.asset` and resolves display/icon for rolled instances.
- **Registry bootstrap assets** (must exist for clean runtime indexing):
  - [Assets/Resources/Loot/Bootstrap.asset](Assets/Resources/Loot/Bootstrap.asset)
  - [Assets/Resources/Loot/ItemRegistry.asset](Assets/Resources/Loot/ItemRegistry.asset)
  - [Assets/Resources/Loot/RarityRegistry.asset](Assets/Resources/Loot/RarityRegistry.asset)
  - [Assets/Resources/Loot/AffixRegistry.asset](Assets/Resources/Loot/AffixRegistry.asset)
- **Rolled instance model**: [Assets/Scripts/LootSystem/ItemInstance.cs](Assets/Scripts/LootSystem/ItemInstance.cs)
  - Stores `baseItemId`, `rarityId`, `itemLevel`, affixes; computes `StatMod` list.

### Inventory + Equipment UI
- **Inventory runtime container**: [Assets/Scripts/Player/PlayerInventory.cs](Assets/Scripts/Player/PlayerInventory.cs)
- **Inventory UI**: [Assets/Abyss/Inventory/PlayerInventoryUI.cs](Assets/Abyss/Inventory/PlayerInventoryUI.cs)
  - Supports rolled instance ids stored as strings; uses `LootRegistryRuntime` to resolve base slot and display.
- **Equipment runtime container**: [Assets/Abyss/Equipment/PlayerEquipment.cs](Assets/Abyss/Equipment/PlayerEquipment.cs)
- **Equipment resolver**: [Assets/Abyss/Equipment/PlayerEquipmentResolver.cs](Assets/Abyss/Equipment/PlayerEquipmentResolver.cs)
- **Equipment UI**: [Assets/Abyss/Equipment/PlayerEquipmentUI.cs](Assets/Abyss/Equipment/PlayerEquipmentUI.cs)
- **Tooltip UI (rolled instance aware)**: [Assets/Scripts/UI/ItemTooltipUI.cs](Assets/Scripts/UI/ItemTooltipUI.cs)

### Set bonus system
- **Set definition type**: [Assets/Scripts/LootSystem/ItemSetDefinitionSO.cs](Assets/Scripts/LootSystem/ItemSetDefinitionSO.cs)
- **Zone1 set** (Abyssal Initiate): [Assets/GameData/Sets/ItemSet_AbyssalInitiate.asset](Assets/GameData/Sets/ItemSet_AbyssalInitiate.asset)
  - Tiers: 2pc `+1 Defense`, 3pc `+5 Max Health`.
- **Equipped set tracking**: [Assets/Scripts/LootSystem/EquippedSetTracker.cs](Assets/Scripts/LootSystem/EquippedSetTracker.cs)
- **Bonus evaluation / stat accumulation**: [Assets/Scripts/LootSystem/SetBonusRuntime.cs](Assets/Scripts/LootSystem/SetBonusRuntime.cs)

### DevCheats + QA hotkeys
- **Create cheats object in scene (Edit Mode)**: [Assets/Editor/Dev/DevCheatsSetup.cs](Assets/Editor/Dev/DevCheatsSetup.cs)
  - Menu: `Tools/Abyssbound/Dev/Create DevCheats In Scene`
- **Runtime cheats (Play Mode)**: [Assets/Game/Dev/DevCheats.cs](Assets/Game/Dev/DevCheats.cs)
  - Hotkeys: `F1` GodMode, `F2` Spawn, `F3` Kill Spawned, `F4` SelfDamage, `F8/F9/F10` spawn Trash/Elite/Boss with Loot V2 tables.
- **Extra cheat helpers (spawn selected items, grant test items)**: [Assets/Game/Debug/DevCheatActions.cs](Assets/Game/Debug/DevCheatActions.cs)

### Editor menu scripts (Tools/Abyssbound/*)
Key “day to day” menus live under [Assets/Editor/Abyssbound](Assets/Editor/Abyssbound) plus a few top-level editors under [Assets/Editor](Assets/Editor).
- Zone1 loot tuning + sims: [Assets/Editor/Abyssbound/Loot/Zone1/Zone1LootTierTuningToolsEditor.cs](Assets/Editor/Abyssbound/Loot/Zone1/Zone1LootTierTuningToolsEditor.cs)
- Set drops tuning + sims: [Assets/Editor/Abyssbound/Loot/SetDrops/Zone1SetDropToolsEditor.cs](Assets/Editor/Abyssbound/Loot/SetDrops/Zone1SetDropToolsEditor.cs)
- Loot V2 QA automation: [Assets/Editor/Abyssbound/Loot/QA/LootQaSmokeTestMenu.cs](Assets/Editor/Abyssbound/Loot/QA/LootQaSmokeTestMenu.cs)
- Selected item QA tooling: [Assets/Editor/Abyssbound/Loot/QA/LootQaSelectedItemSettingsEditor.cs](Assets/Editor/Abyssbound/Loot/QA/LootQaSelectedItemSettingsEditor.cs)
- Starter set QA: [Assets/Editor/Abyssbound/Sets/QA/StarterSetQaMenu.cs](Assets/Editor/Abyssbound/Sets/QA/StarterSetQaMenu.cs)
- Inventory/equipment UI builders:
  - [Assets/Editor/BuildPlayerInventoryUIEditor.cs](Assets/Editor/BuildPlayerInventoryUIEditor.cs)
  - [Assets/Editor/BuildPlayerEquipmentUIEditor.cs](Assets/Editor/BuildPlayerEquipmentUIEditor.cs)
- Icon automation/validation: [Assets/Editor/Abyssbound/Content/Icons/AutoAssignEquipmentSlotIconsEditor.cs](Assets/Editor/Abyssbound/Content/Icons/AutoAssignEquipmentSlotIconsEditor.cs)

## 3) Current loot tuning

### Zone1 rarity weights + item level ranges
- **Tuning asset**: [Assets/GameData/Loot/Tuning/Zone1_LootTuning.asset](Assets/GameData/Loot/Tuning/Zone1_LootTuning.asset)
- **Loader**: [Assets/Scripts/LootSystem/Tuning/Zone1LootTuning.cs](Assets/Scripts/LootSystem/Tuning/Zone1LootTuning.cs)

**Trash weights** (common→legendary): `68 / 20 / 10 / 1.8 / 0.2 / 0.02`
- Item level range: `1–5`

**Elite weights** (common→legendary): `45 / 25 / 20 / 8 / 1.8 / 0.2`
- Item level range: `4–8`

**Boss weights** (common→legendary): `20 / 20 / 35 / 18 / 6 / 1`
- Item level range: `8–12`

**Where to change**
- Edit [Assets/GameData/Loot/Tuning/Zone1_LootTuning.asset](Assets/GameData/Loot/Tuning/Zone1_LootTuning.asset) directly, or use the editor preset tool:
  - `Tools/Abyssbound/Loot/Zone1/Apply Tier Preset (Trash/Elite/Boss)`

### Zone1 set drops (Abyssal Initiate)
- **Config asset**: [Assets/GameData/Loot/SetDrops/Zone1_AbyssalInitiate_SetDropConfig.asset](Assets/GameData/Loot/SetDrops/Zone1_AbyssalInitiate_SetDropConfig.asset)
- **Runtime**:
  - Config type: [Assets/Scripts/LootSystem/SetDrops/SetDropConfigSO.cs](Assets/Scripts/LootSystem/SetDrops/SetDropConfigSO.cs)
  - Zone1 config loader: [Assets/Scripts/LootSystem/SetDrops/Zone1AbyssalInitiateSetDrops.cs](Assets/Scripts/LootSystem/SetDrops/Zone1AbyssalInitiateSetDrops.cs)
  - Roll logic + pity: [Assets/Scripts/LootSystem/SetDrops/SetDropRuntime.cs](Assets/Scripts/LootSystem/SetDrops/SetDropRuntime.cs)

**Current rates (percent)**
- Trash: `0.5%` (rolls `1` piece on hit)
- Elite: `2%` (rolls `1` piece on hit)
- Boss: `6%` (rolls `1` piece on hit)

**Boss pity (session-only)**
- Enabled: `true`
- Threshold: `10` boss kills
- Guarantee: `true` (forces 1 piece when threshold reached)

**Where to change**
- Edit [Assets/GameData/Loot/SetDrops/Zone1_AbyssalInitiate_SetDropConfig.asset](Assets/GameData/Loot/SetDrops/Zone1_AbyssalInitiate_SetDropConfig.asset) or use:
  - `Tools/Abyssbound/Loot/Set Drops/Apply Zone1 Preset (Trash/Elite/Boss)`

## 4) QA workflows (step-by-step)

### Run sims (200 / 1000)
1. Use Zone1 Loot V2 tier sims:
   - `Tools/Abyssbound/Loot/Zone1/Sim 200 (Trash|Elite|Boss)`
   - `Tools/Abyssbound/Loot/Zone1/Sim 1000 (Trash|Elite|Boss)`
2. Use Set Drops sims:
   - `Tools/Abyssbound/Loot/Set Drops/Sim 200 (Trash|Elite|Boss)`
   - `Tools/Abyssbound/Loot/Set Drops/Sim 1000 (Trash|Elite|Boss)`
3. If you want “selected enemy” sims:
   - `Tools/Abyssbound/Loot/Sim Loot V2 (Selected Enemy) — 200/1000`

### Spawn items for affix QA
1. Set the “Selected Item”:
   - `Tools/Abyssbound/QA/Selected Item/Set Selected From Project Selection`
2. Spawn variants:
   - `Tools/Abyssbound/QA/Spawn Selected Item (All Rarities)`
3. Notes:
   - Runtime helper is [Assets/Scripts/LootSystem/LootQaSpawnHelper.cs](Assets/Scripts/LootSystem/LootQaSpawnHelper.cs)
   - It drops to world if the inventory UI would exceed 28 visible slots.

### Validate set bonuses
1. Enter Play Mode in [Assets/Scenes/Abyssbound_Zone1.unity](Assets/Scenes/Abyssbound_Zone1.unity).
2. Run:
   - `Tools/Abyssbound/QA/Give + Equip Starter Set (Abyssal Initiate) #F5`
3. Validate via:
   - `Tools/Abyssbound/QA/Print Active Set Bonus Keys`
   - Observe HUD panel (DMG/HP/DR): [Assets/Game/UI/PlayerStatsHudPanel.cs](Assets/Game/UI/PlayerStatsHudPanel.cs)

### Validate icon assignments
1. Audit equipment icons:
   - `Tools/Abyssbound/Content/Icons/Report Missing/Placeholder Icons (Equipment)`
2. Auto-fix placeholders:
   - `Tools/Abyssbound/Content/Icons/Fix Weapon+Gear Icons (Silhouettes)`
3. Starter set verification:
   - `Tools/Abyssbound/Content/Icons/Verify Starter Set Icons`
4. Debug “where did this icon come from”:
   - `Tools/Abyssbound/Debug/Log Equipped Item Icon Sources` ([Assets/Editor/Abyssbound/Debug/EquipIconSourceLoggingEditor.cs](Assets/Editor/Abyssbound/Debug/EquipIconSourceLoggingEditor.cs))

## 5) Recent fixes + important context

### Icon pass status
- Primary tooling + history is under:
  - [Assets/Editor/Abyssbound/Content/Icons/AutoAssignEquipmentSlotIconsEditor.cs](Assets/Editor/Abyssbound/Content/Icons/AutoAssignEquipmentSlotIconsEditor.cs)
- Recent summaries in project root:
  - [ABYSSBOUND_LEGS_FINAL_FIX_SUMMARY.md](ABYSSBOUND_LEGS_FINAL_FIX_SUMMARY.md)
  - [ABYSSBOUND_LEGS_ICON_FIX_SUMMARY.md](ABYSSBOUND_LEGS_ICON_FIX_SUMMARY.md)
  - [ABYSSBOUND_STARTER_LEGS_ICON_FIX.md](ABYSSBOUND_STARTER_LEGS_ICON_FIX.md)

### Issues encountered + resolved
- **Struct/null compile pitfall (CS0019)**
  - Fix: [Assets/Scripts/LootSystem/LootRollerV2.cs](Assets/Scripts/LootSystem/LootRollerV2.cs)
  - Note: [ABYSSBOUND_COMPILE_FIX_LOOTROLLERV2.md](ABYSSBOUND_COMPILE_FIX_LOOTROLLERV2.md)
- **Import Error Code:(4) SourceAssetDB timestamp mismatch** (Starter legs asset)
  - Fix menu items: [Assets/Editor/Abyssbound/Maintenance/SourceAssetDbTimestampMismatchTools.cs](Assets/Editor/Abyssbound/Maintenance/SourceAssetDbTimestampMismatchTools.cs)
  - Summary: [ABYSSBOUND_CONSOLE_CLEANUP_SUMMARY.md](ABYSSBOUND_CONSOLE_CLEANUP_SUMMARY.md)

### Known gotchas / lessons learned
- **Rolled instance ids vs base item ids**: inventory/equipment frequently store a rolled id (e.g., `ri_...`), which must be resolved through [Assets/Scripts/LootSystem/LootRegistryRuntime.cs](Assets/Scripts/LootSystem/LootRegistryRuntime.cs).
- **Registry bootstrap must exist**: if you see missing bootstrap warnings, run `Tools/Abyssbound/Content/Create Starter Loot Content` ([Assets/Editor/Abyssbound/Loot/CreateStarterLootContent.cs](Assets/Editor/Abyssbound/Loot/CreateStarterLootContent.cs)).
- **Inventory UI is 4x7 (28 slots)**: QA spawners may drop to world when “full” so items remain visible (see [Assets/Scripts/LootSystem/LootQaSpawnHelper.cs](Assets/Scripts/LootSystem/LootQaSpawnHelper.cs)).
- **Icon changes may need a clean rebuild**: if runtime icons don’t update, restart Play Mode with Domain Reload enabled so `LootRegistryRuntime` rebuilds.
- **Deprecation noise intentionally ignored**: Input Manager / legacy input warnings are not blocking; avoid spending time unless they break builds.

## 6) Next steps: Stats system + visibility (Attack / Str / Def etc.)

### Current state
- `StatType` already has RPG-style fields: [Assets/Scripts/LootSystem/StatType.cs](Assets/Scripts/LootSystem/StatType.cs) (`Attack`, `Strength`, `DefenseSkill`, `RangedSkill`, `MagicSkill`, `MeleeSkill`).
- HUD currently shows DMG/HP/DR via [Assets/Game/UI/PlayerStatsHudPanel.cs](Assets/Game/UI/PlayerStatsHudPanel.cs).
- Damage currently aggregates:
  - Base + equipment contributions in [Assets/Game/Player/PlayerCombatStats.cs](Assets/Game/Player/PlayerCombatStats.cs)
  - Loot V2 mods via `ItemInstance.GetAllStatMods(...)`
  - Set bonuses via [Assets/Scripts/LootSystem/SetBonusRuntime.cs](Assets/Scripts/LootSystem/SetBonusRuntime.cs)

### Proposed implementation plan
1. **Single source of truth for derived stats**
   - Create a `PlayerDerivedStats` (or similar) component that computes:
     - Attack / Strength / DefenseSkill (and optionally the existing DMG/HP/DR totals)
   - Inputs: equipment slots (rolled ids), `LootRegistryRuntime`, `ItemInstance` stat mods, set bonuses.
2. **Extend set bonuses + item base stats to support RPG stats**
   - Ensure `ItemDefinitionSO.baseStats` and affixes can roll `Attack/Strength/DefenseSkill` etc.
   - Keep percent mods gated until there’s a consistent stacking rule (currently percent is ignored in several accumulators).
3. **Update UI visibility**
   - Extend [Assets/Game/UI/PlayerStatsHudPanel.cs](Assets/Game/UI/PlayerStatsHudPanel.cs) to show Attack/Str/Def alongside (or instead of) DMG/HP/DR.
   - Keep refresh on equipment change to avoid polling-only updates.
4. **QA helpers / automation**
   - Add a `Tools/Abyssbound/QA/Print Player Derived Stats` menu item (fast sanity check).
   - Optionally add a “spawn item with Attack/Str/Def affixes” dev helper using [Assets/Scripts/LootSystem/LootQaSpawnHelper.cs](Assets/Scripts/LootSystem/LootQaSpawnHelper.cs).

### Decisions needed
- Final stat list for MVP HUD: only `Attack/Strength/DefenseSkill`, or also include `RangedSkill/MagicSkill/MeleeSkill`?
- Display rules: show **raw totals** only, or totals + breakdown (base / gear / set bonuses)?
- Percent stacking: define when/where percent modifiers start applying.
