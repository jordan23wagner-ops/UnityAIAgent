using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Abyss.Items;
using Abyss.Shop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

using ShopItemType = Abyss.Items.ItemType;
using ShopItemDefinition = Abyss.Items.ItemDefinition;
using AbyssItemRarity = Abyss.Items.ItemRarity;

public static class CreateDefaultShopAssetsEditor
{
    [MenuItem("Tools/Abyss/Create Default Shop Assets")]
    public static void CreateDefaults()
    {
        EnsureFolder("Assets/Abyss/Items");
        EnsureFolder("Assets/Abyss/Items/Definitions");
        EnsureFolder("Assets/Abyss/Shops");
        EnsureFolder("Assets/Abyss/Shops/Inventories");

        var defsFolder = "Assets/Abyss/Items/Definitions";
        var invFolder = "Assets/Abyss/Shops/Inventories";

        // Item definitions
        var healthPotion = EnsureItem(defsFolder + "/Item_HealthPotion.asset", "potion_health", "Health Potion", "Restores health.", ShopItemType.Consumable, 10);
        var manaPotion = EnsureItem(defsFolder + "/Item_ManaPotion.asset", "potion_mana", "Mana Potion", "Restores mana.", ShopItemType.Consumable, 12);
        var townScroll = EnsureItem(defsFolder + "/Item_TownScroll.asset", "scroll_town", "Town Scroll", "Returns you to town.", ShopItemType.Consumable, 25);

        // Weapons are equippable: ensure they have an EquipmentSlot and handedness so the inventory Equip action is available.
        var bronzeSword = EnsureItem(defsFolder + "/Item_BronzeSword.asset", "weapon_bronze_sword", "Bronze Sword", "A basic starter sword.", ShopItemType.Weapon, 80, EquipmentSlot.RightHand, WeaponHandedness.OneHanded);
        var trainingBow = EnsureItem(defsFolder + "/Item_TrainingBow.asset", "weapon_training_bow", "Training Bow", "A basic starter bow.", ShopItemType.Weapon, 90, EquipmentSlot.RightHand, WeaponHandedness.TwoHanded);
        var apprenticeStaff = EnsureItem(defsFolder + "/Item_ApprenticeStaff.asset", "weapon_apprentice_staff", "Apprentice Staff", "A basic starter staff.", ShopItemType.Weapon, 100, EquipmentSlot.RightHand, WeaponHandedness.TwoHanded);

        // Basic armor (equippable by slot; itemType can remain Misc).
        var basicHelm = EnsureItem(defsFolder + "/Item_BasicHelm.asset", "armor_basic_helm", "Basic Helm", "Simple starter head protection.", ShopItemType.Misc, 35, EquipmentSlot.Helm, WeaponHandedness.None);
        var basicChest = EnsureItem(defsFolder + "/Item_BasicChest.asset", "armor_basic_chest", "Basic Chest", "Simple starter body protection.", ShopItemType.Misc, 60, EquipmentSlot.Chest, WeaponHandedness.None);
        var basicLegs = EnsureItem(defsFolder + "/Item_BasicLegs.asset", "armor_basic_legs", "Basic Legs", "Simple starter leg protection.", ShopItemType.Misc, 50, EquipmentSlot.Legs, WeaponHandedness.None);

        var bronzePickaxe = EnsureItem(defsFolder + "/Item_BronzePickaxe.asset", "tool_bronze_pickaxe", "Bronze Pickaxe", "Used for mining.", ShopItemType.Skilling, 60);
        var hatchet = EnsureItem(defsFolder + "/Item_Hatchet.asset", "tool_hatchet", "Hatchet", "Used for woodcutting.", ShopItemType.Skilling, 55);
        var fishingRod = EnsureItem(defsFolder + "/Item_FishingRod.asset", "tool_fishing_rod", "Fishing Rod", "Used for fishing.", ShopItemType.Skilling, 65);

        var ironOre = EnsureItem(defsFolder + "/Item_IronOre.asset", "mat_iron_ore", "Iron Ore", "A basic crafting material.", ShopItemType.Workshop, 15);
        var leather = EnsureItem(defsFolder + "/Item_Leather.asset", "mat_leather", "Leather", "A basic crafting material.", ShopItemType.Workshop, 18);
        var woodPlank = EnsureItem(defsFolder + "/Item_WoodPlank.asset", "mat_wood_plank", "Wood Plank", "A basic crafting material.", ShopItemType.Workshop, 12);

        // Inventories
        var invConsumables = EnsureInventory(invFolder + "/ShopInventory_Consumables.asset", new (ShopItemDefinition, int)[]
        {
            (healthPotion, 10),
            (manaPotion, 12),
            (townScroll, 25),
        });

        var invWeapons = EnsureInventory(invFolder + "/ShopInventory_Weapons.asset", new (ShopItemDefinition, int)[]
        {
            (bronzeSword, 80),
            (trainingBow, 95),
            (apprenticeStaff, 110),
            (basicHelm, 35),
            (basicChest, 60),
            (basicLegs, 50),
        });

        var invSkilling = EnsureInventory(invFolder + "/ShopInventory_Skilling.asset", new (ShopItemDefinition, int)[]
        {
            (bronzePickaxe, 60),
            (hatchet, 55),
            (fishingRod, 65),
        });

        var invWorkshop = EnsureInventory(invFolder + "/ShopInventory_Workshop.asset", new (ShopItemDefinition, int)[]
        {
            (ironOre, 15),
            (leather, 18),
            (woodPlank, 12),
        });

        // Best-effort assign to merchants in scene.
#if UNITY_2022_2_OR_NEWER
        var shops = UnityEngine.Object.FindObjectsByType<MerchantShop>(FindObjectsSortMode.None);
#else
        var shops = UnityEngine.Object.FindObjectsOfType<MerchantShop>();
#endif

        int assigned = 0;
        foreach (var shop in shops)
        {
            if (shop == null) continue;

            string key = (shop.MerchantName + " " + shop.gameObject.name).ToLowerInvariant();
            ShopInventory chosen = null;

            if (key.Contains("weapons")) chosen = invWeapons;
            else if (key.Contains("consumables")) chosen = invConsumables;
            else if (key.Contains("skilling")) chosen = invSkilling;
            else if (key.Contains("workshop")) chosen = invWorkshop;

            if (chosen == null) continue;

            shop.shopInventory = chosen;
            if (shop.stock != null && shop.stock.Count > 0)
                shop.stock.Clear();
            EditorUtility.SetDirty(shop);
            assigned++;
            Debug.Log($"[CreateDefaultShopAssets] Assigned {chosen.name} to {shop.gameObject.name}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log($"[CreateDefaultShopAssets] Completed. ShopsFound={shops.Length} Assigned={assigned}");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string folder = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(folder)) return;

        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, folder);
    }

    private static ShopItemDefinition EnsureItem(string assetPath, string id, string displayName, string description, ShopItemType type, int baseValue,
        EquipmentSlot equipSlot = EquipmentSlot.None,
        WeaponHandedness handedness = WeaponHandedness.None)
    {
        var byId = FindItemById(id);
        if (byId != null)
        {
            ApplyItemDefaults(byId, id, displayName, description, type, baseValue, equipSlot, handedness);
            return byId;
        }

        var existing = AssetDatabase.LoadAssetAtPath<ShopItemDefinition>(assetPath);
        if (existing != null)
        {
            ApplyItemDefaults(existing, id, displayName, description, type, baseValue, equipSlot, handedness);
            return existing;
        }

        var def = ScriptableObject.CreateInstance<ShopItemDefinition>();
        ApplyItemDefaults(def, id, displayName, description, type, baseValue, equipSlot, handedness);

        AssetDatabase.CreateAsset(def, assetPath);
        EditorUtility.SetDirty(def);
        Debug.Log($"[CreateDefaultShopAssets] Created ItemDefinition {displayName} ({id})");
        return def;
    }

    private static void ApplyItemDefaults(ShopItemDefinition def, string id, string displayName, string description, ShopItemType type, int baseValue,
        EquipmentSlot equipSlot,
        WeaponHandedness handedness)
    {
        if (def == null) return;

        if (!string.IsNullOrWhiteSpace(id) && !string.Equals(def.itemId, id, StringComparison.OrdinalIgnoreCase))
            def.itemId = id;

        if (string.IsNullOrWhiteSpace(def.displayName))
            def.displayName = displayName;

        if (string.IsNullOrWhiteSpace(def.description))
            def.description = description;

        // Only set type/value if they are unset/invalid to avoid clobbering intentional tweaks.
        if (def.itemType == ShopItemType.None)
            def.itemType = type;

        if (def.baseValue <= 0)
            def.baseValue = baseValue;

        // The equip flow keys off equipmentSlot != None.
        if (equipSlot != EquipmentSlot.None && def.equipmentSlot == EquipmentSlot.None)
            def.equipmentSlot = equipSlot;

        if (handedness != WeaponHandedness.None && def.weaponHandedness == WeaponHandedness.None)
            def.weaponHandedness = handedness;

        if (def.rarity == 0)
            def.rarity = AbyssItemRarity.Common;

        EditorUtility.SetDirty(def);
    }

    private static ShopItemDefinition FindItemById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        try
        {
            var guids = AssetDatabase.FindAssets("t:ItemDefinition");
            if (guids == null || guids.Length == 0)
                return null;

            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                var def = AssetDatabase.LoadAssetAtPath<ShopItemDefinition>(path);
                if (def == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(def.itemId) && string.Equals(def.itemId, id, StringComparison.OrdinalIgnoreCase))
                    return def;
            }
        }
        catch { }

        return null;
    }

    private static ShopInventory EnsureInventory(string assetPath, IEnumerable<(ShopItemDefinition item, int price)> entries)
    {
        var inv = AssetDatabase.LoadAssetAtPath<ShopInventory>(assetPath);
        bool created = false;

        if (inv == null)
        {
            inv = ScriptableObject.CreateInstance<ShopInventory>();
            AssetDatabase.CreateAsset(inv, assetPath);
            created = true;
        }

        inv.entries ??= new List<ShopInventory.Entry>();
        inv.entries.Clear();

        foreach (var (item, price) in entries)
        {
            if (item == null || price <= 0) continue;
            inv.entries.Add(new ShopInventory.Entry { item = item, price = price });
        }

        EditorUtility.SetDirty(inv);
        if (created)
            Debug.Log($"[CreateDefaultShopAssets] Created ShopInventory {inv.name}");
        else
            Debug.Log($"[CreateDefaultShopAssets] Updated ShopInventory {inv.name}");

        return inv;
    }
}
