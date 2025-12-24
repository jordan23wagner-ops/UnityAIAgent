#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Abyss.Equipment;
using Abyss.Items;
using Abyssbound.Loot;
using UnityEditor;
using UnityEngine;

public static class StarterSetQaMenu
{
    private const string SetAssetPath = "Assets/GameData/Sets/ItemSet_AbyssalInitiate.asset";

    private const string StarterSetRoot = "Assets/GameData/Loot/StarterSet";
    private const string HelmAssetPath = StarterSetRoot + "/Item_Starter_Helm.asset";
    private const string ChestAssetPath = StarterSetRoot + "/Item_Starter_Chest.asset";
    private const string LegsAssetPath = StarterSetRoot + "/Item_Starter_Legs.asset";

    private const string IconHelmPath = "Assets/Abyss/Equipment/Icons/sil_helm.png";
    private const string IconChestPath = "Assets/Abyss/Equipment/Icons/sil_chest.png";
    private const string IconLegsPath = "Assets/Abyss/Equipment/Icons/sil_boots.png"; // closest available placeholder

    private const string SetId = "AbyssalInitiate";
    private const string SetDisplayName = "Abyssal Initiate";

    private static bool s_warnedNotPlaying;

    [InitializeOnLoadMethod]
    private static void EnsureStarterSetAssetsOnEditorLoad()
    {
        // Required starter content (A5). No logs; no play-mode requirement.
        try { EnsureStarterSetAssets(out _, out _); } catch { }
    }

    [MenuItem("Tools/Abyssbound/QA/Print Equipped Set Counts")]
    public static void PrintEquippedSetCounts()
    {
        if (!EnsurePlayMode())
            return;

        var tracker = EquippedSetTracker.GetOrCreate();
        tracker.ForceRebuild();

        var counts = tracker.GetAllEquippedSetCounts();
        if (counts == null || counts.Count == 0)
        {
            Debug.Log("[QA] No set pieces equipped.");
            return;
        }

        foreach (var kvp in counts)
        {
            var set = kvp.Key;
            int count = kvp.Value;
            if (set == null) continue;

            string name = !string.IsNullOrWhiteSpace(set.displayName) ? set.displayName : set.setId;
            if (string.IsNullOrWhiteSpace(name)) name = set.name;

            Debug.Log($"[QA] {name}: {count}");
        }
    }

    [MenuItem("Tools/Abyssbound/QA/Print Active Set Bonus Keys")]
    public static void PrintActiveSetBonusKeys()
    {
        if (!EnsurePlayMode())
            return;

        var keys = new List<string>(8);

        // We don't care about totals here; just collect active tier keys.
        int dmg = 0;
        int def = 0;
        int hp = 0;
        try
        {
            SetBonusRuntime.AccumulateActiveSetBonuses(ref dmg, ref def, ref hp, keys);
        }
        catch { }

        if (keys.Count == 0)
        {
            Debug.Log("[QA] No active set bonus tiers.");
            return;
        }

        keys.Sort(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < keys.Count; i++)
            Debug.Log($"[QA] Active Set Bonus: {keys[i]}");
    }

    [MenuItem("Tools/Abyssbound/QA/Give Starter Set (Abyssal Initiate) _F5")]
    public static void GiveStarterSet()
    {
        if (!EnsurePlayMode())
            return;

        if (!EnsureStarterSetAssets(out var set, out var pieces))
            return;

        int ilvl = GetQaItemLevelOrDefault();

        if (!TryFindInventory(out var inventory))
            return;

        var registry = LootRegistryRuntime.GetOrCreate();
        registry.BuildIfNeeded();

        int granted = 0;
        for (int i = 0; i < pieces.Count; i++)
        {
            var baseItem = pieces[i];
            if (baseItem == null) continue;

            registry.RegisterOrUpdateItem(baseItem);

            var inst = new ItemInstance
            {
                baseItemId = baseItem.id,
                rarityId = "Common",
                itemLevel = Mathf.Max(1, ilvl),
                baseScalar = 1f,
            };

            var rolledId = registry.RegisterRolledInstance(inst);
            if (string.IsNullOrWhiteSpace(rolledId))
                continue;

            inventory.Add(rolledId, 1);
            granted++;
        }

        Debug.Log($"[QA] Granted {SetDisplayName} set ({granted} items) ilvl={Mathf.Max(1, ilvl)}");
    }

    [MenuItem("Tools/Abyssbound/QA/Give + Equip Starter Set (Abyssal Initiate) #F5")]
    public static void GiveAndEquipStarterSet()
    {
        if (!EnsurePlayMode())
            return;

        if (!EnsureStarterSetAssets(out var set, out var pieces))
            return;

        int ilvl = GetQaItemLevelOrDefault();

        if (!TryFindInventory(out var inventory))
            return;

        if (!TryFindEquipment(out var equipment))
            return;

        var registry = LootRegistryRuntime.GetOrCreate();
        registry.BuildIfNeeded();

        var rolledIds = new List<string>(pieces.Count);

        int granted = 0;
        for (int i = 0; i < pieces.Count; i++)
        {
            var baseItem = pieces[i];
            if (baseItem == null) continue;

            registry.RegisterOrUpdateItem(baseItem);

            var inst = new ItemInstance
            {
                baseItemId = baseItem.id,
                rarityId = "Common",
                itemLevel = Mathf.Max(1, ilvl),
                baseScalar = 1f,
            };

            var rolledId = registry.RegisterRolledInstance(inst);
            if (string.IsNullOrWhiteSpace(rolledId))
                continue;

            inventory.Add(rolledId, 1);
            rolledIds.Add(rolledId);
            granted++;
        }

        // Equip each granted piece using normal inventory-consuming equip logic.
        for (int i = 0; i < rolledIds.Count; i++)
        {
            var id = rolledIds[i];
            if (string.IsNullOrWhiteSpace(id)) continue;
            equipment.TryEquipFromInventory(inventory, resolve: null, itemId: id, out _);
        }

        Debug.Log($"[QA] Granted+Equipped {SetDisplayName} set ({granted} items) ilvl={Mathf.Max(1, ilvl)}");
    }

    private static bool EnsurePlayMode()
    {
        if (Application.isPlaying)
            return true;

        if (!s_warnedNotPlaying)
        {
            s_warnedNotPlaying = true;
            Debug.LogWarning("[QA] Enter Play Mode first.");
        }

        return false;
    }

    private static int GetQaItemLevelOrDefault()
    {
        try
        {
            if (LootQaSettings.TryGetItemLevelOverride(out var lvl, out _))
                return Mathf.Max(1, lvl);
        }
        catch { }

        return 1;
    }

    private static bool TryFindInventory(out PlayerInventory inventory)
    {
        inventory = null;
        try
        {
#if UNITY_2022_2_OR_NEWER
            inventory = UnityEngine.Object.FindFirstObjectByType<PlayerInventory>();
#else
            inventory = UnityEngine.Object.FindObjectOfType<PlayerInventory>();
#endif
        }
        catch { inventory = null; }

        if (inventory != null)
            return true;

        Debug.LogWarning("[QA] No PlayerInventory found in the active scene.");
        return false;
    }

    private static bool TryFindEquipment(out PlayerEquipment equipment)
    {
        equipment = null;
        try
        {
#if UNITY_2022_2_OR_NEWER
            equipment = UnityEngine.Object.FindFirstObjectByType<PlayerEquipment>();
#else
            equipment = UnityEngine.Object.FindObjectOfType<PlayerEquipment>();
#endif
        }
        catch { equipment = null; }

        if (equipment != null)
            return true;

        Debug.LogWarning("[QA] No PlayerEquipment found in the active scene.");
        return false;
    }

    private static bool EnsureStarterSetAssets(out ItemSetDefinitionSO set, out List<ItemDefinitionSO> pieces)
    {
        set = null;
        pieces = new List<ItemDefinitionSO>(3);

        EnsureFolder("Assets/GameData");
        EnsureFolder("Assets/GameData/Sets");
        EnsureFolder("Assets/GameData/Loot");
        EnsureFolder(StarterSetRoot);

        set = LoadOrCreate<ItemSetDefinitionSO>(SetAssetPath);
        if (set == null)
        {
            Debug.LogWarning("[QA] Failed to create/load ItemSetDefinitionSO.");
            return false;
        }

        set.setId = SetId;
        set.displayName = SetDisplayName;

        // Editor-time upgrade/defaulting: ensure Phase 2 bonus tiers exist.
        // Do not overwrite if a designer has already configured tiers.
        EnsureAbyssalInitiateBonusTiersIfMissing(set);

        var helm = LoadOrCreate<ItemDefinitionSO>(HelmAssetPath);
        var chest = LoadOrCreate<ItemDefinitionSO>(ChestAssetPath);
        var legs = LoadOrCreate<ItemDefinitionSO>(LegsAssetPath);

        var genericArmorIcon = LoadSpriteAtPath(IconChestPath);

        ConfigurePiece(helm, "Starter_Helm", "Abyssal Initiate Helm", EquipmentSlot.Helm, set, LoadSpriteAtPath(IconHelmPath) ?? genericArmorIcon);
        ConfigurePiece(chest, "Starter_Chest", "Abyssal Initiate Chest", EquipmentSlot.Chest, set, LoadSpriteAtPath(IconChestPath) ?? genericArmorIcon);
        ConfigurePiece(legs, "Starter_Legs", "Abyssal Initiate Legs", EquipmentSlot.Legs, set, LoadSpriteAtPath(IconLegsPath) ?? genericArmorIcon);

        pieces.Add(helm);
        pieces.Add(chest);
        pieces.Add(legs);

        set.pieces = new List<ItemDefinitionSO>(pieces);
        EditorUtility.SetDirty(set);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return true;
    }

    private static void EnsureAbyssalInitiateBonusTiersIfMissing(ItemSetDefinitionSO set)
    {
        if (set == null)
            return;

        // Only apply defaults for the known starter set.
        if (!string.Equals(set.setId, SetId, StringComparison.OrdinalIgnoreCase))
            return;

        // If already configured, leave it alone.
        if (set.bonuses != null && set.bonuses.Count > 0)
            return;

        set.bonuses = new List<ItemSetDefinitionSO.SetBonusTier>(2)
        {
            new ItemSetDefinitionSO.SetBonusTier
            {
                requiredPieces = 2,
                description = "+1 Defense",
                modifiers = new List<StatMod>
                {
                    new StatMod { stat = StatType.Defense, value = 1f, percent = false },
                }
            },
            new ItemSetDefinitionSO.SetBonusTier
            {
                requiredPieces = 3,
                description = "+5 Max Health",
                modifiers = new List<StatMod>
                {
                    new StatMod { stat = StatType.MaxHealth, value = 5f, percent = false },
                }
            },
        };

        EditorUtility.SetDirty(set);
    }

    private static void ConfigurePiece(ItemDefinitionSO item, string id, string displayName, EquipmentSlot slot, ItemSetDefinitionSO set, Sprite icon)
    {
        if (item == null)
            return;

        item.id = id;
        item.displayName = displayName;
        item.slot = slot;
        item.set = set;
        if (icon != null)
            item.icon = icon;

        item.occupiesSlots ??= new List<EquipmentSlot>();

        // Starter armor stats (small numbers; informational + applied via PlayerHealth).
        item.baseStats = new List<StatMod>(2);
        switch (slot)
        {
            case EquipmentSlot.Helm:
                item.baseStats.Add(new StatMod { stat = StatType.Defense, value = 1f, percent = false });
                break;
            case EquipmentSlot.Chest:
                item.baseStats.Add(new StatMod { stat = StatType.Defense, value = 2f, percent = false });
                item.baseStats.Add(new StatMod { stat = StatType.MaxHealth, value = 5f, percent = false });
                break;
            case EquipmentSlot.Legs:
                item.baseStats.Add(new StatMod { stat = StatType.Defense, value = 1f, percent = false });
                break;
        }

        item.allowedAffixTags ??= new List<AffixTag>();

        EditorUtility.SetDirty(item);
    }

    private static Sprite LoadSpriteAtPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try { return AssetDatabase.LoadAssetAtPath<Sprite>(path); }
        catch { return null; }
    }

    private static T LoadOrCreate<T>(string assetPath) where T : ScriptableObject
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (existing != null)
            return existing;

        var so = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(so, assetPath);
        EditorUtility.SetDirty(so);
        return so;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
        var name = System.IO.Path.GetFileName(path);

        if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
            return;

        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
