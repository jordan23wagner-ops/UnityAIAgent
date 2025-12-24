#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Abyss.Equipment;
using Abyss.Inventory;
using Abyss.Items;
using Abyssbound.Loot;
using Game.Systems;
using UnityEditor;
using UnityEngine;

public static class LootQaSmokeTestMenu
{
    [MenuItem("Tools/Abyssbound/QA/Smoke Test/Run Loot QA Smoke Test (Setup + Spawn + Equip + Open UI)")]
    public static void Run()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[LootQA Smoke] Enter Play Mode first, then run this command.");
            return;
        }

        // Ensure the QA settings asset exists.
        try { LootQaSelectedItemSettingsEditor.EnsureAssetExists(); } catch { }

        // Pick a good equippable ItemDefinitionSO automatically (weapon preferred), and set it as Selected.
        var picked = FindBestEquippableItemDefinitionSo();
        if (picked == null)
        {
            Debug.LogWarning("[LootQA Smoke] Could not find any ItemDefinitionSO assets. Create one via Create/Abyssbound/Loot/Item Definition.");
            return;
        }

        try
        {
            var settings = AssetDatabase.LoadAssetAtPath<LootQaSelectedItemSettingsSO>("Assets/Resources/LootQaSelectedItemSettings.asset");
            if (settings != null)
            {
                settings.selectedItemDefinition = picked;
                if (settings.defaultSelectedItemDefinition == null)
                    settings.defaultSelectedItemDefinition = picked;
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
        }
        catch { }

        Debug.Log($"[LootQA Smoke] Using QA item: {picked.name}", picked);

        // Open UIs so the user can visually confirm rarity borders/strips immediately.
        OpenInventoryAndEquipmentUi();

        var inv = PlayerInventoryResolver.GetOrFind();
        var eq = PlayerEquipmentResolver.GetOrFindOrCreate();

        // Reset state to make the run deterministic.
        try { UnequipAllToInventory(eq, inv); } catch { }
        try { ClearInventory(inv); } catch { }

        // Standardize QA settings for this run.
        LootQaSettings.DebugLogsEnabled = true;

        // Run scenarios.
        var created = new List<string>(64);

        int s1 = SpawnScenario(picked, LootQaSpawnHelper.AllRarityIds, itemLevel: 1, created, "S1");
        int s2 = SpawnScenario(picked, LootQaSpawnHelper.AllRarityIds, itemLevel: 20, created, "S2");
        int s3 = SpawnScenario(picked, LootQaSpawnHelper.MagicPlusRarityIds, itemLevel: 10, created, "S3");

        // Equip representative items so equipment border/strip visuals are exercised.
        int equipOk = 0;
        int equipFail = 0;
        string lastEquipMsg = string.Empty;

        // Prefer equipping a Legendary if present; otherwise, just equip the last spawned item.
        var toEquip = PickPreferredEquipIds(created);
        for (int i = 0; i < toEquip.Count; i++)
        {
            var id = toEquip[i];
            if (string.IsNullOrWhiteSpace(id)) continue;

            if (inv != null && !inv.Has(id, 1))
                continue;

            if (eq != null)
            {
                bool ok = eq.TryEquipFromInventory(inv, resolve: null, itemId: id, out var msg);
                lastEquipMsg = msg;
                if (ok) equipOk++;
                else equipFail++;
            }
        }

        // Force UI refresh if open.
        try { ForceUiRefresh(); } catch { }

        Debug.Log(
            $"[LootQA Smoke] Done. Spawned S1={s1} (ilvl1 all), S2={s2} (ilvl20 all), S3={s3} (ilvl10 magic+) | Equipped ok={equipOk} fail={equipFail} | lastEquipMsg='{lastEquipMsg}' | stacksNow={(inv != null ? inv.GetStackCount() : 0)}"
        );

        Debug.Log("[LootQA Smoke] Visual checks: inventory grid borders should be rarity-tinted; equipment slot outline + rarity strip should be rarity-tinted (legacy + rolled). Open tooltip/details to confirm name tinting.");
    }

    [MenuItem("Tools/Abyssbound/QA/Smoke Test/Run Loot QA Full Automation (Multi-slot + 200 Drops)")]
    public static void RunFullAutomation()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[LootQA Full] Enter Play Mode first, then run this command.");
            return;
        }

        // Ensure the QA settings asset exists.
        try { LootQaSelectedItemSettingsEditor.EnsureAssetExists(); } catch { }

        OpenInventoryAndEquipmentUi();

        var inv = PlayerInventoryResolver.GetOrFind();
        var eq = PlayerEquipmentResolver.GetOrFindOrCreate();

        try { UnequipAllToInventory(eq, inv); } catch { }
        try { ClearInventory(inv); } catch { }

        LootQaSettings.DebugLogsEnabled = true;

        int multiSlotsEquippedOk = 0;
        int multiSlotsEquippedFail = 0;
        try
        {
            RunMultiSlotCycle(inv, eq, maxDistinctSlots: 6, out multiSlotsEquippedOk, out multiSlotsEquippedFail);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LootQA Full] Multi-slot cycle failed: {e.Message}");
        }

        try
        {
            Run200DropSimulation(itemLevel: 10, rollCount: 200);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LootQA Full] 200-drop simulation failed: {e.Message}");
        }

        try { ForceUiRefresh(); } catch { }

        Debug.Log($"[LootQA Full] Done. Multi-slot equip ok={multiSlotsEquippedOk} fail={multiSlotsEquippedFail} | stacksNow={(inv != null ? inv.GetStackCount() : 0)}");
    }

    [MenuItem("Tools/Abyssbound/QA/Smoke Test/Simulate 200 Drops (Auto LootTableSO)")]
    public static void Simulate200Drops()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[LootQA Sim] Enter Play Mode first, then run this command.");
            return;
        }

        try
        {
            Run200DropSimulation(itemLevel: 10, rollCount: 200);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LootQA Sim] Failed: {e.Message}");
        }
    }

    private static int SpawnScenario(UnityEngine.Object selection, IReadOnlyList<string> rarityIds, int itemLevel, List<string> outIds, string tag)
    {
        LootQaSettings.ItemLevel = Mathf.Clamp(itemLevel, 1, 20);
        int spawned = LootQaSpawnHelper.SpawnSelectedItemForRarityIds(
            selection,
            rarityIds,
            itemLevel: LootQaSettings.ItemLevel,
            perItemLogs: true,
            logPrefix: $"[LootQA Smoke {tag}]",
            outRolledIds: outIds
        );
        return spawned;
    }

    private static ItemDefinitionSO FindBestEquippableItemDefinitionSo()
    {
        try
        {
            var guids = AssetDatabase.FindAssets("t:ItemDefinitionSO");
            if (guids == null || guids.Length == 0)
                return null;

            ItemDefinitionSO firstAny = null;
            ItemDefinitionSO firstEquippable = null;
            ItemDefinitionSO firstWeaponish = null;

            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var so = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(path);
                if (so == null) continue;

                firstAny ??= so;

                bool equippable = false;
                try { equippable = so.slot != EquipmentSlot.None; } catch { equippable = false; }

                if (!equippable)
                    continue;

                firstEquippable ??= so;

                // Prefer something that occupies hands (usually most visible in equipment UI).
                bool occupiesHand = false;
                try
                {
                    if (so.occupiesSlots != null)
                    {
                        for (int j = 0; j < so.occupiesSlots.Count; j++)
                        {
                            if (so.occupiesSlots[j] == EquipmentSlot.LeftHand || so.occupiesSlots[j] == EquipmentSlot.RightHand)
                                occupiesHand = true;
                        }
                    }
                }
                catch { occupiesHand = false; }

                if (occupiesHand || so.slot == EquipmentSlot.RightHand || so.slot == EquipmentSlot.LeftHand)
                {
                    firstWeaponish ??= so;
                    break;
                }
            }

            return firstWeaponish ?? firstEquippable ?? firstAny;
        }
        catch
        {
            return null;
        }
    }

    private static void OpenInventoryAndEquipmentUi()
    {
        try
        {
#if UNITY_2022_2_OR_NEWER
            var invUi = UnityEngine.Object.FindFirstObjectByType<PlayerInventoryUI>();
#else
            var invUi = UnityEngine.Object.FindObjectOfType<PlayerInventoryUI>();
#endif
            invUi?.Open();
        }
        catch { }

        try
        {
#if UNITY_2022_2_OR_NEWER
            var eqUi = UnityEngine.Object.FindFirstObjectByType<PlayerEquipmentUI>();
#else
            var eqUi = UnityEngine.Object.FindObjectOfType<PlayerEquipmentUI>();
#endif
            eqUi?.Open();
        }
        catch { }
    }

    private static void ForceUiRefresh()
    {
        try
        {
#if UNITY_2022_2_OR_NEWER
            var invUi = UnityEngine.Object.FindFirstObjectByType<PlayerInventoryUI>();
#else
            var invUi = UnityEngine.Object.FindObjectOfType<PlayerInventoryUI>();
#endif
            if (invUi != null)
            {
                // RefreshAll is private; call via reflection to avoid changing runtime API.
                InvokeInstanceMethod(invUi, "RefreshAll");
            }
        }
        catch { }

        try
        {
#if UNITY_2022_2_OR_NEWER
            var eqUi = UnityEngine.Object.FindFirstObjectByType<PlayerEquipmentUI>();
#else
            var eqUi = UnityEngine.Object.FindObjectOfType<PlayerEquipmentUI>();
#endif
            if (eqUi != null)
                InvokeInstanceMethod(eqUi, "Refresh");
        }
        catch { }
    }

    private static void UnequipAllToInventory(PlayerEquipment eq, PlayerInventory inv)
    {
        if (eq == null || inv == null)
            return;

        // Try unequip each slot to inventory; this raises Changed events.
        var slots = (EquipmentSlot[])Enum.GetValues(typeof(EquipmentSlot));
        for (int i = 0; i < slots.Length; i++)
        {
            var s = slots[i];
            if (s == EquipmentSlot.None) continue;
            try { eq.TryUnequipToInventory(inv, resolve: null, slot: s); } catch { }
        }
    }

    private static void ClearInventory(PlayerInventory inv)
    {
        if (inv == null)
            return;

        try
        {
            var f = typeof(PlayerInventory).GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic);
            if (f != null && f.GetValue(inv) is IDictionary dict)
                dict.Clear();
        }
        catch { }

        // Fire Changed event so open UI refreshes.
        try
        {
            var field = typeof(PlayerInventory).GetField("Changed", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null && field.GetValue(inv) is Action a)
                a.Invoke();
        }
        catch { }
    }

    private static List<string> PickPreferredEquipIds(List<string> created)
    {
        // Pick a small subset to avoid thrashing equipment:
        // - Prefer Legendary/Epic/Rare by checking the registry rarity id.
        // - Fallback to last item.
        var result = new List<string>(3);
        if (created == null || created.Count == 0)
            return result;

        try
        {
            var reg = LootRegistryRuntime.GetOrCreate();
            string bestLegendary = null;
            string bestEpic = null;
            string bestRare = null;

            for (int i = 0; i < created.Count; i++)
            {
                var id = created[i];
                if (string.IsNullOrWhiteSpace(id)) continue;

                if (reg != null && reg.TryGetRolledInstance(id, out var inst) && inst != null)
                {
                    var rid = inst.rarityId ?? string.Empty;
                    if (bestLegendary == null && rid.Equals("Legendary", StringComparison.OrdinalIgnoreCase)) bestLegendary = id;
                    else if (bestEpic == null && rid.Equals("Epic", StringComparison.OrdinalIgnoreCase)) bestEpic = id;
                    else if (bestRare == null && rid.Equals("Rare", StringComparison.OrdinalIgnoreCase)) bestRare = id;
                }
            }

            if (bestLegendary != null) result.Add(bestLegendary);
            if (bestEpic != null) result.Add(bestEpic);
            if (bestRare != null) result.Add(bestRare);

            if (result.Count == 0)
                result.Add(created[created.Count - 1]);
        }
        catch
        {
            result.Add(created[created.Count - 1]);
        }

        return result;
    }

    private static List<string> PickPreferredEquipIdsForBaseItem(List<string> created, string baseItemId)
    {
        var result = new List<string>(2);
        if (created == null || created.Count == 0 || string.IsNullOrWhiteSpace(baseItemId))
            return result;

        try
        {
            var reg = LootRegistryRuntime.GetOrCreate();
            string bestLegendary = null;
            string bestEpic = null;
            string bestRare = null;

            for (int i = 0; i < created.Count; i++)
            {
                var id = created[i];
                if (string.IsNullOrWhiteSpace(id)) continue;

                if (reg != null && reg.TryGetRolledInstance(id, out var inst) && inst != null)
                {
                    if (!string.Equals(inst.baseItemId, baseItemId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var rid = inst.rarityId ?? string.Empty;
                    if (bestLegendary == null && rid.Equals("Legendary", StringComparison.OrdinalIgnoreCase)) bestLegendary = id;
                    else if (bestEpic == null && rid.Equals("Epic", StringComparison.OrdinalIgnoreCase)) bestEpic = id;
                    else if (bestRare == null && rid.Equals("Rare", StringComparison.OrdinalIgnoreCase)) bestRare = id;
                }
            }

            if (bestLegendary != null) result.Add(bestLegendary);
            else if (bestEpic != null) result.Add(bestEpic);
            else if (bestRare != null) result.Add(bestRare);
        }
        catch
        {
            // ignore
        }

        return result;
    }

    private static void RunMultiSlotCycle(PlayerInventory inv, PlayerEquipment eq, int maxDistinctSlots, out int equipOk, out int equipFail)
    {
        equipOk = 0;
        equipFail = 0;

        if (inv == null || eq == null)
        {
            Debug.LogWarning("[LootQA Full] Missing inventory/equipment; cannot run multi-slot cycle.");
            return;
        }

        // Keep total spawned <= 28 slots. MagicPlus is 4 items per base item.
        int maxSlotsSafe = Mathf.Clamp(maxDistinctSlots, 1, 7);

        var picks = FindEquippableItemsAcrossSlots(maxSlotsSafe);
        if (picks.Count == 0)
        {
            Debug.LogWarning("[LootQA Full] No equippable ItemDefinitionSO assets found.");
            return;
        }

        var created = new List<string>(64);
        int spawnedTotal = 0;

        for (int i = 0; i < picks.Count; i++)
        {
            var item = picks[i];
            if (item == null) continue;

            // Update QA selection so F7 + other QA tools remain consistent after the run.
            TrySetQaSelectedItem(item);

            int spawned = SpawnScenario(item, LootQaSpawnHelper.MagicPlusRarityIds, itemLevel: 10, created, $"MS{i + 1}");
            spawnedTotal += spawned;

            // Equip something from this base item.
            var toEquip = PickPreferredEquipIdsForBaseItem(created, item.id);
            for (int j = 0; j < toEquip.Count; j++)
            {
                var id = toEquip[j];
                if (!inv.Has(id, 1)) continue;

                bool ok = eq.TryEquipFromInventory(inv, resolve: null, itemId: id, out _);
                if (ok) equipOk++;
                else equipFail++;
            }
        }

        Debug.Log($"[LootQA Full] Multi-slot cycle: distinctSlots={picks.Count} spawnedTotal={spawnedTotal} equipOk={equipOk} equipFail={equipFail}");
    }

    private static List<ItemDefinitionSO> FindEquippableItemsAcrossSlots(int maxDistinctSlots)
    {
        var results = new List<ItemDefinitionSO>(Mathf.Max(1, maxDistinctSlots));
        try
        {
            var guids = AssetDatabase.FindAssets("t:ItemDefinitionSO");
            if (guids == null || guids.Length == 0)
                return results;

            // Prefer visibly distinct equipment slots.
            var preferredOrder = new List<EquipmentSlot>
            {
                EquipmentSlot.RightHand,
                EquipmentSlot.LeftHand,
                EquipmentSlot.Chest,
                EquipmentSlot.Helm,
                EquipmentSlot.Legs,
                EquipmentSlot.Boots,
                EquipmentSlot.Ring1,
                EquipmentSlot.Ring2,
                EquipmentSlot.Amulet,
                EquipmentSlot.Gloves,
            };

            var pickedBySlot = new Dictionary<EquipmentSlot, ItemDefinitionSO>();
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var so = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(path);
                if (so == null) continue;

                EquipmentSlot slot = EquipmentSlot.None;
                try { slot = so.slot; } catch { slot = EquipmentSlot.None; }
                if (slot == EquipmentSlot.None) continue;
                if (pickedBySlot.ContainsKey(slot)) continue;

                pickedBySlot[slot] = so;
                if (pickedBySlot.Count >= Mathf.Max(1, maxDistinctSlots) * 2)
                    break;
            }

            for (int i = 0; i < preferredOrder.Count && results.Count < Mathf.Max(1, maxDistinctSlots); i++)
            {
                if (pickedBySlot.TryGetValue(preferredOrder[i], out var so) && so != null)
                    results.Add(so);
            }

            if (results.Count < Mathf.Max(1, maxDistinctSlots))
            {
                foreach (var kv in pickedBySlot)
                {
                    if (results.Count >= Mathf.Max(1, maxDistinctSlots))
                        break;
                    if (kv.Value == null) continue;
                    if (!results.Contains(kv.Value))
                        results.Add(kv.Value);
                }
            }
        }
        catch
        {
            // ignore
        }

        return results;
    }

    private static void TrySetQaSelectedItem(ItemDefinitionSO item)
    {
        if (item == null)
            return;

        try
        {
            var settings = AssetDatabase.LoadAssetAtPath<LootQaSelectedItemSettingsSO>("Assets/Resources/LootQaSelectedItemSettings.asset");
            if (settings == null)
                return;

            settings.selectedItemDefinition = item;
            if (settings.defaultSelectedItemDefinition == null)
                settings.defaultSelectedItemDefinition = item;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }
        catch { }
    }

    private static void Run200DropSimulation(int itemLevel, int rollCount)
    {
        int n = Mathf.Max(1, rollCount);
        int ilvl = Mathf.Clamp(itemLevel, 1, 20);

        var table = TryFindLootTableSo();
        if (table == null)
        {
            Debug.LogWarning("[LootQA Sim] No LootTableSO assets found.");
            return;
        }

        var rarityCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var itemCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int nullRolls = 0;
        int totalAffixes = 0;

        // Deterministic across runs.
        const int baseSeed = 1337;

        for (int i = 0; i < n; i++)
        {
            var inst = LootRollerV2.RollItem(table, itemLevel: ilvl, seed: baseSeed + i);
            if (inst == null)
            {
                nullRolls++;
                continue;
            }

            var rid = string.IsNullOrWhiteSpace(inst.rarityId) ? "<none>" : inst.rarityId;
            var bid = string.IsNullOrWhiteSpace(inst.baseItemId) ? "<none>" : inst.baseItemId;

            rarityCounts.TryGetValue(rid, out var rc);
            rarityCounts[rid] = rc + 1;

            itemCounts.TryGetValue(bid, out var ic);
            itemCounts[bid] = ic + 1;

            try { totalAffixes += inst.affixes != null ? inst.affixes.Count : 0; } catch { }
        }

        string RarityLine(string id)
        {
            rarityCounts.TryGetValue(id, out var c);
            float pct = n > 0 ? (100f * c / n) : 0f;
            return $"{id}={c} ({pct:0.0}%)";
        }

        var topItems = GetTopN(itemCounts, 5);
        float avgAff = n > 0 ? (float)totalAffixes / n : 0f;

        Debug.Log(
            $"[LootQA Sim] Table='{table.name}' ilvl={ilvl} rolls={n} nulls={nullRolls} avgAffixes={avgAff:0.00} | " +
            $"{RarityLine("Common")}, {RarityLine("Uncommon")}, {RarityLine("Magic")}, {RarityLine("Rare")}, {RarityLine("Epic")}, {RarityLine("Legendary")}, {RarityLine("Set")}, {RarityLine("Radiant")} | " +
            $"TopItems={string.Join(", ", topItems)}"
        );
    }

    private static LootTableSO TryFindLootTableSo()
    {
        // Prefer known starter/zone tables created by content tools.
        var preferredPaths = new[]
        {
            "Assets/Resources/Loot/Tables/LootTable_Starter.asset",
            "Assets/Resources/Loot/Tables/Zone1_Trash.asset",
            "Assets/Resources/Loot/Tables/Zone1_Elite.asset",
            "Assets/Resources/Loot/Tables/Zone1_Boss.asset",
        };

        for (int i = 0; i < preferredPaths.Length; i++)
        {
            var t = AssetDatabase.LoadAssetAtPath<LootTableSO>(preferredPaths[i]);
            if (t != null) return t;
        }

        try
        {
            var guids = AssetDatabase.FindAssets("t:LootTableSO");
            if (guids == null || guids.Length == 0)
                return null;
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<LootTableSO>(path);
        }
        catch
        {
            return null;
        }
    }

    private static List<string> GetTopN(Dictionary<string, int> counts, int n)
    {
        var results = new List<string>(Mathf.Max(1, n));
        if (counts == null || counts.Count == 0 || n <= 0)
            return results;

        try
        {
            var list = new List<KeyValuePair<string, int>>(counts);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));
            int take = Mathf.Min(n, list.Count);
            for (int i = 0; i < take; i++)
                results.Add($"{list[i].Key}={list[i].Value}");
        }
        catch { }

        return results;
    }

    private static void InvokeInstanceMethod(object target, string methodName)
    {
        if (target == null || string.IsNullOrWhiteSpace(methodName))
            return;

        try
        {
            var mi = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            mi?.Invoke(target, null);
        }
        catch { }
    }
}
#endif
