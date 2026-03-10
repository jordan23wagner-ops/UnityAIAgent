using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using Abyss.Enemies;

public class EnemyJSONImporter : EditorWindow
{
    [MenuItem("Tools/Abyssbound/Import Enemies from JSON")]
    public static void ImportEnemies()
    {
        string jsonPath = Path.Combine(Application.dataPath, "GameData/NewEnemies.json");
        if (!File.Exists(jsonPath))
        {
            Debug.LogError($"JSON file not found at: {jsonPath}");
            return;
        }

        string jsonContent = File.ReadAllText(jsonPath);
        string wrappedJson = "{\"enemies\":" + jsonContent + "}";
        EnemyContainer container = JsonUtility.FromJson<EnemyContainer>(wrappedJson);

        if (container == null || container.enemies == null)
        {
            Debug.LogError("Failed to parse Enemy JSON.");
            return;
        }

        foreach (var data in container.enemies)
        {
            CreateOrUpdateEnemy(data);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Successfully imported {container.enemies.Count} enemies from JSON!");
    }

    private static void CreateOrUpdateEnemy(SimpleEnemyData data)
    {
        string folderPath = "Assets/GameData/Enemies/Generated";
        if (!AssetDatabase.IsValidFolder("Assets/GameData/Enemies"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/GameData"))
            {
                 // Ensure GameData exists first, though items made it
            }
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "GameData/Enemies"));
        }
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "GameData/Enemies/Generated"));
        }

        string assetPath = $"{folderPath}/{data.enemyId}.asset";
        EnemyDefinition enemy = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(assetPath);

        if (enemy == null)
        {
            enemy = ScriptableObject.CreateInstance<EnemyDefinition>();
            AssetDatabase.CreateAsset(enemy, assetPath);
        }

        enemy.enemyId = data.enemyId;
        enemy.displayName = data.displayName;
        enemy.description = data.description;
        enemy.maxHealth = data.maxHealth;
        enemy.damage = data.damage;
        enemy.moveSpeed = data.moveSpeed;
        enemy.attackRange = data.attackRange;
        enemy.attackCooldown = data.attackCooldown;
        enemy.expReward = data.expReward;
        enemy.detectionRadius = data.detectionRadius;
        enemy.chaseRadius = data.chaseRadius;

        EditorUtility.SetDirty(enemy);
    }

    [System.Serializable]
    private class EnemyContainer
    {
        public List<SimpleEnemyData> enemies;
    }

    [System.Serializable]
    private class SimpleEnemyData
    {
        public string enemyId;
        public string displayName;
        public string description;
        public float maxHealth;
        public float damage;
        public float moveSpeed;
        public float attackRange;
        public float attackCooldown;
        public float expReward;
        public float detectionRadius;
        public float chaseRadius;
    }
}