#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Abyss.Items;
using Abyssbound.Loot;
using UnityEditor;
using UnityEngine;

public static class Add2HTestItemsToInventory
{
    private const string RootFolder = "Assets/GameData/Loot/Test2H";

    private const string GreatswordId = "Test2H_TrainingGreatsword";
    private const string LongbowId = "Test2H_TrainingLongbow";
    private const string StaffId = "Test2H_TrainingStaff";

    [MenuItem("Tools/Abyssbound/QA/Add 2H Test Items To Inventory")]
    public static void AddToInventory()
    {
        if (!EnsureAssetsAndRegistries(out var baseItems, out var error))
        {
            if (!string.IsNullOrWhiteSpace(error))
                Debug.LogWarning($"[Loot QA] {error}");
            return;
        }

        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Loot QA] Enter Play Mode, then run: Tools/Abyssbound/QA/Add 2H Test Items To Inventory");
            return;
        }

        PlayerInventory inventory = null;
#if UNITY_2022_2_OR_NEWER
        inventory = UnityEngine.Object.FindFirstObjectByType<PlayerInventory>();
#else
        inventory = UnityEngine.Object.FindObjectOfType<PlayerInventory>();
#endif

        if (inventory == null)
        {
            Debug.LogWarning("[Loot QA] No PlayerInventory found in the active scene.");
            return;
        }

        var registry = LootRegistryRuntime.GetOrCreate();
        registry.BuildIfNeeded();

        for (int i = 0; i < baseItems.Count; i++)
        {
            var baseItem = baseItems[i];
            if (baseItem == null) continue;

            // Ensure runtime registry can resolve these immediately (even if it already built before assets were created).
            registry.RegisterOrUpdateItem(baseItem);

            var inst = new ItemInstance
            {
                baseItemId = baseItem.id,
                rarityId = "Common",
                itemLevel = 1,
                baseScalar = 1f,
            };

            var rolledId = registry.RegisterRolledInstance(inst);
            if (string.IsNullOrWhiteSpace(rolledId))
                continue;

            inventory.Add(rolledId, 1);
        }

        Debug.Log("[Loot QA] Added 2H test items (Greatsword, Longbow, Staff) to PlayerInventory.");
    }

    private static bool EnsureAssetsAndRegistries(out List<ItemDefinitionSO> baseItems, out string error)
    {
        baseItems = new List<ItemDefinitionSO>(3);
        error = string.Empty;

        EnsureFolder("Assets/GameData");
        EnsureFolder("Assets/GameData/Loot");
        EnsureFolder(RootFolder);

        var greatsword = CreateOrLoadItem($"{RootFolder}/Item_{GreatswordId}.asset", GreatswordId, "Training Greatsword", AffixTag.WeaponMelee, StatType.MeleeDamage);
        var longbow = CreateOrLoadItem($"{RootFolder}/Item_{LongbowId}.asset", LongbowId, "Training Longbow", AffixTag.WeaponRanged, StatType.RangedDamage);
        var staff = CreateOrLoadItem($"{RootFolder}/Item_{StaffId}.asset", StaffId, "Training Staff", AffixTag.WeaponMagic, StatType.MagicDamage);

        if (greatsword != null) baseItems.Add(greatsword);
        if (longbow != null) baseItems.Add(longbow);
        if (staff != null) baseItems.Add(staff);

        if (baseItems.Count != 3)
        {
            error = "Failed to create/load one or more test items.";
            return false;
        }

        // Ensure these items are part of the bootstrap registry (so they work with normal runtime resolution too).
        // QA should never block on missing Resources assets; auto-create minimal bootstrap/registries if absent.
        const string resourcesRoot = "Assets/Resources";
        const string lootRoot = "Assets/Resources/Loot";
        const string bootstrapPath = "Assets/Resources/Loot/Bootstrap.asset";
        const string itemRegPath = "Assets/Resources/Loot/ItemRegistry.asset";
        const string rarityRegPath = "Assets/Resources/Loot/RarityRegistry.asset";
        const string affixRegPath = "Assets/Resources/Loot/AffixRegistry.asset";

        EnsureFolder(resourcesRoot);
        EnsureFolder(lootRoot);

        static T CreateOrLoadAtPath<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var so = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(so, path);
            EditorUtility.SetDirty(so);
            return so;
        }

        var bootstrap = AssetDatabase.LoadAssetAtPath<LootRegistryBootstrapSO>(bootstrapPath);
        if (bootstrap == null)
            bootstrap = CreateOrLoadAtPath<LootRegistryBootstrapSO>(bootstrapPath);

        var itemRegistry = bootstrap.itemRegistry != null ? bootstrap.itemRegistry : AssetDatabase.LoadAssetAtPath<ItemRegistrySO>(itemRegPath);
        if (itemRegistry == null)
            itemRegistry = CreateOrLoadAtPath<ItemRegistrySO>(itemRegPath);

        var rarityRegistry = bootstrap.rarityRegistry != null ? bootstrap.rarityRegistry : AssetDatabase.LoadAssetAtPath<RarityRegistrySO>(rarityRegPath);
        if (rarityRegistry == null)
            rarityRegistry = CreateOrLoadAtPath<RarityRegistrySO>(rarityRegPath);

        var affixRegistry = bootstrap.affixRegistry != null ? bootstrap.affixRegistry : AssetDatabase.LoadAssetAtPath<AffixRegistrySO>(affixRegPath);
        if (affixRegistry == null)
            affixRegistry = CreateOrLoadAtPath<AffixRegistrySO>(affixRegPath);

        bootstrap.itemRegistry = itemRegistry;
        bootstrap.rarityRegistry = rarityRegistry;
        bootstrap.affixRegistry = affixRegistry;
        EditorUtility.SetDirty(bootstrap);

        bootstrap.itemRegistry.items ??= new List<ItemDefinitionSO>();

        bool changed = false;
        for (int i = 0; i < baseItems.Count; i++)
        {
            var it = baseItems[i];
            if (it == null) continue;

            bool exists = false;
            for (int j = 0; j < bootstrap.itemRegistry.items.Count; j++)
            {
                var existing = bootstrap.itemRegistry.items[j];
                if (existing == null) continue;
                if (string.Equals(existing.id, it.id, StringComparison.OrdinalIgnoreCase)) { exists = true; break; }
            }

            if (!exists)
            {
                bootstrap.itemRegistry.items.Add(it);
                changed = true;
            }
        }

        if (changed)
            EditorUtility.SetDirty(bootstrap.itemRegistry);

        if (changed)
            AssetDatabase.SaveAssets();

        return true;
    }

    private static ItemDefinitionSO CreateOrLoadItem(string assetPath, string id, string displayName, AffixTag weaponTag, StatType baseDamageStat)
    {
        var so = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(assetPath);
        if (so == null)
        {
            so = ScriptableObject.CreateInstance<ItemDefinitionSO>();
            AssetDatabase.CreateAsset(so, assetPath);
        }

        so.id = id;
        so.displayName = displayName;

        // Slot stays as a weapon hand; occupancy defines 2H.
        so.slot = EquipmentSlot.RightHand;
        so.occupiesSlots = new List<EquipmentSlot> { EquipmentSlot.RightHand, EquipmentSlot.LeftHand };

        so.baseStats = new List<StatMod>
        {
            new StatMod { stat = baseDamageStat, value = 1f, percent = false }
        };

        so.allowedAffixTags = new List<AffixTag> { weaponTag };
        so.setId = "";

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
