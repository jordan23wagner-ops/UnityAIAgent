using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Abyss.Equipment;
using Abyss.Items;
using Abyss.Shop;
using Abyssbound.Loot;
using UnityEditor;
using UnityEngine;

namespace Abyssbound.EditorTools
{
    public static class AutoAssignEquipmentSlotIconsEditor
    {
        private const string LootV2ItemsRoot = "Assets/Resources/Loot/Items";
        private const string WeaponsShopInventoryPath = "Assets/Abyss/Shops/Inventories/ShopInventory_Weapons.asset";

        private const string MenuBase = "Tools/Abyssbound/Content/Icons/";
        private const string MenuReport = MenuBase + "Report Missing/Placeholder Icons (Equipment)";
        private const string MenuAssign = MenuBase + "Auto-Assign Equipment Slot Icons";
        private const string MenuReportLegacyWeapons = MenuBase + "Report Legacy Shop Placeholder Icons (Weapons)";
        private const string MenuAssignLegacyWeapons = MenuBase + "Auto-Assign Legacy Shop Icons (Weapons)";
        private const string MenuReportWeaponGear = MenuBase + "Report Weapon+Gear Placeholder Icons";
        private const string MenuFixWeaponGear = MenuBase + "Fix Weapon+Gear Icons (Silhouettes)";
        private const string MenuFixLegsConsistency = MenuBase + "Fix Legs Icon Consistency + Write Summary";
        private const string MenuAuthoritativeLegsBootsFix = MenuBase + "Authoritative Fix Legs+Boots Icons (Legacy + Loot V2)";
        private const string MenuFinalLegsFixArtAndData = MenuBase + "FINAL Fix Legs Icon (Art + Data) + Write Summary";
        private const string MenuFixStarterLegsIcon = MenuBase + "Fix Loot V2 Starter Legs Icon (Equipment UI)";
        private const string MenuVerifyStarterSetIcons = MenuBase + "Verify Starter Set Icons";
        private const string MenuReportEquipUiSlotMapping = MenuBase + "Report Slot Icon Mapping (Equipment UI)";

        private const string SilRoot = "Assets/Abyss/Equipment/Icons/";
        private const string SilSwordPath = SilRoot + "sil_sword.png";
        private const string SilBowPath = SilRoot + "sil_bow.png";
        private const string SilStaffPath = SilRoot + "sil_staff.png";
        private const string Sil2HPath = SilRoot + "sil_2h.png";
        private const string SilHelmPath = SilRoot + "sil_helm.png";
        private const string SilChestPath = SilRoot + "sil_chest.png";
        private const string SilLegsPath = SilRoot + "sil_legs.png";
        private const string SilGlovesPath = SilRoot + "sil_gloves.png";
        private const string SilBootsPath = SilRoot + "sil_boots.png";
        private const string SilBeltPath = SilRoot + "sil_belt.png";
        private const string SilShieldPath = SilRoot + "sil_shield.png";
        private const string SilAmuletPath = SilRoot + "sil_amulet.png";
        private const string SilRingPath = SilRoot + "sil_ring.png";
        private const string SilCapePath = SilRoot + "sil_cape.png";
        private const string SilArrowsPath = SilRoot + "sil_arrows.png";
        private const string SilOrbPath = SilRoot + "sil_orb.png";

        private const string StarterLegsLootV2Path = "Assets/GameData/Loot/StarterSet/Item_Starter_Legs.asset";
        private const string StarterHelmLootV2Path = "Assets/GameData/Loot/StarterSet/Item_Starter_Helm.asset";
        private const string StarterChestLootV2Path = "Assets/GameData/Loot/StarterSet/Item_Starter_Chest.asset";

        // Step B scope: treat these as placeholder. (Do NOT match "letter" to avoid false positives like "Leather".)
        private static readonly string[] PlaceholderNeedles = { "placeholder", "tmp" };

        private static readonly Dictionary<string, Sprite> SpriteCache = new(StringComparer.OrdinalIgnoreCase);

        private static bool s_WarnedMissingSilLegsTodo;

        private static readonly HashSet<string> s_WarnedOnce = new(StringComparer.OrdinalIgnoreCase);

        private static readonly Regex LetterWordRegex = new Regex("(^|[^a-z])letter([^a-z]|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private enum WeaponKind
        {
            Unknown = 0,
            Bow = 1,
            Staff = 2,
            Generic = 3,
        }

        [MenuItem(MenuReport)]
        public static void ReportMissingOrPlaceholderIcons_EquipmentOnly()
        {
            SpriteCache.Clear();
            AssetDatabase.Refresh();
            var report = ScanLootV2Equipment();
            LogReport("ICON REPORT (Loot V2 Equipment)", report);
        }

        [MenuItem(MenuAssign)]
        public static void AutoAssignEquipmentSlotIcons()
        {
            SpriteCache.Clear();
            AssetDatabase.Refresh();
            var report = ScanLootV2Equipment();

            int updated = 0;
            var updatedPaths = new List<string>();
            var warnedMissingSlotSprites = new HashSet<EquipmentSlot>();

            for (int i = 0; i < report.Items.Count; i++)
            {
                var entry = report.Items[i];
                if (!entry.IsMissingOrPlaceholder)
                    continue;

                var desired = GetSlotSprite(entry.Slot, entry.OccupiesBothHands, warnedMissingSlotSprites);
                if (desired == null)
                    continue;

                if (entry.Item != null && entry.Item.icon == desired)
                    continue;

                Undo.RecordObject(entry.Item, "Auto-Assign Equipment Slot Icon");
                entry.Item.icon = desired;
                EditorUtility.SetDirty(entry.Item);

                updated++;
                updatedPaths.Add(entry.AssetPath);
            }

            if (updated > 0)
                AssetDatabase.SaveAssets();

            int skipped = report.Scanned - report.MissingOrPlaceholder;

            Debug.Log(
                "[Abyssbound][Icons] Auto-Assign Equipment Slot Icons complete\n" +
                $"- scanned: {report.Scanned}\n" +
                $"- updated: {updated}\n" +
                $"- skipped: {skipped}");

            LogPathsWithLimit("[Abyssbound][Icons] Updated assets", updatedPaths, 25);
        }

        [MenuItem(MenuReportLegacyWeapons)]
        public static void ReportLegacyShopPlaceholderIcons_Weapons()
        {
            SpriteCache.Clear();
            AssetDatabase.Refresh();

            var report = ScanLegacyShopInventoryForPlaceholders(WeaponsShopInventoryPath);
            LogLegacyShopReport("LEGACY SHOP REPORT (Weapons)", report);
        }

        [MenuItem(MenuAssignLegacyWeapons)]
        public static void AutoAssignLegacyShopIcons_Weapons()
        {
            SpriteCache.Clear();
            AssetDatabase.Refresh();

            var report = ScanLegacyShopInventoryForPlaceholders(WeaponsShopInventoryPath);

            int updated = 0;
            var updatedPaths = new List<string>();
            var warnedMissingSlotSprites = new HashSet<EquipmentSlot>();

            for (int i = 0; i < report.Items.Count; i++)
            {
                var entry = report.Items[i];
                if (!entry.IsMissingOrPlaceholder)
                    continue;

                if (entry.Item == null)
                    continue;

                var desired = GetLegacyShopSprite(entry.Item, warnedMissingSlotSprites);
                if (desired == null)
                    continue;

                if (entry.Item.icon == desired)
                    continue;

                Undo.RecordObject(entry.Item, "Auto-Assign Legacy Shop Icon");
                entry.Item.icon = desired;
                EditorUtility.SetDirty(entry.Item);

                updated++;
                updatedPaths.Add(entry.AssetPath);
            }

            if (updated > 0)
                AssetDatabase.SaveAssets();

            int skipped = report.Scanned - report.MissingOrPlaceholder;
            Debug.Log(
                "[Abyssbound][Icons] Auto-Assign Legacy Shop Icons (Weapons) complete\n" +
                $"- shopInventory: {WeaponsShopInventoryPath}\n" +
                $"- scanned: {report.Scanned}\n" +
                $"- updated: {updated}\n" +
                $"- skipped: {skipped}");

            LogPathsWithLimit("[Abyssbound][Icons] Updated legacy items", updatedPaths, 25);
        }

        [MenuItem(MenuReportWeaponGear)]
        public static void ReportWeaponGearPlaceholderIcons()
        {
            SpriteCache.Clear();
            AssetDatabase.Refresh();

            var warnedMissingSlotSprites = new HashSet<EquipmentSlot>();
            var scan = ScanWeaponGearForFixes(apply: false, warnedMissingSlotSprites);
            LogWeaponGearScan("WEAPON+GEAR REPORT (Loot V2 + Legacy Shop Weapons)", scan);
        }

        [MenuItem(MenuFixWeaponGear)]
        public static void FixWeaponGearIcons_Silhouettes()
        {
            SpriteCache.Clear();
            AssetDatabase.Refresh();

            var warnedMissingSlotSprites = new HashSet<EquipmentSlot>();
            var scan = ScanWeaponGearForFixes(apply: true, warnedMissingSlotSprites);

            if (scan.UpdatedLootV2 + scan.UpdatedLegacy > 0)
                AssetDatabase.SaveAssets();

            Debug.Log(
                "[Abyssbound][Icons] Fix Weapon+Gear Icons (Silhouettes) complete\n" +
                $"- LootV2 scanned: {scan.ScannedLootV2} | updated: {scan.UpdatedLootV2} | skipped: {scan.ScannedLootV2 - scan.UpdatedLootV2}\n" +
                $"- Legacy scanned: {scan.ScannedLegacy} | updated: {scan.UpdatedLegacy} | skipped: {scan.ScannedLegacy - scan.UpdatedLegacy}\n" +
                $"- legs fallback used (TODO sil_legs needed): {scan.LegsFallbackUsedCount}");

            LogPathsWithLimit("[Abyssbound][Icons] Updated assets (Loot V2 + Legacy)", scan.UpdatedAssetPaths, 25);
        }

        [MenuItem(MenuReportEquipUiSlotMapping)]
        public static void ReportEquipmentUiSlotIconMapping()
        {
            SpriteCache.Clear();
            AssetDatabase.Refresh();

            var lines = BuildEquipmentUiSlotMappingLines(out var prefabCount);
            Debug.Log($"[Abyssbound][Icons] Equipment UI Slot Icon Mapping (prefabs={prefabCount})");
            LogPathsWithLimit("[Abyssbound][Icons] Slot mapping", lines, 50);
        }

        [MenuItem(MenuFixLegsConsistency)]
        public static void FixLegsIconConsistency_WriteSummary()
        {
            SpriteCache.Clear();
            AssetDatabase.Refresh();

            s_WarnedMissingSilLegsTodo = false;
            s_WarnedOnce.Clear();

            var summary = new System.Text.StringBuilder(2048);
            summary.AppendLine("# ABYSSBOUND — Legs Icon Fix Summary");
            summary.AppendLine();
            summary.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z");
            summary.AppendLine();
            summary.AppendLine("Goal: Legs must use a true legs silhouette everywhere; Boots must use boots silhouette; Basic Legs in merchant must not show belt silhouette.");
            summary.AppendLine();

            // A) Confirm sprites
            var silLegs = ResolveSpriteByPathOrName(SilLegsPath, "sil_legs", out var silLegsPath);
            var silBoots = ResolveSpriteByPathOrName(SilBootsPath, "sil_boots", out var silBootsPath);
            var silBelt = ResolveSpriteByPathOrName(SilBeltPath, "sil_belt", out var silBeltPath);
            var silChest = ResolveSpriteByPathOrName(SilChestPath, "sil_chest", out var silChestPath);

            summary.AppendLine("## A) Sprite availability");
            summary.AppendLine($"- sil_legs: {(silLegs != null ? silLegsPath : "NOT FOUND")}");
            summary.AppendLine($"- sil_boots: {(silBoots != null ? silBootsPath : "NOT FOUND")}");
            summary.AppendLine($"- sil_belt: {(silBelt != null ? silBeltPath : "NOT FOUND")}");
            summary.AppendLine($"- sil_chest: {(silChest != null ? silChestPath : "NOT FOUND")}");
            summary.AppendLine();

            // Capture current equipment UI mapping BEFORE changes.
            summary.AppendLine("## A2/A3) Equipment UI slot background sprites (before fix)");
            var beforeMapping = CaptureEquipmentUiSlotBackgrounds();
            if (beforeMapping.Lines.Count == 0)
            {
                summary.AppendLine("- No PlayerEquipmentUI prefabs found.");
            }
            else
            {
                for (int i = 0; i < beforeMapping.Lines.Count; i++)
                    summary.AppendLine("- " + beforeMapping.Lines[i]);
            }
            summary.AppendLine();

            var changedAssets = new List<string>();
            int legacyChanged = 0;
            int lootV2Changed = 0;
            int equipUiPrefabsChanged = 0;
            int legsFallbackUsed = 0;

            // B) Fix legacy merchant Basic Legs
            summary.AppendLine("## B) Legacy merchant item (ShopInventory_Weapons) — Basic Legs");
            if (silLegs == null)
            {
                summary.AppendLine("- WARNING: sil_legs is missing; legacy Basic Legs icon not changed.");
            }
            else
            {
                if (TryFixLegacyBasicLegsIcon(WeaponsShopInventoryPath, silLegs, out var legacyAssetPath, out var legacyReason))
                {
                    legacyChanged++;
                    changedAssets.Add(legacyAssetPath);
                    summary.AppendLine($"- UPDATED: {legacyAssetPath} ({legacyReason})");
                }
                else
                {
                    summary.AppendLine($"- No change: {legacyReason}");
                }
            }
            summary.AppendLine();

            // C) Fix Loot V2 legs items
            summary.AppendLine("## C) Loot V2 legs items (Assets/Resources/Loot/Items)" );
            if (silLegs == null)
            {
                summary.AppendLine("- WARNING: sil_legs is missing; Loot V2 Legs icons not changed.");
            }
            else
            {
                var updatedLootV2 = FixLootV2LegsIcons(silLegs, out var lootUpdatedPaths);
                lootV2Changed = updatedLootV2;
                for (int i = 0; i < lootUpdatedPaths.Count; i++)
                    changedAssets.Add(lootUpdatedPaths[i]);

                summary.AppendLine($"- Updated legs items: {updatedLootV2}");
                AppendSamplePaths(summary, lootUpdatedPaths, 10);
            }
            summary.AppendLine();

            // D) Fix equipment UI slot background mapping
            summary.AppendLine("## D) Equipment UI slot background mapping (prefabs)" );
            if (silLegs == null)
            {
                summary.AppendLine("- WARNING: sil_legs is missing; Equipment UI Legs background not updated.");
            }
            else
            {
                var res = FixEquipmentUiLegsBootsBackgrounds(silLegs, silBoots, silChest, ref legsFallbackUsed);
                equipUiPrefabsChanged = res.ChangedPrefabCount;
                for (int i = 0; i < res.ChangedPrefabs.Count; i++)
                    changedAssets.Add(res.ChangedPrefabs[i]);

                summary.AppendLine($"- Prefabs changed: {equipUiPrefabsChanged}");
                AppendSamplePaths(summary, res.ChangedPrefabs, 10);
            }
            summary.AppendLine();

            // Save assets (only once).
            if (legacyChanged + lootV2Changed + equipUiPrefabsChanged > 0)
                AssetDatabase.SaveAssets();

            // Capture mapping AFTER changes.
            summary.AppendLine("## D2) Equipment UI slot background sprites (after fix)");
            var afterMapping = CaptureEquipmentUiSlotBackgrounds();
            if (afterMapping.Lines.Count == 0)
            {
                summary.AppendLine("- No PlayerEquipmentUI prefabs found.");
            }
            else
            {
                for (int i = 0; i < afterMapping.Lines.Count; i++)
                    summary.AppendLine("- " + afterMapping.Lines[i]);
            }
            summary.AppendLine();

            summary.AppendLine("## Changes" );
            summary.AppendLine($"- Legacy Basic Legs updated: {legacyChanged}" );
            summary.AppendLine($"- Loot V2 Legs updated: {lootV2Changed}" );
            summary.AppendLine($"- Equipment UI prefabs updated: {equipUiPrefabsChanged}" );
            summary.AppendLine($"- Legs distinct fallback used (TODO sil_legs needed): {legsFallbackUsed}" );
            summary.AppendLine();

            summary.AppendLine("## QA steps" );
            summary.AppendLine("1) Open Weapons & Gear shop: Basic Legs icon should be legs silhouette.");
            summary.AppendLine("2) Open Equipment UI: Legs slot shows legs silhouette; Boots slot shows boots silhouette.");
            summary.AppendLine("3) Kill a mob and pick up legs drop (if available): inventory icon shows legs silhouette.");

            var outPath = WriteProjectRootMarkdown("ABYSSBOUND_LEGS_ICON_FIX_SUMMARY.md", summary.ToString());

            // Keep console output short.
            Debug.Log(
                "[Abyssbound][Icons] Legs icon consistency fix complete\n" +
                $"- legacyUpdated={legacyChanged} lootV2Updated={lootV2Changed} equipUiPrefabsUpdated={equipUiPrefabsChanged}\n" +
                $"- wrote {outPath}");
        }

        [MenuItem(MenuAuthoritativeLegsBootsFix)]
        public static void AuthoritativeFixLegsBootsIcons_WriteSummary()
        {
            SpriteCache.Clear();
            AssetDatabase.Refresh();

            s_WarnedMissingSilLegsTodo = false;
            s_WarnedOnce.Clear();

            var summary = new System.Text.StringBuilder(4096);
            summary.AppendLine("# ABYSSBOUND — Authoritative Legs & Boots Icon Correction");
            summary.AppendLine();
            summary.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z");
            summary.AppendLine();
            summary.AppendLine("Design intent: LEGS must ALWAYS use sil_legs; BOOTS must ALWAYS use sil_boots (regardless of current icon).");
            summary.AppendLine("Scope: item-level icons for gear only (legacy ItemDefinition + Loot V2 ItemDefinitionSO). No weapons and no other slots are modified.");
            summary.AppendLine();

            var silLegs = ResolveSpriteByPathOrName(SilLegsPath, "sil_legs", out var silLegsPath);
            var silBoots = ResolveSpriteByPathOrName(SilBootsPath, "sil_boots", out var silBootsPath);

            summary.AppendLine("## Sprite availability");
            summary.AppendLine($"- sil_legs: {(silLegs != null ? silLegsPath : "NOT FOUND")}");
            summary.AppendLine($"- sil_boots: {(silBoots != null ? silBootsPath : "NOT FOUND")}");
            summary.AppendLine();

            int legacyLegsUpdated = 0;
            int legacyBootsUpdated = 0;
            var legacyUpdatedPaths = new List<string>();

            int lootV2LegsUpdated = 0;
            int lootV2BootsUpdated = 0;
            var lootV2UpdatedPaths = new List<string>();

            // A) Legacy gear (merchant / inventory) — authoritative update
            summary.AppendLine("## A) Legacy items (Abyss.Items.ItemDefinition)");
            if (silLegs == null || silBoots == null)
            {
                summary.AppendLine("- WARNING: One or more required silhouette sprites are missing. Missing targets will be skipped.");
            }

            string[] legacyGuids;
            try { legacyGuids = AssetDatabase.FindAssets("t:ItemDefinition"); }
            catch (Exception ex)
            {
                legacyGuids = Array.Empty<string>();
                summary.AppendLine($"- ERROR: FindAssets(t:ItemDefinition) failed: {ex.Message}");
            }

            for (int i = 0; i < legacyGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(legacyGuids[i]);
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                if (item == null)
                    continue;

                EquipmentSlot slot;
                try { slot = item.equipmentSlot; }
                catch { slot = EquipmentSlot.None; }

                if (slot != EquipmentSlot.Legs && slot != EquipmentSlot.Boots)
                    continue;

                var desired = slot == EquipmentSlot.Legs ? silLegs : silBoots;
                if (desired == null)
                    continue;

                if (item.icon == desired)
                    continue;

                Undo.RecordObject(item, "Authoritative Fix Legs/Boots Icon");
                item.icon = desired;
                EditorUtility.SetDirty(item);

                legacyUpdatedPaths.Add(path);
                if (slot == EquipmentSlot.Legs) legacyLegsUpdated++;
                else legacyBootsUpdated++;
            }

            summary.AppendLine($"- Legacy Legs icons updated: {legacyLegsUpdated}");
            summary.AppendLine($"- Legacy Boots icons updated: {legacyBootsUpdated}");
            AppendSamplePaths(summary, legacyUpdatedPaths, 15);
            summary.AppendLine();

            // B) Loot V2 gear (drops, inventory, equipment) — authoritative update
            summary.AppendLine("## B) Loot V2 items (Abyssbound.Loot.ItemDefinitionSO)");
            if (silLegs == null || silBoots == null)
            {
                summary.AppendLine("- WARNING: One or more required silhouette sprites are missing. Missing targets will be skipped.");
            }

            string[] lootGuids;
            try { lootGuids = AssetDatabase.FindAssets("t:ItemDefinitionSO"); }
            catch (Exception ex)
            {
                lootGuids = Array.Empty<string>();
                summary.AppendLine($"- ERROR: FindAssets(t:ItemDefinitionSO) failed: {ex.Message}");
            }

            for (int i = 0; i < lootGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(lootGuids[i]);
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                var item = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(path);
                if (item == null)
                    continue;

                EquipmentSlot slot;
                try { slot = item.slot; }
                catch { slot = EquipmentSlot.None; }

                if (slot != EquipmentSlot.Legs && slot != EquipmentSlot.Boots)
                    continue;

                var desired = slot == EquipmentSlot.Legs ? silLegs : silBoots;
                if (desired == null)
                    continue;

                if (item.icon == desired)
                    continue;

                Undo.RecordObject(item, "Authoritative Fix Legs/Boots Icon");
                item.icon = desired;
                EditorUtility.SetDirty(item);

                lootV2UpdatedPaths.Add(path);
                if (slot == EquipmentSlot.Legs) lootV2LegsUpdated++;
                else lootV2BootsUpdated++;
            }

            summary.AppendLine($"- Loot V2 Legs icons updated: {lootV2LegsUpdated}");
            summary.AppendLine($"- Loot V2 Boots icons updated: {lootV2BootsUpdated}");
            AppendSamplePaths(summary, lootV2UpdatedPaths, 15);
            summary.AppendLine();

            // Save once.
            var totalUpdated = legacyUpdatedPaths.Count + lootV2UpdatedPaths.Count;
            if (totalUpdated > 0)
                AssetDatabase.SaveAssets();

            summary.AppendLine("## C) Auto-assign prevention");
            summary.AppendLine("- Confirmed: editor auto-assign mapping enforces hard rules:");
            summary.AppendLine("  - Legs -> sil_legs");
            summary.AppendLine("  - Boots -> sil_boots");
            summary.AppendLine();

            summary.AppendLine("## QA checklist");
            summary.AppendLine("1) Weapons & Gear shop: Basic Legs shows legs silhouette.");
            summary.AppendLine("2) Equipment UI: Legs slot item shows legs; Boots slot item shows boots.");
            summary.AppendLine("3) Inventory grid: Legs and Boots icons differ correctly.");

            var outPath = WriteProjectRootMarkdown("ABYSSBOUND_LEGS_BOOTS_AUTHORITATIVE_FIX.md", summary.ToString());

            Debug.Log(
                "[Abyssbound][Icons] Authoritative Legs+Boots icon fix complete\n" +
                $"- legacyUpdated={legacyUpdatedPaths.Count} (legs={legacyLegsUpdated}, boots={legacyBootsUpdated})\n" +
                $"- lootV2Updated={lootV2UpdatedPaths.Count} (legs={lootV2LegsUpdated}, boots={lootV2BootsUpdated})\n" +
                $"- wrote {outPath}");
        }

        [MenuItem(MenuFinalLegsFixArtAndData)]
        public static void FinalFixLegsIcon_ArtAndData_WriteSummary()
        {
            SpriteCache.Clear();
            AssetDatabase.Refresh();

            var summary = new System.Text.StringBuilder(3072);
            summary.AppendLine("# ABYSSBOUND — Final Legs Icon Fix (Art + Data)");
            summary.AppendLine();
            summary.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z");
            summary.AppendLine();
            summary.AppendLine("Context:");
            summary.AppendLine("- Merchant Basic Legs already references sil_legs.png");
            summary.AppendLine("- sil_legs.png artwork previously resembled a belt");
            summary.AppendLine("- Loot V2 Abyssal Initiate Legs previously referenced sil_boots.png");
            summary.AppendLine();

            // A) Replace sil_legs.png art in-place.
            summary.AppendLine("## A) sil_legs sprite ART");
            bool artOk = TryOverwriteSilLegsPngWithGeneratedArt(SilLegsPath, out var artMsg);
            summary.AppendLine($"- Result: {(artOk ? "UPDATED" : "FAILED")}");
            summary.AppendLine($"- Details: {artMsg}");
            summary.AppendLine();

            // B) Fix Loot V2 starter legs data.
            summary.AppendLine("## B) Loot V2 starter legs DATA");
            var silLegsSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SilLegsPath);
            if (silLegsSprite == null)
            {
                summary.AppendLine($"- FAILED: Could not load Sprite at {SilLegsPath}");
            }
            else
            {
                var starterLegs = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(StarterLegsLootV2Path);
                if (starterLegs == null)
                {
                    summary.AppendLine($"- FAILED: Could not load ItemDefinitionSO at {StarterLegsLootV2Path}");
                }
                else
                {
                    var beforeName = starterLegs.icon != null ? SafeName(starterLegs.icon) : "(null)";
                    var beforePath = starterLegs.icon != null ? SafeAssetPath(starterLegs.icon) : "(null)";

                    if (starterLegs.icon != silLegsSprite)
                    {
                        Undo.RecordObject(starterLegs, "Final Fix Starter Legs Icon");
                        starterLegs.icon = silLegsSprite;
                        EditorUtility.SetDirty(starterLegs);
                        summary.AppendLine("- UPDATED: Item_Starter_Legs.icon");
                    }
                    else
                    {
                        summary.AppendLine("- No change: Item_Starter_Legs.icon already references sil_legs");
                    }

                    summary.AppendLine($"- Asset: {StarterLegsLootV2Path}");
                    summary.AppendLine($"- Before: {beforeName} ({beforePath})");
                    summary.AppendLine($"- After:  {SafeName(starterLegs.icon)} ({SafeAssetPath(starterLegs.icon)})");
                }
            }
            summary.AppendLine();

            // C) Safety / refresh
            summary.AppendLine("## C) Refresh\n- AssetDatabase.SaveAssets() + Refresh() executed.");
            summary.AppendLine();
            summary.AppendLine("## Reminder");
            summary.AppendLine("- Restart Play Mode (and ensure Domain Reload is enabled) so LootRegistryRuntime rebuilds and picks up updated base item icons.");
            summary.AppendLine();

            summary.AppendLine("## QA checklist");
            summary.AppendLine("1) Weapons & Gear shop: Basic Legs shows legs silhouette (not belt).");
            summary.AppendLine("2) Equipment UI: Abyssal Initiate Legs shows legs silhouette.");
            summary.AppendLine("3) Boots still show boots silhouette.");

            try { AssetDatabase.SaveAssets(); } catch { }
            try { AssetDatabase.Refresh(); } catch { }

            var outPath = WriteProjectRootMarkdown("ABYSSBOUND_LEGS_FINAL_FIX_SUMMARY.md", summary.ToString());

            Debug.Log(
                "[Abyssbound][Icons] Final Legs icon fix complete\n" +
                $"- sil_legs art updated: {artOk}\n" +
                $"- starter legs icon updated: {(AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(StarterLegsLootV2Path)?.icon == AssetDatabase.LoadAssetAtPath<Sprite>(SilLegsPath))}\n" +
                $"- wrote {outPath}");
        }

        [MenuItem(MenuFixStarterLegsIcon)]
        public static void FixLootV2StarterLegsIcon_WriteSummary()
        {
            SpriteCache.Clear();
            AssetDatabase.Refresh();

            var sb = new System.Text.StringBuilder(1024);
            sb.AppendLine("# ABYSSBOUND — Starter Legs Icon Fix (Loot V2)");
            sb.AppendLine();
            sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z");
            sb.AppendLine();

            var silLegs = AssetDatabase.LoadAssetAtPath<Sprite>(SilLegsPath);
            var starterLegs = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(StarterLegsLootV2Path);

            if (silLegs == null)
            {
                sb.AppendLine($"- FAILED: Could not load Sprite at {SilLegsPath}");
            }
            else if (starterLegs == null)
            {
                sb.AppendLine($"- FAILED: Could not load ItemDefinitionSO at {StarterLegsLootV2Path}");
            }
            else
            {
                var beforeName = starterLegs.icon != null ? SafeName(starterLegs.icon) : "(null)";
                var beforePath = starterLegs.icon != null ? SafeAssetPath(starterLegs.icon) : "(null)";

                bool changed = starterLegs.icon != silLegs;
                if (changed)
                {
                    Undo.RecordObject(starterLegs, "Fix Starter Legs Icon");
                    starterLegs.icon = silLegs;
                    EditorUtility.SetDirty(starterLegs);
                }

                try { AssetDatabase.SaveAssets(); } catch { }
                try { AssetDatabase.Refresh(); } catch { }

                sb.AppendLine($"- Target asset: {StarterLegsLootV2Path}");
                sb.AppendLine($"- Before: {beforeName} ({beforePath})");
                sb.AppendLine($"- After:  {SafeName(starterLegs.icon)} ({SafeAssetPath(starterLegs.icon)})");
                sb.AppendLine($"- Updated: {changed}");
                sb.AppendLine();
                sb.AppendLine("Reminder: restart Play Mode (Domain Reload enabled) so LootRegistryRuntime rebuilds and picks up updated base item icons.");
            }

            var outPath = WriteProjectRootMarkdown("ABYSSBOUND_STARTER_LEGS_ICON_FIX.md", sb.ToString());

            Debug.Log(
                "[Abyssbound][Icons] Starter Legs icon fix complete\n" +
                $"- wrote {outPath}");
        }

        [MenuItem(MenuVerifyStarterSetIcons)]
        public static void VerifyStarterSetIcons()
        {
            SpriteCache.Clear();
            AssetDatabase.Refresh();

            var lines = new List<string>(8);
            VerifyOneStarter(lines, "Helm", StarterHelmLootV2Path);
            VerifyOneStarter(lines, "Chest", StarterChestLootV2Path);
            VerifyOneStarter(lines, "Legs", StarterLegsLootV2Path);

            var msg = "[Abyssbound][Icons] Starter Set Icons:\n";
            if (lines.Count == 0)
                msg += "- (no starter set assets found)";
            else
                for (int i = 0; i < lines.Count; i++) msg += "- " + lines[i] + "\n";

            Debug.Log(msg);
        }

        private static void VerifyOneStarter(List<string> lines, string label, string path)
        {
            if (lines == null) return;
            if (string.IsNullOrWhiteSpace(path)) return;

            var so = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(path);
            if (so == null)
            {
                lines.Add($"{label}: MISSING asset at {path}");
                return;
            }

            var icon = so.icon;
            var iconName = icon != null ? SafeName(icon) : "(null)";
            var iconPath = icon != null ? SafeAssetPath(icon) : "(null)";
            lines.Add($"{label}: {so.displayName} | id={so.id} | slot={so.slot} | icon={iconName} ({iconPath})");
        }

        private struct TextureImporterSnapshot
        {
            public TextureImporterType TextureType;
            public SpriteImportMode SpriteImportMode;
            public bool AlphaIsTransparency;
            public bool IsReadable;
            public TextureImporterCompression Compression;
            public FilterMode FilterMode;
            public TextureWrapMode WrapMode;
            public int MaxTextureSize;
            public bool MipmapEnabled;

            public static TextureImporterSnapshot Capture(TextureImporter importer)
            {
                return new TextureImporterSnapshot
                {
                    TextureType = importer.textureType,
                    SpriteImportMode = importer.spriteImportMode,
                    AlphaIsTransparency = importer.alphaIsTransparency,
                    IsReadable = importer.isReadable,
                    Compression = importer.textureCompression,
                    FilterMode = importer.filterMode,
                    WrapMode = importer.wrapMode,
                    MaxTextureSize = importer.maxTextureSize,
                    MipmapEnabled = importer.mipmapEnabled,
                };
            }

            public void Restore(TextureImporter importer)
            {
                importer.textureType = TextureType;
                importer.spriteImportMode = SpriteImportMode;
                importer.alphaIsTransparency = AlphaIsTransparency;
                importer.isReadable = IsReadable;
                importer.textureCompression = Compression;
                importer.filterMode = FilterMode;
                importer.wrapMode = WrapMode;
                importer.maxTextureSize = MaxTextureSize;
                importer.mipmapEnabled = MipmapEnabled;
            }
        }

        private static bool TryOverwriteSilLegsPngWithGeneratedArt(string assetPath, out string message)
        {
            message = null;
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                message = "Missing asset path.";
                return false;
            }

            if (!File.Exists(GetAbsoluteProjectPath(assetPath)))
            {
                message = $"File not found on disk: {assetPath}";
                return false;
            }

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                message = $"No TextureImporter for {assetPath}";
                return false;
            }

            var snap = TextureImporterSnapshot.Capture(importer);

            try
            {
                // Ensure Sprite import and readable so we can inspect size.
                bool changedImporter = false;
                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.alphaIsTransparency = true;
                    changedImporter = true;
                }

                if (!importer.isReadable)
                {
                    importer.isReadable = true;
                    changedImporter = true;
                }

                if (changedImporter)
                    importer.SaveAndReimport();

                var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                int w = existing != null ? existing.width : 64;
                int h = existing != null ? existing.height : 64;
                w = Mathf.Clamp(w, 16, 1024);
                h = Mathf.Clamp(h, 16, 1024);

                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;

                var pixels = new Color32[w * h];
                // Default transparent.
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = new Color32(0, 0, 0, 0);

                // Generate simple pants/legs silhouette (white fill, transparent background).
                var white = new Color32(255, 255, 255, 255);

                for (int y = 0; y < h; y++)
                {
                    float v = (y + 0.5f) / h;

                    // Waistband block.
                    bool inWaist = v >= 0.72f && v <= 0.84f;

                    for (int x = 0; x < w; x++)
                    {
                        float u = (x + 0.5f) / w;

                        bool on = false;

                        if (inWaist)
                        {
                            on = u >= 0.28f && u <= 0.72f;
                        }
                        else if (v >= 0.16f && v < 0.72f)
                        {
                            // Two legs: slightly wider near the bottom.
                            float t = Mathf.InverseLerp(0.72f, 0.16f, v); // 0 at top, 1 at bottom
                            float halfW = Mathf.Lerp(0.075f, 0.095f, t);

                            // Left leg
                            float leftCenter = 0.37f;
                            bool left = Mathf.Abs(u - leftCenter) <= halfW;
                            // Right leg
                            float rightCenter = 0.63f;
                            bool right = Mathf.Abs(u - rightCenter) <= halfW;

                            // Keep a visible gap between legs.
                            bool inGap = u > 0.48f && u < 0.52f && v < 0.70f;
                            on = (left || right) && !inGap;

                            // Slight inner crotch notch near top.
                            if (v > 0.64f && v < 0.72f)
                            {
                                if (u > 0.44f && u < 0.56f)
                                    on = false;
                            }
                        }

                        if (on)
                            pixels[y * w + x] = white;
                    }
                }

                tex.SetPixels32(pixels);
                tex.Apply(false, false);

                var pngBytes = tex.EncodeToPNG();
                if (pngBytes == null || pngBytes.Length == 0)
                {
                    message = "EncodeToPNG returned empty bytes.";
                    return false;
                }

                File.WriteAllBytes(GetAbsoluteProjectPath(assetPath), pngBytes);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

                // Restore importer settings if we changed them.
                var importer2 = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer2 != null)
                {
                    snap.Restore(importer2);
                    importer2.SaveAndReimport();
                }

                message = $"Replaced pixel content (generated pants silhouette) at {assetPath} ({w}x{h}).";
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        private static string GetAbsoluteProjectPath(string assetPath)
        {
            string projectRoot = null;
            try { projectRoot = Directory.GetParent(Application.dataPath)?.FullName; }
            catch { projectRoot = null; }

            if (string.IsNullOrWhiteSpace(projectRoot))
                projectRoot = Environment.CurrentDirectory;

            var p = Path.Combine(projectRoot, assetPath);
            return Path.GetFullPath(p);
        }

        private sealed class EquipmentUiMappingCapture
        {
            public readonly List<string> Lines = new();
        }

        private static EquipmentUiMappingCapture CaptureEquipmentUiSlotBackgrounds()
        {
            var cap = new EquipmentUiMappingCapture();
            var prefabs = FindPrefabsWithComponent<PlayerEquipmentUI>();
            for (int i = 0; i < prefabs.Count; i++)
            {
                var path = prefabs[i];
                var mapping = CaptureOneEquipmentUiPrefab(path);
                if (mapping == null) continue;

                mapping.TryGetValue(EquipmentSlot.Legs, out var legs);
                mapping.TryGetValue(EquipmentSlot.Boots, out var boots);
                mapping.TryGetValue(EquipmentSlot.Belt, out var belt);

                cap.Lines.Add($"{path} | Legs={legs ?? "(none)"} | Boots={boots ?? "(none)"} | Belt={belt ?? "(none)"}");
            }

            return cap;
        }

        private static Dictionary<EquipmentSlot, string> CaptureOneEquipmentUiPrefab(string prefabAssetPath)
        {
            if (string.IsNullOrWhiteSpace(prefabAssetPath))
                return null;

            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(prefabAssetPath);
                if (root == null) return null;

                var ui = root.GetComponentInChildren<PlayerEquipmentUI>(true);
                if (ui == null) return null;

                var so = new SerializedObject(ui);
                var slotsProp = so.FindProperty("slots");
                if (slotsProp == null || !slotsProp.isArray) return null;

                var map = new Dictionary<EquipmentSlot, string>();

                for (int i = 0; i < slotsProp.arraySize; i++)
                {
                    var elem = slotsProp.GetArrayElementAtIndex(i);
                    if (elem == null) continue;

                    var slotProp = elem.FindPropertyRelative("slot");
                    var emptyIconProp = elem.FindPropertyRelative("emptyIcon");
                    if (slotProp == null || emptyIconProp == null) continue;

                    var slot = (EquipmentSlot)slotProp.enumValueIndex;
                    var sprite = emptyIconProp.objectReferenceValue as Sprite;
                    map[slot] = sprite != null ? SafeAssetPath(sprite) : null;
                }

                return map;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (root != null)
                    PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private sealed class EquipUiFixResult
        {
            public int ChangedPrefabCount;
            public readonly List<string> ChangedPrefabs = new();
        }

        private static EquipUiFixResult FixEquipmentUiLegsBootsBackgrounds(Sprite silLegs, Sprite silBoots, Sprite silChest, ref int legsFallbackUsed)
        {
            var res = new EquipUiFixResult();
            var prefabs = FindPrefabsWithComponent<PlayerEquipmentUI>();

            for (int p = 0; p < prefabs.Count; p++)
            {
                var prefabPath = prefabs[p];
                GameObject root = null;
                bool changed = false;

                try
                {
                    root = PrefabUtility.LoadPrefabContents(prefabPath);
                    if (root == null) continue;

                    var ui = root.GetComponentInChildren<PlayerEquipmentUI>(true);
                    if (ui == null) continue;

                    var so = new SerializedObject(ui);
                    var slotsProp = so.FindProperty("slots");
                    if (slotsProp == null || !slotsProp.isArray) continue;

                    Sprite oldLegs = null;
                    Sprite oldBoots = null;

                    // First pass: capture old.
                    for (int i = 0; i < slotsProp.arraySize; i++)
                    {
                        var elem = slotsProp.GetArrayElementAtIndex(i);
                        if (elem == null) continue;
                        var slotProp = elem.FindPropertyRelative("slot");
                        var emptyIconProp = elem.FindPropertyRelative("emptyIcon");
                        if (slotProp == null || emptyIconProp == null) continue;

                        var slot = (EquipmentSlot)slotProp.enumValueIndex;
                        var sprite = emptyIconProp.objectReferenceValue as Sprite;
                        if (slot == EquipmentSlot.Legs) oldLegs = sprite;
                        if (slot == EquipmentSlot.Boots) oldBoots = sprite;
                    }

                    // Determine boots target: prefer the sprite currently (incorrectly) used for legs if it looks like boots silhouette.
                    Sprite bootsTarget = silBoots;
                    if (oldLegs != null && oldLegs != silLegs)
                    {
                        var oldLegsPath = SafeAssetPath(oldLegs);
                        var oldLegsName = SafeName(oldLegs);
                        if (ContainsAnyNeedle(oldLegsPath, "sil_boots") || ContainsAnyNeedle(oldLegsName, "sil_boots"))
                            bootsTarget = oldLegs;
                    }

                    if (bootsTarget == null)
                    {
                        // Best-effort: keep existing boots, else chest.
                        bootsTarget = oldBoots != null ? oldBoots : silChest;
                    }

                    // Second pass: apply.
                    for (int i = 0; i < slotsProp.arraySize; i++)
                    {
                        var elem = slotsProp.GetArrayElementAtIndex(i);
                        if (elem == null) continue;
                        var slotProp = elem.FindPropertyRelative("slot");
                        var emptyIconProp = elem.FindPropertyRelative("emptyIcon");
                        if (slotProp == null || emptyIconProp == null) continue;

                        var slot = (EquipmentSlot)slotProp.enumValueIndex;
                        if (slot == EquipmentSlot.Legs)
                        {
                            if (emptyIconProp.objectReferenceValue != silLegs)
                            {
                                emptyIconProp.objectReferenceValue = silLegs;
                                changed = true;
                            }
                        }
                        else if (slot == EquipmentSlot.Boots)
                        {
                            if (bootsTarget != null && emptyIconProp.objectReferenceValue != bootsTarget)
                            {
                                emptyIconProp.objectReferenceValue = bootsTarget;
                                changed = true;
                            }
                        }
                    }

                    if (changed)
                    {
                        so.ApplyModifiedPropertiesWithoutUndo();
                        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                        res.ChangedPrefabCount++;
                        res.ChangedPrefabs.Add(prefabPath);
                    }
                }
                catch
                {
                    // ignore
                }
                finally
                {
                    if (root != null)
                        PrefabUtility.UnloadPrefabContents(root);
                }
            }

            // If sil_legs was missing, the caller won't invoke this.
            // Track fallback usage only when we could not set legs to sil_legs (not expected here).
            return res;
        }

        private static bool TryFixLegacyBasicLegsIcon(string shopInventoryAssetPath, Sprite silLegs, out string legacyAssetPath, out string reason)
        {
            legacyAssetPath = null;
            reason = "(unknown)";

            if (silLegs == null)
            {
                reason = "sil_legs missing";
                return false;
            }

            var inventory = AssetDatabase.LoadAssetAtPath<ShopInventory>(shopInventoryAssetPath);
            if (inventory == null)
            {
                reason = $"ShopInventory not found at {shopInventoryAssetPath}";
                return false;
            }

            ItemDefinition target = null;
            try
            {
                if (inventory.entries != null)
                {
                    for (int i = 0; i < inventory.entries.Count; i++)
                    {
                        var e = inventory.entries[i];
                        if (e == null || e.item == null) continue;

                        var it = e.item;
                        var id = string.Empty;
                        var dn = string.Empty;
                        try { id = it.itemId ?? string.Empty; } catch { id = string.Empty; }
                        try { dn = it.displayName ?? string.Empty; } catch { dn = string.Empty; }

                        if (string.Equals(id, "armor_basic_legs", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(dn, "Basic Legs", StringComparison.OrdinalIgnoreCase) ||
                            (id.IndexOf("basic_legs", StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (dn.IndexOf("basic legs", StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            target = it;
                            break;
                        }
                    }
                }
            }
            catch { }

            if (target == null)
            {
                reason = "Basic Legs item not found in ShopInventory_Weapons";
                return false;
            }

            legacyAssetPath = AssetDatabase.GetAssetPath(target);
            var current = target.icon;
            if (!IsWrongLegsIcon(current, silLegs))
            {
                reason = "icon is not a placeholder/wrong silhouette";
                return false;
            }

            Undo.RecordObject(target, "Fix Basic Legs Icon");
            target.icon = silLegs;
            EditorUtility.SetDirty(target);
            reason = $"icon set to {SafeName(silLegs)}";
            return true;
        }

        private static int FixLootV2LegsIcons(Sprite silLegs, out List<string> updatedPaths)
        {
            updatedPaths = new List<string>();
            if (silLegs == null) return 0;

            var report = ScanLootV2Equipment();
            int updated = 0;

            for (int i = 0; i < report.Items.Count; i++)
            {
                var e = report.Items[i];
                if (e == null || e.Item == null) continue;
                if (e.Slot != EquipmentSlot.Legs) continue;

                var current = e.Item.icon;

                // Only overwrite placeholder/wrong silhouette (belt/chest/boots/any silhouette) or explicit needles.
                if (!IsWrongLegsIcon(current, silLegs))
                    continue;

                if (current != null && !IsSilhouetteSprite(current) && !ContainsAnyNeedle(SafeAssetPath(current), "placeholder", "letter", "tmp") && !ContainsAnyNeedle(SafeName(current), "placeholder", "letter", "tmp"))
                {
                    // Custom icon: keep.
                    continue;
                }

                if (e.Item.icon == silLegs)
                    continue;

                Undo.RecordObject(e.Item, "Fix Loot V2 Legs Icon");
                e.Item.icon = silLegs;
                EditorUtility.SetDirty(e.Item);
                updated++;
                updatedPaths.Add(e.AssetPath);
            }

            return updated;
        }

        private static bool IsWrongLegsIcon(Sprite current, Sprite silLegs)
        {
            if (current == null)
                return true;

            if (silLegs != null && current == silLegs)
                return false;

            var name = SafeName(current);
            var path = SafeAssetPath(current);

            // Explicit placeholder needles.
            if (ContainsAnyNeedle(name, "placeholder", "letter", "tmp") || ContainsAnyNeedle(path, "placeholder", "letter", "tmp"))
                return true;

            // Legs must not use belt/chest/boots silhouettes.
            if (ContainsAnyNeedle(name, "sil_belt", "sil_chest", "sil_boots") || ContainsAnyNeedle(path, "sil_belt", "sil_chest", "sil_boots"))
                return true;

            // If it's a silhouette but not legs, treat as wrong for legs.
            if (IsSilhouetteSprite(current))
                return true;

            return false;
        }

        private static Sprite ResolveSpriteByPathOrName(string preferredPath, string nameToken, out string resolvedPath)
        {
            resolvedPath = null;

            Sprite s = null;
            if (!string.IsNullOrWhiteSpace(preferredPath))
            {
                s = AssetDatabase.LoadAssetAtPath<Sprite>(preferredPath);
                if (s != null)
                {
                    resolvedPath = preferredPath;
                    return s;
                }
            }

            try
            {
                var guids = AssetDatabase.FindAssets($"{nameToken} t:Sprite");
                if (guids != null && guids.Length > 0)
                {
                    for (int i = 0; i < guids.Length; i++)
                    {
                        var p = AssetDatabase.GUIDToAssetPath(guids[i]);
                        if (string.IsNullOrWhiteSpace(p)) continue;
                        var spr = AssetDatabase.LoadAssetAtPath<Sprite>(p);
                        if (spr == null) continue;

                        if (string.Equals(spr.name, nameToken, StringComparison.OrdinalIgnoreCase) ||
                            p.IndexOf("/" + nameToken + ".", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            resolvedPath = p;
                            return spr;
                        }

                        // Keep first candidate.
                        if (s == null)
                        {
                            s = spr;
                            resolvedPath = p;
                        }
                    }
                }
            }
            catch { }

            return s;
        }

        private static string WriteProjectRootMarkdown(string fileName, string contents)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = "ABYSSBOUND_LEGS_ICON_FIX_SUMMARY.md";

            string projectRoot = null;
            try
            {
                // Application.dataPath ends with /Assets
                projectRoot = System.IO.Directory.GetParent(Application.dataPath)?.FullName;
            }
            catch { projectRoot = null; }

            if (string.IsNullOrWhiteSpace(projectRoot))
                projectRoot = Environment.CurrentDirectory;

            var path = Path.Combine(projectRoot, fileName);
            try
            {
                File.WriteAllText(path, contents ?? string.Empty);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Abyssbound][Icons] Failed to write summary file '{path}': {ex.Message}");
            }

            try { AssetDatabase.Refresh(); } catch { }
            return path.Replace('\\', '/');
        }

        private static void AppendSamplePaths(System.Text.StringBuilder sb, List<string> paths, int max)
        {
            if (sb == null || paths == null) return;
            int take = Mathf.Clamp(max, 0, paths.Count);
            if (take <= 0) return;

            sb.AppendLine("- Sample updated assets:");
            for (int i = 0; i < take; i++)
                sb.AppendLine("  - " + paths[i]);
        }

        private static List<string> FindPrefabsWithComponent<T>() where T : Component
        {
            var paths = new List<string>();
            try
            {
                var guids = AssetDatabase.FindAssets("t:Prefab");
                for (int i = 0; i < guids.Length; i++)
                {
                    var p = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (string.IsNullOrWhiteSpace(p)) continue;

                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                    if (go == null) continue;
                    if (go.GetComponentInChildren<T>(true) == null) continue;
                    paths.Add(p);
                }
            }
            catch { }

            paths.Sort(StringComparer.OrdinalIgnoreCase);
            return paths;
        }

        private static List<string> BuildEquipmentUiSlotMappingLines(out int prefabCount)
        {
            prefabCount = 0;
            var lines = new List<string>();
            var prefabs = FindPrefabsWithComponent<PlayerEquipmentUI>();
            prefabCount = prefabs.Count;

            for (int i = 0; i < prefabs.Count; i++)
            {
                var prefabPath = prefabs[i];
                var map = CaptureOneEquipmentUiPrefab(prefabPath);
                if (map == null) continue;

                foreach (var kv in map)
                {
                    lines.Add($"{prefabPath} | {kv.Key} -> {kv.Value ?? "(none)"}");
                }
            }

            return lines;
        }

        private sealed class WeaponGearFixScan
        {
            public int ScannedLootV2;
            public int ScannedLegacy;
            public int CandidatesLootV2;
            public int CandidatesLegacy;
            public int UpdatedLootV2;
            public int UpdatedLegacy;
            public int LegsFallbackUsedCount;
            public readonly List<string> CandidateLines = new();
            public readonly List<string> UpdatedAssetPaths = new();
        }

        private static void LogWeaponGearScan(string title, WeaponGearFixScan scan)
        {
            Debug.Log(
                $"[Abyssbound][Icons] {title}\n" +
                $"- LootV2 scanned: {scan.ScannedLootV2} | candidates: {scan.CandidatesLootV2}\n" +
                $"- Legacy scanned: {scan.ScannedLegacy} | candidates: {scan.CandidatesLegacy}\n" +
                $"- legs fallback needed (TODO sil_legs): {scan.LegsFallbackUsedCount}");

            if (scan.CandidateLines == null || scan.CandidateLines.Count == 0)
            {
                Debug.Log("[Abyssbound][Icons] Weapon+Gear candidates: (none)");
                return;
            }

            int take = Mathf.Clamp(25, 0, scan.CandidateLines.Count);
            var msg = "[Abyssbound][Icons] Weapon+Gear candidates (first 25):\n";
            for (int i = 0; i < take; i++)
                msg += "- " + scan.CandidateLines[i] + "\n";

            if (scan.CandidateLines.Count > take)
                msg += $"... and {scan.CandidateLines.Count - take} more\n";

            Debug.Log(msg);
        }

        private static WeaponGearFixScan ScanWeaponGearForFixes(bool apply, HashSet<EquipmentSlot> warnedMissingSlotSprites)
        {
            var scan = new WeaponGearFixScan();

            // Preload silhouettes we may conditionally use.
            var silSword = LoadSprite(SilSwordPath);
            var silBow = LoadSprite(SilBowPath);
            var silStaff = LoadSprite(SilStaffPath);
            var sil2h = LoadSprite(Sil2HPath);
            var silOrb = LoadSprite(SilOrbPath);
            var silLegs = LoadSprite(SilLegsPath);
            var silBoots = LoadSprite(SilBootsPath);
            var silChest = LoadSprite(SilChestPath);

            // Loot V2 scope A
            var lootV2 = ScanLootV2Equipment();
            for (int i = 0; i < lootV2.Items.Count; i++)
            {
                var e = lootV2.Items[i];
                if (e == null || e.Item == null) continue;

                scan.ScannedLootV2++;

                var desired = GetWeaponGearDesiredSprite_LootV2(e.Item, e.Slot, e.OccupiesBothHands, silSword, silBow, silStaff, sil2h, silOrb, silLegs, silBoots, silChest, warnedMissingSlotSprites, ref scan.LegsFallbackUsedCount);
                if (desired == null)
                    continue;

                if (!ShouldReplaceWeaponGearIcon(e.CurrentIcon, desired, e.Slot, silLegs))
                    continue;

                scan.CandidatesLootV2++;
                scan.CandidateLines.Add($"[LootV2] {e.AssetPath} -> {SafeName(desired)}");

                if (!apply)
                    continue;

                if (e.Item.icon == desired)
                    continue;

                Undo.RecordObject(e.Item, "Fix Weapon+Gear Icon");
                e.Item.icon = desired;
                EditorUtility.SetDirty(e.Item);
                scan.UpdatedLootV2++;
                scan.UpdatedAssetPaths.Add(e.AssetPath);
            }

            // Legacy weapons shop scope B
            var legacy = ScanLegacyShopInventoryForPlaceholders(WeaponsShopInventoryPath);
            for (int i = 0; i < legacy.Items.Count; i++)
            {
                var e = legacy.Items[i];
                if (e == null || e.Item == null) continue;

                scan.ScannedLegacy++;

                var desired = GetWeaponGearDesiredSprite_Legacy(e.Item, e.Slot, silSword, silBow, silStaff, sil2h, silOrb, silLegs, silBoots, silChest, warnedMissingSlotSprites, ref scan.LegsFallbackUsedCount);
                if (desired == null)
                    continue;

                if (!ShouldReplaceWeaponGearIcon(e.CurrentIcon, desired, e.Slot, silLegs))
                    continue;

                scan.CandidatesLegacy++;
                scan.CandidateLines.Add($"[Legacy] {e.AssetPath} -> {SafeName(desired)}");

                if (!apply)
                    continue;

                if (e.Item.icon == desired)
                    continue;

                Undo.RecordObject(e.Item, "Fix Weapon+Gear Icon");
                e.Item.icon = desired;
                EditorUtility.SetDirty(e.Item);
                scan.UpdatedLegacy++;
                scan.UpdatedAssetPaths.Add(e.AssetPath);
            }

            return scan;
        }

        private static bool ShouldReplaceWeaponGearIcon(Sprite current, Sprite desired, EquipmentSlot slot, Sprite silLegs)
        {
            if (desired == null)
                return false;

            if (current == desired)
                return false;

            if (current == null)
                return true;

            string name = SafeName(current);
            string path = SafeAssetPath(current);

            if (ContainsAnyNeedle(name, "placeholder", "letter", "tmp") || ContainsAnyNeedle(path, "placeholder", "letter", "tmp"))
                return true;

            if (LooksLikeGeneratedBasicIcon(path, name))
                return true;

            // Correct wrong silhouette assignments (including legs mis-assigned to belt/chest/etc).
            if (IsSilhouetteSprite(current))
            {
                // If we have sil_legs, enforce legs to use it for silhouette placeholders.
                if (slot == EquipmentSlot.Legs && silLegs != null)
                    return current != silLegs;

                return current != desired;
            }

            // Non-placeholder custom icon: keep.
            return false;
        }

        private static bool ContainsAnyNeedle(string value, params string[] needles)
        {
            if (string.IsNullOrWhiteSpace(value) || needles == null || needles.Length == 0)
                return false;

            var lower = value.ToLowerInvariant();
            for (int i = 0; i < needles.Length; i++)
            {
                var n = needles[i];
                if (string.IsNullOrWhiteSpace(n)) continue;
                if (lower.Contains(n)) return true;
            }

            return false;
        }

        private static bool LooksLikeGeneratedBasicIcon(string assetPath, string spriteName)
        {
            if (!string.IsNullOrWhiteSpace(assetPath) && assetPath.Replace('\\', '/').IndexOf("Assets/UI/Generated/Icons/", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (!string.IsNullOrWhiteSpace(spriteName) && spriteName.StartsWith("Icon_", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private static bool IsSilhouetteSprite(Sprite sprite)
        {
            if (sprite == null)
                return false;

            var name = SafeName(sprite);
            if (StartsWithSilhouettePrefix(name))
                return true;

            var path = SafeAssetPath(sprite);
            return ContainsSilhouetteSegment(path);
        }

        private static string SafeName(UnityEngine.Object obj)
        {
            try { return obj != null ? obj.name : null; }
            catch { return null; }
        }

        private static string SafeAssetPath(UnityEngine.Object obj)
        {
            try { return obj != null ? AssetDatabase.GetAssetPath(obj) : null; }
            catch { return null; }
        }

        private static Sprite GetWeaponGearDesiredSprite_LootV2(
            ItemDefinitionSO item,
            EquipmentSlot slot,
            bool occupiesBothHands,
            Sprite silSword,
            Sprite silBow,
            Sprite silStaff,
            Sprite sil2h,
            Sprite silOrb,
            Sprite silLegs,
            Sprite silBoots,
            Sprite silChest,
            HashSet<EquipmentSlot> warnedMissingSlotSprites,
            ref int legsFallbackUsedCount)
        {
            if (item == null)
                return null;

            // Weapon mapping applies to hand slots.
            if (slot == EquipmentSlot.RightHand)
            {
                var kind = GuessWeaponKind(item.id, item.displayName, item.name);

                if (kind == WeaponKind.Bow)
                    return silBow != null ? silBow : silSword;

                if (kind == WeaponKind.Staff)
                    return silStaff != null ? silStaff : (silOrb != null ? silOrb : silSword);

                // Optional 2H icon for generic melee (only if not bow/staff).
                if (occupiesBothHands && sil2h != null)
                    return sil2h;

                return silSword;
            }

            // Slot-based mapping for armor/accessories.
            if (slot == EquipmentSlot.Legs)
                return LoadSpriteRequired(SilLegsPath, EquipmentSlot.Legs, warnedMissingSlotSprites);

            if (slot == EquipmentSlot.LeftHand)
                return LoadSpriteRequired(SilShieldPath, EquipmentSlot.LeftHand, warnedMissingSlotSprites);

            switch (slot)
            {
                case EquipmentSlot.Helm: return LoadSprite(SilHelmPath);
                case EquipmentSlot.Chest: return LoadSprite(SilChestPath);
                case EquipmentSlot.Gloves: return LoadSprite(SilGlovesPath);
                case EquipmentSlot.Boots: return LoadSpriteRequired(SilBootsPath, EquipmentSlot.Boots, warnedMissingSlotSprites);
                case EquipmentSlot.Belt: return LoadSprite(SilBeltPath);
                case EquipmentSlot.Cape: return LoadSprite(SilCapePath);
                case EquipmentSlot.Ammo: return LoadSprite(SilArrowsPath);
                case EquipmentSlot.Amulet: return LoadSprite(SilAmuletPath);
                case EquipmentSlot.Ring1:
                case EquipmentSlot.Ring2:
                    return LoadSprite(SilRingPath);
                case EquipmentSlot.Artifact:
                    return LoadSprite(SilOrbPath);
                default:
                    return null;
            }
        }

        private static Sprite GetWeaponGearDesiredSprite_Legacy(
            ItemDefinition item,
            EquipmentSlot slot,
            Sprite silSword,
            Sprite silBow,
            Sprite silStaff,
            Sprite sil2h,
            Sprite silOrb,
            Sprite silLegs,
            Sprite silBoots,
            Sprite silChest,
            HashSet<EquipmentSlot> warnedMissingSlotSprites,
            ref int legsFallbackUsedCount)
        {
            if (item == null)
                return null;

            if (slot == EquipmentSlot.RightHand)
            {
                var kind = GuessWeaponKind(item.itemId, item.displayName, item.name);

                if (kind == WeaponKind.Bow)
                    return silBow != null ? silBow : silSword;

                if (kind == WeaponKind.Staff)
                    return silStaff != null ? silStaff : (silOrb != null ? silOrb : silSword);

                bool isTwoHanded = false;
                try { isTwoHanded = item.weaponHandedness == WeaponHandedness.TwoHanded; } catch { isTwoHanded = false; }
                if (isTwoHanded && sil2h != null)
                    return sil2h;

                return silSword;
            }

            if (slot == EquipmentSlot.Legs)
                return LoadSpriteRequired(SilLegsPath, EquipmentSlot.Legs, warnedMissingSlotSprites);

            if (slot == EquipmentSlot.LeftHand)
                return LoadSpriteRequired(SilShieldPath, EquipmentSlot.LeftHand, warnedMissingSlotSprites);

            switch (slot)
            {
                case EquipmentSlot.Helm: return LoadSprite(SilHelmPath);
                case EquipmentSlot.Chest: return LoadSprite(SilChestPath);
                case EquipmentSlot.Gloves: return LoadSprite(SilGlovesPath);
                case EquipmentSlot.Boots: return LoadSpriteRequired(SilBootsPath, EquipmentSlot.Boots, warnedMissingSlotSprites);
                case EquipmentSlot.Belt: return LoadSprite(SilBeltPath);
                case EquipmentSlot.Cape: return LoadSprite(SilCapePath);
                case EquipmentSlot.Ammo: return LoadSprite(SilArrowsPath);
                case EquipmentSlot.Amulet: return LoadSprite(SilAmuletPath);
                case EquipmentSlot.Ring1:
                case EquipmentSlot.Ring2:
                    return LoadSprite(SilRingPath);
                case EquipmentSlot.Artifact:
                    return LoadSprite(SilOrbPath);
                default:
                    return null;
            }
        }

        private static Sprite ResolveLegsSilhouetteOrFallback(Sprite silLegs, Sprite silBoots, Sprite silChest, ref int legsFallbackUsedCount)
        {
            if (silLegs != null)
                return silLegs;

            // Enforce distinct fallback (NOT belt). Prefer boots silhouette over chest.
            legsFallbackUsedCount++;

            if (!s_WarnedMissingSilLegsTodo)
            {
                s_WarnedMissingSilLegsTodo = true;
                Debug.LogWarning("[Abyssbound][Icons] TODO: Missing sil_legs. Using a distinct fallback for Legs (not belt). Add Assets/Abyss/Equipment/Icons/sil_legs.png when available.");
            }

            if (silBoots != null) return silBoots;
            if (silChest != null) return silChest;

            Debug.LogWarning("[Abyssbound][Icons] TODO: Missing sil_legs and no distinct fallback (boots/chest) found. Add Assets/Abyss/Equipment/Icons/sil_legs.png");
            return null;
        }

        private static WeaponKind GuessWeaponKind(string id, string displayName, string unityName)
        {
            string hay = (id ?? string.Empty) + " " + (displayName ?? string.Empty) + " " + (unityName ?? string.Empty);
            hay = hay.ToLowerInvariant();

            // Ranged/bow
            if (hay.Contains("bow") || hay.Contains("ranged"))
                return WeaponKind.Bow;

            // Magic/staff
            if (hay.Contains("staff") || hay.Contains("wand") || hay.Contains("magic"))
                return WeaponKind.Staff;

            // Generic weapon (default for RightHand)
            return WeaponKind.Generic;
        }

        private static LootV2ScanReport ScanLootV2Equipment()
        {
            var result = new LootV2ScanReport();

            if (!AssetDatabase.IsValidFolder(LootV2ItemsRoot))
            {
                Debug.LogWarning($"[Abyssbound][Icons] NOT FOUND: folder '{LootV2ItemsRoot}'");
                return result;
            }

            string[] guids;
            try
            {
                guids = AssetDatabase.FindAssets("t:ItemDefinitionSO", new[] { LootV2ItemsRoot });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Abyssbound][Icons] Failed to FindAssets under '{LootV2ItemsRoot}': {ex.Message}");
                return result;
            }

            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                var item = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(path);
                if (item == null)
                    continue;

                EquipmentSlot slot;
                try { slot = item.slot; }
                catch { slot = EquipmentSlot.None; }

                if (slot == EquipmentSlot.None)
                    continue;

                var icon = item.icon;
                bool isMissingOrPlaceholder = IsMissingOrPlaceholder(icon);

                bool occupiesBoth = OccupiesBothHands(item);

                result.Scanned++;
                if (isMissingOrPlaceholder)
                    result.MissingOrPlaceholder++;

                result.Items.Add(new LootV2ItemEntry
                {
                    Item = item,
                    AssetPath = path,
                    Slot = slot,
                    OccupiesBothHands = occupiesBoth,
                    IsMissingOrPlaceholder = isMissingOrPlaceholder,
                    CurrentIcon = icon,
                });
            }

            return result;
        }

        private static bool OccupiesBothHands(ItemDefinitionSO item)
        {
            if (item == null) return false;
            try
            {
                if (item.occupiesSlots == null || item.occupiesSlots.Count == 0)
                    return false;

                bool left = false;
                bool right = false;

                for (int i = 0; i < item.occupiesSlots.Count; i++)
                {
                    if (item.occupiesSlots[i] == EquipmentSlot.LeftHand) left = true;
                    if (item.occupiesSlots[i] == EquipmentSlot.RightHand) right = true;
                }

                return left && right;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsMissingOrPlaceholder(Sprite sprite)
        {
            if (sprite == null)
                return true;

            string name = null;
            try { name = sprite.name; }
            catch { name = null; }

            string path = null;
            try { path = AssetDatabase.GetAssetPath(sprite); }
            catch { path = null; }

            // Step B: placeholder if it's a silhouette (sil_*) OR name/path contains placeholder/tmp.
            if (StartsWithSilhouettePrefix(name) || ContainsSilhouetteSegment(path))
                return true;

            return ContainsPlaceholderNeedle(name) || ContainsPlaceholderNeedle(path);
        }

        private static bool StartsWithSilhouettePrefix(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return value.StartsWith("sil_", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsSilhouetteSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            // Asset paths are typically like Assets/.../sil_helm.png
            return value.IndexOf("/sil_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("\\\\sil_", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsPlaceholderNeedle(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var lower = value.ToLowerInvariant();
            for (int i = 0; i < PlaceholderNeedles.Length; i++)
            {
                if (lower.Contains(PlaceholderNeedles[i]))
                    return true;
            }

            return false;
        }

        private static bool IsLegacyMissingOrPlaceholder(Sprite sprite)
        {
            if (sprite == null)
                return true;

            string name = null;
            try { name = sprite.name; } catch { name = null; }

            string path = null;
            try { path = AssetDatabase.GetAssetPath(sprite); } catch { path = null; }

            if (ContainsPlaceholderNeedle(name) || ContainsPlaceholderNeedle(path))
                return true;

            if (LooksLikeLetterIcon(name) || LooksLikeLetterIcon(path))
                return true;

            return false;
        }

        private static bool LooksLikeLetterIcon(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            // Match "letter" as a standalone-ish token, to avoid false positives like "Leather".
            return LetterWordRegex.IsMatch(value);
        }

        private static Sprite GetSlotSprite(EquipmentSlot slot, bool occupiesBothHands, HashSet<EquipmentSlot> warnedMissingSlotSprites)
        {
            // Mapping based on the ICON PASS RECON (silhouette icons under Assets/Abyss/Equipment/Icons/)
            // Rules:
            // - Ring1/Ring2 share ring icon
            // - LeftHand/RightHand use weapon icon unless distinct hand icons exist
            // - 2H still uses weapon icon

            switch (slot)
            {
                case EquipmentSlot.Helm:
                    return LoadSprite("Assets/Abyss/Equipment/Icons/sil_helm.png");
                case EquipmentSlot.Chest:
                    return LoadSprite("Assets/Abyss/Equipment/Icons/sil_chest.png");
                case EquipmentSlot.Legs:
                    // IMPORTANT (per Step B): if sil_legs does not exist, skip and warn.
                    return LoadSpriteRequired("Assets/Abyss/Equipment/Icons/sil_legs.png", EquipmentSlot.Legs, warnedMissingSlotSprites);
                case EquipmentSlot.Belt:
                    return LoadSprite("Assets/Abyss/Equipment/Icons/sil_belt.png");
                case EquipmentSlot.Gloves:
                    return LoadSprite("Assets/Abyss/Equipment/Icons/sil_gloves.png");
                case EquipmentSlot.Boots:
                    return LoadSprite("Assets/Abyss/Equipment/Icons/sil_boots.png");
                case EquipmentSlot.Cape:
                    return LoadSprite("Assets/Abyss/Equipment/Icons/sil_cape.png");
                case EquipmentSlot.Ammo:
                    return LoadSprite("Assets/Abyss/Equipment/Icons/sil_arrows.png");
                case EquipmentSlot.Amulet:
                    return LoadSprite("Assets/Abyss/Equipment/Icons/sil_amulet.png");
                case EquipmentSlot.Ring1:
                case EquipmentSlot.Ring2:
                    return LoadSprite("Assets/Abyss/Equipment/Icons/sil_ring.png");
                case EquipmentSlot.Artifact:
                    return LoadSprite("Assets/Abyss/Equipment/Icons/sil_orb.png");
                case EquipmentSlot.LeftHand:
                    // Step B mapping: LeftHand -> sil_shield. If missing, skip and warn.
                    return LoadSpriteRequired("Assets/Abyss/Equipment/Icons/sil_shield.png", EquipmentSlot.LeftHand, warnedMissingSlotSprites);
                case EquipmentSlot.RightHand:
                    // 2H still uses weapon icon.
                    return LoadSprite("Assets/Abyss/Equipment/Icons/sil_sword.png");
                default:
                    return null;
            }
        }

        private static Sprite GetLegacyShopSprite(ItemDefinition item, HashSet<EquipmentSlot> warnedMissingSlotSprites)
        {
            if (item == null)
                return null;

            EquipmentSlot slot;
            try { slot = item.equipmentSlot; }
            catch { slot = EquipmentSlot.None; }

            if (slot == EquipmentSlot.None)
                return null;

            // Weapon heuristics for Bow/Staff.
            bool isBow = false;
            bool isStaff = false;

            try
            {
                var hay = ((item.itemId ?? string.Empty) + " " + (item.displayName ?? string.Empty) + " " + item.name).ToLowerInvariant();
                if (hay.Contains("bow")) isBow = true;
                if (hay.Contains("staff") || hay.Contains("wand")) isStaff = true;
            }
            catch { }

            switch (slot)
            {
                case EquipmentSlot.Helm:
                    return LoadSprite("Assets/Abyss/Equipment/Icons/sil_helm.png");
                case EquipmentSlot.Chest:
                    return LoadSprite("Assets/Abyss/Equipment/Icons/sil_chest.png");
                case EquipmentSlot.Legs:
                    return LoadSpriteRequired("Assets/Abyss/Equipment/Icons/sil_legs.png", EquipmentSlot.Legs, warnedMissingSlotSprites);
                case EquipmentSlot.Gloves:
                    return LoadSprite("Assets/Abyss/Equipment/Icons/sil_gloves.png");
                case EquipmentSlot.Boots:
                    return LoadSprite("Assets/Abyss/Equipment/Icons/sil_boots.png");
                case EquipmentSlot.Belt:
                    return LoadSprite("Assets/Abyss/Equipment/Icons/sil_belt.png");
                case EquipmentSlot.Cape:
                    return LoadSprite("Assets/Abyss/Equipment/Icons/sil_cape.png");
                case EquipmentSlot.Ammo:
                    return LoadSprite("Assets/Abyss/Equipment/Icons/sil_arrows.png");
                case EquipmentSlot.Amulet:
                    return LoadSprite("Assets/Abyss/Equipment/Icons/sil_amulet.png");
                case EquipmentSlot.Ring1:
                case EquipmentSlot.Ring2:
                    return LoadSprite("Assets/Abyss/Equipment/Icons/sil_ring.png");
                case EquipmentSlot.Artifact:
                    return LoadSprite("Assets/Abyss/Equipment/Icons/sil_orb.png");
                case EquipmentSlot.LeftHand:
                    return LoadSpriteRequired("Assets/Abyss/Equipment/Icons/sil_shield.png", EquipmentSlot.LeftHand, warnedMissingSlotSprites);
                case EquipmentSlot.RightHand:
                    if (isBow)
                    {
                        var arrows = LoadSprite("Assets/Abyss/Equipment/Icons/sil_arrows.png");
                        if (arrows != null) return arrows;
                    }

                    if (isStaff)
                    {
                        var orb = LoadSprite("Assets/Abyss/Equipment/Icons/sil_orb.png");
                        if (orb != null) return orb;
                    }

                    return LoadSprite("Assets/Abyss/Equipment/Icons/sil_sword.png");
                default:
                    return null;
            }
        }

        private static Sprite LoadSpriteRequired(string assetPath, EquipmentSlot slot, HashSet<EquipmentSlot> warnedMissingSlotSprites)
        {
            var sprite = LoadSprite(assetPath);
            if (sprite == null)
            {
                // If the file exists but isn't importing as a Sprite, try to correct importer settings.
                TryEnsureSpriteImport(assetPath);
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            }
            if (sprite != null)
                return sprite;

            if (warnedMissingSlotSprites != null && warnedMissingSlotSprites.Add(slot))
                Debug.LogWarning($"[Abyssbound][Icons] Missing slot icon for {slot}: expected Sprite at '{assetPath}'. Items in this slot will be skipped. (If the file exists, ensure Texture Type = Sprite (2D and UI).)");

            return null;
        }

        private static void TryEnsureSpriteImport(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return;

            // Only attempt for common image types.
            var ext = Path.GetExtension(assetPath);
            if (!string.Equals(ext, ".png", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(ext, ".jpg", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(ext, ".jpeg", StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                    return;

                if (importer.textureType == TextureImporterType.Sprite)
                    return;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            catch
            {
                // Keep this best-effort; the caller will warn if the sprite is still missing.
            }
        }

        private static Sprite LoadSprite(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return null;

            if (SpriteCache.TryGetValue(assetPath, out var cached))
                return cached;

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);

            // Important: do NOT cache null, so that adding/importing a sprite later in the same editor session works.
            if (sprite != null)
                SpriteCache[assetPath] = sprite;

            return sprite;
        }

        private static void LogReport(string title, LootV2ScanReport report)
        {
            int skipped = report.Scanned - report.MissingOrPlaceholder;

            Debug.Log(
                $"[Abyssbound][Icons] {title}\n" +
                $"- scanned: {report.Scanned}\n" +
                $"- missing/placeholder: {report.MissingOrPlaceholder}\n" +
                $"- skipped: {skipped}");

            var paths = new List<string>();
            for (int i = 0; i < report.Items.Count; i++)
            {
                if (report.Items[i].IsMissingOrPlaceholder)
                    paths.Add(report.Items[i].AssetPath);
            }

            LogPathsWithLimit("[Abyssbound][Icons] Missing/placeholder assets", paths, 25);
        }

        private static void LogPathsWithLimit(string header, List<string> paths, int limit)
        {
            if (paths == null || paths.Count == 0)
            {
                Debug.Log($"{header}: (none)");
                return;
            }

            int count = paths.Count;
            int take = Mathf.Clamp(limit, 0, count);

            var msg = header + ":\n";
            for (int i = 0; i < take; i++)
                msg += "- " + paths[i] + "\n";

            if (count > take)
                msg += $"... and {count - take} more\n";

            Debug.Log(msg);
        }

        private static LegacyShopScanReport ScanLegacyShopInventoryForPlaceholders(string shopInventoryAssetPath)
        {
            var report = new LegacyShopScanReport();

            if (string.IsNullOrWhiteSpace(shopInventoryAssetPath))
            {
                Debug.LogWarning("[Abyssbound][Icons] NOT FOUND: ShopInventory path was empty.");
                return report;
            }

            var inventory = AssetDatabase.LoadAssetAtPath<ShopInventory>(shopInventoryAssetPath);
            if (inventory == null)
            {
                Debug.LogWarning($"[Abyssbound][Icons] NOT FOUND: ShopInventory at '{shopInventoryAssetPath}'");
                return report;
            }

            var uniqueItems = new HashSet<ItemDefinition>();
            try
            {
                if (inventory.entries != null)
                {
                    for (int i = 0; i < inventory.entries.Count; i++)
                    {
                        var e = inventory.entries[i];
                        if (e == null || e.item == null) continue;
                        uniqueItems.Add(e.item);
                    }
                }
            }
            catch { }

            foreach (var item in uniqueItems)
            {
                if (item == null) continue;

                EquipmentSlot slot;
                try { slot = item.equipmentSlot; }
                catch { slot = EquipmentSlot.None; }

                // Scope guard: only equippable legacy items (weapons/armor) from this shop.
                if (slot == EquipmentSlot.None)
                    continue;

                bool placeholder = IsLegacyMissingOrPlaceholder(item.icon);

                report.Scanned++;
                if (placeholder) report.MissingOrPlaceholder++;

                report.Items.Add(new LegacyShopItemEntry
                {
                    Item = item,
                    AssetPath = AssetDatabase.GetAssetPath(item),
                    Slot = slot,
                    IsMissingOrPlaceholder = placeholder,
                    CurrentIcon = item.icon,
                });
            }

            return report;
        }

        private static void LogLegacyShopReport(string title, LegacyShopScanReport report)
        {
            int skipped = report.Scanned - report.MissingOrPlaceholder;

            Debug.Log(
                $"[Abyssbound][Icons] {title}\n" +
                $"- scanned: {report.Scanned}\n" +
                $"- missing/placeholder: {report.MissingOrPlaceholder}\n" +
                $"- skipped: {skipped}");

            var paths = new List<string>();
            for (int i = 0; i < report.Items.Count; i++)
            {
                if (report.Items[i].IsMissingOrPlaceholder)
                    paths.Add(report.Items[i].AssetPath);
            }

            LogPathsWithLimit("[Abyssbound][Icons] Legacy shop items with placeholder icons", paths, 25);
        }

        private sealed class LootV2ScanReport
        {
            public int Scanned;
            public int MissingOrPlaceholder;
            public readonly List<LootV2ItemEntry> Items = new();
        }

        private sealed class LootV2ItemEntry
        {
            public ItemDefinitionSO Item;
            public string AssetPath;
            public EquipmentSlot Slot;
            public bool OccupiesBothHands;
            public bool IsMissingOrPlaceholder;
            public Sprite CurrentIcon;
        }

        private sealed class LegacyShopScanReport
        {
            public int Scanned;
            public int MissingOrPlaceholder;
            public readonly List<LegacyShopItemEntry> Items = new();
        }

        private sealed class LegacyShopItemEntry
        {
            public ItemDefinition Item;
            public string AssetPath;
            public EquipmentSlot Slot;
            public bool IsMissingOrPlaceholder;
            public Sprite CurrentIcon;
        }
    }
}
