# Abyssbound Editor Tools

This project consolidates in-editor utilities under:

- `Tools/Abyssbound/QA/...`
- `Tools/Abyssbound/Dev/...`
- `Tools/Abyssbound/Content/...`
- `Tools/Abyssbound/Maintenance/...`

## QA (Play-mode helpers)

Common loot/gear QA workflows:

- `Tools/Abyssbound/QA/Simulate 200 Drops (Selected Enemy)`
  - Select an enemy GameObject in the scene.
  - Runs the legacy/zone drop pipeline simulation for that enemy.

- `Tools/Abyssbound/QA/Simulate 200 Drops (Loot V2 Current Table)`
  - Uses the Loot V2 pipeline (current/active loot table context).

- `Tools/Abyssbound/QA/Simulate Zone Loot Rates (Window)...`
  - Opens the ZoneLoot simulator window for quick tuning sanity checks.

- `Tools/Abyssbound/QA/Auto-Configure Zone Drops (Zone1)`
  - Creates/updates Zone1 drop configuration defaults for faster iteration.

- `Tools/Abyssbound/QA/Items/Assign Test Rarities (Non-destructive)`
  - Convenience tool for assigning a few known test item rarities.
  - Logs a single summary (non-spammy).

Notes:
- Some QA menu actions require Play Mode; they will warn if run in Edit Mode.
- Tier naming uses `EnemyTier.Trash` displayed as "Trash Mob" in logs/UI.

## Dev (Scene setup / dev cheats)

- `Tools/Abyssbound/Dev/Ensure Bootstrapper In Scene`
  - Ensures required scene bootstrap/foundation objects exist.

- `Tools/Abyssbound/Dev/Create DevCheats In Scene`
  - Adds dev cheat helpers to the active scene (if your current workflow uses them).

## Content (Asset creation)

- `Tools/Abyssbound/Content/Create Starter Loot Content`
  - Creates starter loot ScriptableObjects for bootstrapping.

- `Tools/Abyssbound/Content/Create Zone1 Loot Table (from ItemDefinitions)...`
  - Builds a Zone1 table from the current ItemDefinitions.

- `Tools/Abyssbound/Content/Loot/Create Abyssal Sigil Sample Assets`
  - Creates sample legacy assets (Abyssal Sigil + a small DropTable).

- `Tools/Abyssbound/Content/Gates/Create Zone1 Boss Gate Definition`
  - Creates/selects the Zone1 boss gate definition asset.

## Maintenance (Project hygiene / migrations)

- `Tools/Abyssbound/Maintenance/Project Hygiene`
  - Safe organizer for common asset types.
  - Defaults to Dry Run (no changes) and skips `Assets/Resources` to avoid breaking `Resources.Load`.

- `Tools/Abyssbound/Maintenance/Migrate Fodder -> Trash (Summary)`
  - Renames asset *filenames* containing "Fodder" to "Trash" (case-insensitive) and prints a one-shot summary.
  - Core serialization safety for tier/field renames is handled in code via `FormerlySerializedAs`.

- `Tools/Abyssbound/Maintenance/Items/Backfill ItemDefinition Rarity Defaults`
  - Normalizes invalid/legacy rarity values on `ItemDefinition` assets.

- `Tools/Abyssbound/Maintenance/Legacy/Gates/...`
  - Older gate creation helpers kept for reference/back-compat.
