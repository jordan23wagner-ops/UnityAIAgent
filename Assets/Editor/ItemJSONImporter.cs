using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using Abyss.Items;

public class ItemJSONImporter : EditorWindow
{
    [MenuItem("Tools/Abyssbound/Import Items from JSON")]
    public static void ImportItems()
    {
        string jsonPath = Path.Combine(Application.dataPath, "GameData/NewItems.json");
        if (!File.Exists(jsonPath))
        {
            Debug.LogError($"JSON file not found at: {jsonPath}");
            return;
        }

        string jsonContent = File.ReadAllText(jsonPath);
        
        // Simple wrapper because Unity's JsonUtility hates top-level arrays
        string wrappedJson = "{\"items\":" + jsonContent + "}";
        ItemContainer container = JsonUtility.FromJson<ItemContainer>(wrappedJson);

        if (container == null || container.items == null)
        {
            Debug.LogError("Failed to parse JSON.");
            return;
        }

        foreach (var itemData in container.items)
        {
            CreateOrUpdateItem(itemData);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Successfully imported {container.items.Count} items from JSON!");
    }

    private static void CreateOrUpdateItem(SimpleItemData data)
    {
        string folderPath = "Assets/GameData/Items/Generated";
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "GameData/Items/Generated"));
        }

        string assetPath = $"{folderPath}/{data.itemId}.asset";
        ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(assetPath);

        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemDefinition>();
            AssetDatabase.CreateAsset(item, assetPath);
        }

        // Map Data
        item.itemId = data.itemId;
        item.displayName = data.displayName;
        item.description = data.description;
        item.baseValue = data.baseValue;
        item.DamageBonus = data.damageBonus;
        item.MaxHealthBonus = data.maxHealthBonus;
        item.DamageReductionFlat = data.damageReductionFlat;

        // Enums (String parsing)
        System.Enum.TryParse(data.rarity, out item.rarity);
        System.Enum.TryParse(data.itemType, out item.itemType);
        System.Enum.TryParse(data.equipmentSlot, out item.equipmentSlot);
        System.Enum.TryParse(data.weaponHandedness, out item.weaponHandedness);

        EditorUtility.SetDirty(item);
    }

    [System.Serializable]
    private class ItemContainer
    {
        public List<SimpleItemData> items;
    }

    [System.Serializable]
    private class SimpleItemData
    {
        public string itemId;
        public string displayName;
        public string description;
        public string rarity;
        public string itemType;
        public int baseValue;
        public string equipmentSlot;
        public string weaponHandedness;
        public int damageBonus;
        public int maxHealthBonus;
        public int damageReductionFlat;
    }
}