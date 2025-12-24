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
    private const string BootstrapPath = "Assets/Resources/Loot/Bootstrap.asset";

    [MenuItem("Tools/Abyssbound/Content/Create Starter Loot Content")]
    public static void Create()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder(Root);

        // Ensure the bootstrap path is available and not occupied by a wrong asset type.
        try
        {
            var main = AssetDatabase.LoadMainAssetAtPath(BootstrapPath);
            if (main != null && main is not LootRegistryBootstrapSO)
            {
                var moveTo = "Assets/Resources/Loot/Bootstrap_WrongType.asset";
                AssetDatabase.MoveAsset(BootstrapPath, moveTo);
            }
        }
        catch { }

        // Registries
        var itemReg = CreateOrLoad<ItemRegistrySO>($"{Root}/ItemRegistry.asset");
        var rarityReg = CreateOrLoad<RarityRegistrySO>($"{Root}/RarityRegistry.asset");
        var affixReg = CreateOrLoad<AffixRegistrySO>($"{Root}/AffixRegistry.asset");
        var bootstrap = CreateOrLoad<LootRegistryBootstrapSO>(BootstrapPath);

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

        void AddAffix(string id, string name, StatType stat, float min, float max, int weight, bool percent, List<AffixDefinitionSO.AffixTier> tiers, params AffixTag[] tags)
        {
            var path = $"{Root}/Affixes/Affix_{id}.asset";
            var so = CreateOrLoad<AffixDefinitionSO>(path);
            so.id = id;
            so.displayName = name;
            so.weight = weight;
            so.stat = stat;
            so.minRoll = min;
            so.maxRoll = max;
            so.tiers = tiers ?? new List<AffixDefinitionSO.AffixTier>();
            so.percent = percent;
            so.tags = new List<AffixTag>(tags);
            so.allowedSlots = new List<EquipmentSlot>();
            EditorUtility.SetDirty(so);

            if (!existingAffixIds.Contains(id))
            {
                affixReg.affixes.Add(so);
                existingAffixIds.Add(id);
            }
        }

        // Recommended default weights (baseline 100):
        // Damage 100, Defense/MaxHealth 90, Skills 60, AttackSpeed 35, MoveSpeed 25.
        // Tiering: 3 tiers (1-5, 6-12, 13-20) with modest early game scaling.
        List<AffixDefinitionSO.AffixTier> T(float a1, float b1, float a2, float b2, float a3, float b3) => new()
        {
            new AffixDefinitionSO.AffixTier{ minItemLevel = 1, maxItemLevel = 5,  minRoll = a1, maxRoll = b1 },
            new AffixDefinitionSO.AffixTier{ minItemLevel = 6, maxItemLevel = 12, minRoll = a2, maxRoll = b2 },
            new AffixDefinitionSO.AffixTier{ minItemLevel = 13, maxItemLevel = 20, minRoll = a3, maxRoll = b3 },
        };

        AddAffix("Power","of Power",StatType.MeleeDamage,1,4,100,false, T(1,3, 2,5, 3,7), AffixTag.WeaponMelee);
        AddAffix("Precision","of Precision",StatType.RangedDamage,1,4,100,false, T(1,3, 2,5, 3,7), AffixTag.WeaponRanged);
        AddAffix("Sorcery","of Sorcery",StatType.MagicDamage,1,4,100,false, T(1,3, 2,5, 3,7), AffixTag.WeaponMagic);

        AddAffix("Fortitude","of Fortitude",StatType.MaxHealth,5,20,90,false, T(5,10, 10,20, 18,35), AffixTag.Armor, AffixTag.Jewelry);
        AddAffix("Bulwark","of Bulwark",StatType.Defense,1,4,90,false, T(1,2, 2,4, 3,6), AffixTag.Armor);

        // Speed rolls are expressed as percent points (e.g., 2 means 2%).
        AddAffix("Swiftness","of Swiftness",StatType.MoveSpeed,1,4,25,true, T(1,2, 2,3, 3,4), AffixTag.Armor, AffixTag.Jewelry);
        AddAffix("Fury","of Fury",StatType.AttackSpeed,1,4,35,true, T(1,2, 2,3, 3,4), AffixTag.WeaponMelee, AffixTag.WeaponRanged, AffixTag.WeaponMagic);

        AddAffix("Strength","of Strength",StatType.Strength,1,3,60,false, T(1,1, 1,2, 2,3), AffixTag.Any);
        AddAffix("AttackSkill","of the Duelist",StatType.Attack,1,3,60,false, T(1,1, 1,2, 2,3), AffixTag.WeaponMelee);
        AddAffix("MagicSkill","of the Arcanist",StatType.MagicSkill,1,3,60,false, T(1,1, 1,2, 2,3), AffixTag.WeaponMagic);

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

        // Zone 1 tuning tables (assets only)
        CreateOrUpdateZone1Tables(rarityReg, table);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[Loot] Starter loot content created/updated under Assets/Resources/Loot");
        Selection.activeObject = table;
    }

    private static void CreateOrUpdateZone1Tables(RarityRegistrySO rarityReg, LootTableSO starterTable)
    {
        if (starterTable == null) return;

        EnsureFolder($"{Root}/Tables");

        var rarityById = new Dictionary<string, RarityDefinitionSO>(StringComparer.OrdinalIgnoreCase);
        if (rarityReg != null && rarityReg.rarities != null)
        {
            for (int i = 0; i < rarityReg.rarities.Count; i++)
            {
                var r = rarityReg.rarities[i];
                if (r == null || string.IsNullOrWhiteSpace(r.id)) continue;
                if (!rarityById.ContainsKey(r.id)) rarityById[r.id] = r;
            }
        }

        void ApplyRarityWeights(LootTableSO t, (string id, float w)[] weights)
        {
            if (t == null) return;
            t.rarities = new List<LootTableSO.WeightedRarityEntry>(weights.Length);

            for (int i = 0; i < weights.Length; i++)
            {
                var (id, w) = weights[i];
                if (string.IsNullOrWhiteSpace(id)) continue;
                if (!rarityById.TryGetValue(id, out var r) || r == null) continue;
                t.rarities.Add(new LootTableSO.WeightedRarityEntry { rarity = r, weight = w });
            }
        }

        LootTableSO CreateOrLoadTable(string fileName, string id)
        {
            var t = CreateOrLoad<LootTableSO>($"{Root}/Tables/{fileName}.asset");
            t.id = id;
            // Reuse starter items by default for Zone1 tuning.
            t.items = starterTable.items != null
                ? new List<LootTableSO.WeightedItemEntry>(starterTable.items)
                : new List<LootTableSO.WeightedItemEntry>();
            t.affixPoolOverride = new List<AffixDefinitionSO>();
            EditorUtility.SetDirty(t);
            return t;
        }

        // Only Common -> Legendary used
        var trash = CreateOrLoadTable("Zone1_Trash", "Zone1_Trash");
        ApplyRarityWeights(trash, new[]
        {
            ("Common", 78f),
            ("Uncommon", 18f),
            ("Magic", 3.5f),
            ("Rare", 0.5f),
            ("Epic", 0f),
            ("Legendary", 0f),
        });

        var elite = CreateOrLoadTable("Zone1_Elite", "Zone1_Elite");
        ApplyRarityWeights(elite, new[]
        {
            ("Common", 55f),
            ("Uncommon", 30f),
            ("Magic", 12f),
            ("Rare", 2.5f),
            ("Epic", 0.5f),
            ("Legendary", 0f),
        });

        var boss = CreateOrLoadTable("Zone1_Boss", "Zone1_Boss");
        ApplyRarityWeights(boss, new[]
        {
            ("Common", 35f),
            ("Uncommon", 35f),
            ("Magic", 20f),
            ("Rare", 8f),
            ("Epic", 1.8f),
            ("Legendary", 0.2f),
        });
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
