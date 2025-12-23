#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Abyss.Items;
using Abyssbound.Loot;
using UnityEditor;
using UnityEngine;

public static class CreateStarterLootContent
{
    private const string Root = "Assets/Resources/Loot";

    [MenuItem("Tools/Abyssbound/Loot/Create Starter Loot Content")]
    public static void Create()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder(Root);

        // Registries
        var itemReg = CreateOrLoad<ItemRegistrySO>($"{Root}/ItemRegistry.asset");
        var rarityReg = CreateOrLoad<RarityRegistrySO>($"{Root}/RarityRegistry.asset");
        var affixReg = CreateOrLoad<AffixRegistrySO>($"{Root}/AffixRegistry.asset");
        var bootstrap = CreateOrLoad<LootRegistryBootstrapSO>($"{Root}/Bootstrap.asset");

        bootstrap.itemRegistry = itemReg;
        bootstrap.rarityRegistry = rarityReg;
        bootstrap.affixRegistry = affixReg;
        EditorUtility.SetDirty(bootstrap);

        // Rarities
        var rarities = new List<(string id, string name, int order, bool enabled, bool special, int amin, int amax, float smin, float smax)>
        {
            ("Common","Common",0,true,false,0,0,1f,1f),
            ("Uncommon","Uncommon",1,true,false,0,1,1f,1.05f),
            ("Magic","Magic",2,true,false,1,2,1.05f,1.15f),
            ("Rare","Rare",3,true,false,2,3,1.1f,1.25f),
            ("Epic","Epic",4,true,false,3,4,1.2f,1.4f),
            ("Legendary","Legendary",5,true,false,4,5,1.35f,1.6f),
            ("Set","Set",6,false,true,4,6,1.4f,1.7f),
            ("Unique","Unique",7,false,true,4,6,1.5f,1.8f),
            ("Mythic","Mythic",8,false,true,5,7,1.6f,2.0f),
            ("Radiant","Radiant",9,false,true,5,7,1.7f,2.2f),
        };

        rarityReg.rarities ??= new List<RarityDefinitionSO>();
        var rarityById = new Dictionary<string, RarityDefinitionSO>(StringComparer.OrdinalIgnoreCase);

        foreach (var r in rarityReg.rarities)
            if (r != null && !string.IsNullOrWhiteSpace(r.id) && !rarityById.ContainsKey(r.id))
                rarityById[r.id] = r;

        foreach (var r in rarities)
        {
            var assetPath = $"{Root}/Rarities/Rarity_{r.id}.asset";
            EnsureFolder($"{Root}/Rarities");
            var so = CreateOrLoad<RarityDefinitionSO>(assetPath);
            so.id = r.id;
            so.displayName = r.name;
            so.sortOrder = r.order;
            so.enabledByDefault = r.enabled;
            so.isSpecial = r.special;
            so.affixMin = r.amin;
            so.affixMax = r.amax;
            so.scalarMin = r.smin;
            so.scalarMax = r.smax;
            EditorUtility.SetDirty(so);

            if (!rarityById.ContainsKey(so.id))
            {
                rarityReg.rarities.Add(so);
                rarityById[so.id] = so;
            }
        }
        EditorUtility.SetDirty(rarityReg);

        // Affixes (6-10)
        EnsureFolder($"{Root}/Affixes");
        affixReg.affixes ??= new List<AffixDefinitionSO>();
        var existingAffixIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in affixReg.affixes)
            if (a != null && !string.IsNullOrWhiteSpace(a.id))
                existingAffixIds.Add(a.id);

        void AddAffix(string id, string name, StatType stat, float min, float max, params AffixTag[] tags)
        {
            var path = $"{Root}/Affixes/Affix_{id}.asset";
            var so = CreateOrLoad<AffixDefinitionSO>(path);
            so.id = id;
            so.displayName = name;
            so.stat = stat;
            so.minRoll = min;
            so.maxRoll = max;
            so.percent = false;
            so.tags = new List<AffixTag>(tags);
            so.allowedSlots = new List<EquipmentSlot>();
            EditorUtility.SetDirty(so);

            if (!existingAffixIds.Contains(id))
            {
                affixReg.affixes.Add(so);
                existingAffixIds.Add(id);
            }
        }

        AddAffix("Power","of Power",StatType.MeleeDamage,1,4,AffixTag.WeaponMelee);
        AddAffix("Precision","of Precision",StatType.RangedDamage,1,4,AffixTag.WeaponRanged);
        AddAffix("Sorcery","of Sorcery",StatType.MagicDamage,1,4,AffixTag.WeaponMagic);
        AddAffix("Fortitude","of Fortitude",StatType.MaxHealth,5,20,AffixTag.Armor,AffixTag.Jewelry);
        AddAffix("Bulwark","of Bulwark",StatType.Defense,1,4,AffixTag.Armor);
        AddAffix("Swiftness","of Swiftness",StatType.MoveSpeed,0.1f,0.35f,AffixTag.Armor,AffixTag.Jewelry);
        AddAffix("Fury","of Fury",StatType.AttackSpeed,0.1f,0.35f,AffixTag.WeaponMelee,AffixTag.WeaponRanged,AffixTag.WeaponMagic);
        AddAffix("Strength","of Strength",StatType.Strength,1,3,AffixTag.Any);
        AddAffix("AttackSkill","of the Duelist",StatType.Attack,1,3,AffixTag.WeaponMelee);
        AddAffix("MagicSkill","of the Arcanist",StatType.MagicSkill,1,3,AffixTag.WeaponMagic);

        EditorUtility.SetDirty(affixReg);

        // Base items (3)
        EnsureFolder($"{Root}/Items");
        itemReg.items ??= new List<ItemDefinitionSO>();
        var existingItemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var it in itemReg.items)
            if (it != null && !string.IsNullOrWhiteSpace(it.id))
                existingItemIds.Add(it.id);

        ItemDefinitionSO AddItem(string id, string name, EquipmentSlot slot, List<StatMod> baseStats, params AffixTag[] tags)
        {
            var path = $"{Root}/Items/Item_{id}.asset";
            var so = CreateOrLoad<ItemDefinitionSO>(path);
            so.id = id;
            so.displayName = name;
            so.slot = slot;
            so.baseStats = baseStats;
            so.allowedAffixTags = new List<AffixTag>(tags);
            so.setId = "";
            EditorUtility.SetDirty(so);

            if (!existingItemIds.Contains(id))
            {
                itemReg.items.Add(so);
                existingItemIds.Add(id);
            }

            return so;
        }

        void MarkTwoHanded(ItemDefinitionSO so)
        {
            if (so == null) return;
            try
            {
                so.occupiesSlots = new List<EquipmentSlot> { EquipmentSlot.RightHand, EquipmentSlot.LeftHand };
                EditorUtility.SetDirty(so);
            }
            catch { }
        }

        var sword = AddItem(
            "Starter_Sword",
            "Rusty Sword",
            EquipmentSlot.RightHand,
            new List<StatMod> { new StatMod { stat = StatType.MeleeDamage, value = 2, percent = false } },
            AffixTag.WeaponMelee);

        var bow = AddItem(
            "Starter_Bow",
            "Simple Bow",
            EquipmentSlot.RightHand,
            new List<StatMod> { new StatMod { stat = StatType.RangedDamage, value = 2, percent = false } },
            AffixTag.WeaponRanged);

        // 2H QA weapons (occupy both hands)
        var greatsword = AddItem(
            "QA_Greatsword_2H",
            "Greatsword",
            EquipmentSlot.RightHand,
            new List<StatMod> { new StatMod { stat = StatType.MeleeDamage, value = 4, percent = false } },
            AffixTag.WeaponMelee);
        MarkTwoHanded(greatsword);

        var longbow = AddItem(
            "QA_Longbow_2H",
            "Longbow",
            EquipmentSlot.RightHand,
            new List<StatMod> { new StatMod { stat = StatType.RangedDamage, value = 4, percent = false } },
            AffixTag.WeaponRanged);
        MarkTwoHanded(longbow);

        var staff = AddItem(
            "QA_Staff_2H",
            "Staff",
            EquipmentSlot.RightHand,
            new List<StatMod> { new StatMod { stat = StatType.MagicDamage, value = 4, percent = false } },
            AffixTag.WeaponMagic);
        MarkTwoHanded(staff);

        var chest = AddItem(
            "Starter_Chest",
            "Worn Chestpiece",
            EquipmentSlot.Chest,
            new List<StatMod>
            {
                new StatMod { stat = StatType.Defense, value = 1, percent = false },
                new StatMod { stat = StatType.MaxHealth, value = 5, percent = false },
            },
            AffixTag.Armor);

        EditorUtility.SetDirty(itemReg);

        // Starter loot table
        EnsureFolder($"{Root}/Tables");
        var table = CreateOrLoad<LootTableSO>($"{Root}/Tables/LootTable_Starter.asset");
        table.id = "Starter";

        table.items = new List<LootTableSO.WeightedItemEntry>
        {
            new LootTableSO.WeightedItemEntry{ item = sword, weight = 1f },
            new LootTableSO.WeightedItemEntry{ item = bow, weight = 1f },
            new LootTableSO.WeightedItemEntry{ item = greatsword, weight = 0.5f },
            new LootTableSO.WeightedItemEntry{ item = longbow, weight = 0.5f },
            new LootTableSO.WeightedItemEntry{ item = staff, weight = 0.5f },
            new LootTableSO.WeightedItemEntry{ item = chest, weight = 1f },
        };

        // Only enabled-by-default rarities in the table by default.
        table.rarities = new List<LootTableSO.WeightedRarityEntry>();
        foreach (var r in rarityReg.rarities)
        {
            if (r == null) continue;
            if (!r.enabledByDefault) continue;

            float w = r.id.Equals("Common", StringComparison.OrdinalIgnoreCase) ? 70f :
                      r.id.Equals("Uncommon", StringComparison.OrdinalIgnoreCase) ? 22f :
                      r.id.Equals("Magic", StringComparison.OrdinalIgnoreCase) ? 6f :
                      r.id.Equals("Rare", StringComparison.OrdinalIgnoreCase) ? 1.8f :
                      r.id.Equals("Epic", StringComparison.OrdinalIgnoreCase) ? 0.18f :
                      0.02f;

            table.rarities.Add(new LootTableSO.WeightedRarityEntry { rarity = r, weight = w });
        }

        table.affixPoolOverride = new List<AffixDefinitionSO>();
        EditorUtility.SetDirty(table);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[Loot] Starter loot content created/updated under Assets/Resources/Loot");
        Selection.activeObject = table;
    }

    private static T CreateOrLoad<T>(string assetPath) where T : ScriptableObject
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (existing != null) return existing;

        var so = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(so, assetPath);
        return so;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
        var name = System.IO.Path.GetFileName(path);

        if (!string.IsNullOrWhiteSpace(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        if (!string.IsNullOrWhiteSpace(parent))
            AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
