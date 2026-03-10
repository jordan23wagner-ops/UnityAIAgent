using UnityEngine;
using UnityEditor;
using System.IO;
using Abyss.Enemies;

namespace Abyssbound.Editor
{
    public class NexusEnemyAutoWirer
    {
        [MenuItem("👽 Nexus/Auto-Wire Abyssbound Enemies", false, 10)]
        public static void AutoWireEnemies()
        {
            // Find all EnemyDefinition assets in the project
            string[] guids = AssetDatabase.FindAssets("t:EnemyDefinition");
            if (guids.Length == 0)
            {
                Debug.LogWarning("👽 Nexus: No EnemyDefinitions found in the project. Are you sure they exist?");
                return;
            }

            string prefabsFolderPath = "Assets/Game/Prefabs/Enemies";
            
            // Create the directory if it doesn't exist
            if (!AssetDatabase.IsValidFolder(prefabsFolderPath))
            {
                string fullPath = Path.Combine(Application.dataPath, "Game/Prefabs/Enemies");
                if (!Directory.Exists(fullPath))
                {
                    Directory.CreateDirectory(fullPath);
                }
                AssetDatabase.Refresh();
            }

            int count = 0;

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                EnemyDefinition def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(assetPath);
                
                if (def == null) continue;

                string prefabPath = $"{prefabsFolderPath}/{def.name}_Prefab.prefab";
                
                // If it already exists, skip to prevent overwriting custom work
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                {
                    Debug.Log($"👽 Nexus: Prefab already exists for {def.name}, skipping.");
                    continue;
                }

                // 1. Create a temporary capsule to act as our base mesh/collider
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = def.name;

                // 2. Configure base physics for top-down ARPG
                CapsuleCollider col = go.GetComponent<CapsuleCollider>();
                col.center = new Vector3(0, 1f, 0);
                col.height = 2f;
                
                Rigidbody rb = go.AddComponent<Rigidbody>();
                rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ; 
                rb.mass = 50f;

                // 3. Attach Combat & AI components automatically
                EnemyHealth health = go.AddComponent<EnemyHealth>();
                go.AddComponent<EnemyAggroChase>();
                go.AddComponent<EnemyMeleeAttack>();
                
                // 4. Attach Looting
                LootDropOnDeath looter = go.AddComponent<LootDropOnDeath>();

                // 5. Stylize based on name for instant visual feedback in-editor
                Renderer ren = go.GetComponent<Renderer>();
                if (ren != null)
                {
                    Material mat = new Material(Shader.Find("Standard"));
                    if (def.name.ToLower().Contains("boss") || def.name.ToLower().Contains("chieftain"))
                    {
                        go.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
                        mat.color = new Color(0.8f, 0.1f, 0.1f); // Dark Red
                    }
                    else if (def.name.ToLower().Contains("goblin"))
                    {
                        go.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
                        mat.color = new Color(0.2f, 0.6f, 0.2f); // Goblin Green
                    }
                    else if (def.name.ToLower().Contains("skeleton"))
                    {
                        mat.color = new Color(0.8f, 0.8f, 0.8f); // Bone White
                    }
                    ren.sharedMaterial = mat;
                }

                // 6. Link the prefab back to the definition asset automatically!
                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                def.prefab = savedPrefab;
                EditorUtility.SetDirty(def);

                // Clean up the temp object from the scene
                GameObject.DestroyImmediate(go);
                
                count++;
            }

            // Save the asset modifications
            AssetDatabase.SaveAssets();
            Debug.Log($"👽 Nexus: Successfully Auto-Wired {count} Enemy Prefabs! Linked directly to their Definitions.");
        }
    }
}
