# ABYSSBOUND — Legs Icon Fix Summary

Generated: 2025-12-26 03:20:32Z

Goal: Legs must use a true legs silhouette everywhere; Boots must use boots silhouette; Basic Legs in merchant must not show belt silhouette.

## A) Sprite availability
- sil_legs: Assets/Abyss/Equipment/Icons/sil_legs.png
- sil_boots: Assets/Abyss/Equipment/Icons/sil_boots.png
- sil_belt: Assets/Abyss/Equipment/Icons/sil_belt.png
- sil_chest: Assets/Abyss/Equipment/Icons/sil_chest.png

## A2/A3) Equipment UI slot background sprites (before fix)
- No PlayerEquipmentUI prefabs found.

## B) Legacy merchant item (ShopInventory_Weapons) — Basic Legs
- No change: icon is not a placeholder/wrong silhouette

## C) Loot V2 legs items (Assets/Resources/Loot/Items)
- Updated legs items: 0

## D) Equipment UI slot background mapping (prefabs)
- Prefabs changed: 0

## D2) Equipment UI slot background sprites (after fix)
- No PlayerEquipmentUI prefabs found.

## Changes
- Legacy Basic Legs updated: 0
- Loot V2 Legs updated: 0
- Equipment UI prefabs updated: 0
- Legs distinct fallback used (TODO sil_legs needed): 0

## QA steps
1) Open Weapons & Gear shop: Basic Legs icon should be legs silhouette.
2) Open Equipment UI: Legs slot shows legs silhouette; Boots slot shows boots silhouette.
3) Kill a mob and pick up legs drop (if available): inventory icon shows legs silhouette.
