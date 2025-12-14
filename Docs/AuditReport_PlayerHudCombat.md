# Abyssbound – Player / HUD / Combat Audit Report

Date: 2025-12-13

## Executive Summary
- **Player exists in scenes**, but the **Player prefab is minimal** (no scripts, Untagged).
- **No HUD/Canvas/EventSystem** found in either main scene.
- **No player combat or player health scripts** found; only `EnemyHealth` exists.
- **Main Camera exists** and uses `TopDownFollowCamera`, but it relies on a **target reference** that can be null.
- This explains “boss activates but player/UI/combat are missing” after transitions: there is **no persistent foundation** guaranteeing Player + HUD + Camera rig across loads.

## What Exists

### Player-related scripts/components
- Movement: `DebugPlayerMover_NewInput` (New Input System, moves transform)
- Inventory: `PlayerInventory` (Add/Has/Count/TryConsume)
- Combat: **none found** (no player-side attack script)
- Health: **none found** (no `PlayerHealth`-like component)

### Enemy / loot dependencies (for combat integration)
- `EnemyHealth` supports `TakeDamage(int)` and calls `DropOnDeath.OnDeath()`.
- `DropOnDeath` grants items into `PlayerInventory`.

### Camera
- `TopDownFollowCamera` exists and follows a serialized `target`.

### UI / HUD
- **No Canvas / HUD prefab** found under `Assets/Prefabs`.
- **No HealthBar UI controller** script found.

## Prefabs

### Player prefab
- `Assets/Prefabs/Player/Player.prefab`
  - Has MeshFilter/MeshRenderer/CapsuleCollider.
  - **Tag is Untagged**.
  - No scripts on the prefab itself.

### Enemy prefab
- `Assets/Prefabs/Enemy_Dummy/Enemy_Dummy.prefab` exists (enemy-side systems were added previously).

## Scenes

### Assets/Scenes/Abyssbound_Zone1.unity
- Has **Main Camera** with `TopDownFollowCamera`.
- Has **Zone1_BossGate_Trigger** with `BossGate` and `OnOpened` wired to `BossEncounterController`.
- Has **Zone1_BossEncounter** with `BossEncounterController`.
- Has **Player_Hero** as an instance of the Player prefab, with added components:
  - `DebugPlayerMover_NewInput`
  - `PlayerInventory`
  - `Rigidbody` (kinematic)
- **No Canvas / HUD / EventSystem** present.

### Assets/Scenes/SampleScene.unity
- Appears to include Player inventory as well.
- **No Canvas / HUD / EventSystem** present.

## Recommended Next Steps
1. Add a runtime **bootstrapper** that guarantees:
   - exactly one Player (tagged `Player`)
   - exactly one HUD Canvas with a health bar
   - exactly one camera rig that follows the player
   - objects persist via `DontDestroyOnLoad`
2. Add minimal **PlayerHealth** and **SimplePlayerCombat** (only because they do not exist).
   - Combat should call `EnemyHealth.TakeDamage()` to integrate with existing enemy death/loot.
3. Add editor automation to:
   - place a `_GameBootstrapper` into the current scene
   - auto-find prefabs or generate minimal prefabs if missing
4. Add scene auditing to report which scenes are missing the foundation objects.

---
This audit was produced by repository scanning (scripts, prefabs, and scene YAML), without mutating any scenes or assets.
