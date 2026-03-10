using UnityEngine;
using UnityEditor;
using Abyssbound.World;

public class NexusAutoBuilder : EditorWindow
{
    [MenuItem("Tools/Nexus Auto Builder/Build Basic Open World")]
    public static void BuildWorld()
    {
        // 1. Create Ground Plane
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Massive Ground Plane";
        ground.transform.localScale = new Vector3(100, 1, 100);
        ground.transform.position = Vector3.zero;
        
        // 2. Create Player
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.tag = "Player";
        player.transform.position = new Vector3(0, 1, 0);
        if (player.GetComponent<Rigidbody>() == null)
        {
            Rigidbody rb = player.AddComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        // 3. Create Camera (or move existing)
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.transform.position = new Vector3(0, 10, -10);
            mainCam.transform.rotation = Quaternion.Euler(45, 0, 0);
        }

        // 4. Create First Waypoint
        GameObject waypoint = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        waypoint.name = "Deep Wilderness Waypoint";
        waypoint.transform.position = new Vector3(20, 1, 20); // Put it far away
        
        Collider col = waypoint.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        Waypoint wpScript = waypoint.AddComponent<Waypoint>();
        wpScript.waypointName = "Deep Wilderness Ruins";

        GameObject lightObj = new GameObject("AuraLight");
        lightObj.transform.SetParent(waypoint.transform);
        lightObj.transform.localPosition = new Vector3(0, 2, 0);
        Light aura = lightObj.AddComponent<Light>();
        aura.type = LightType.Point;
        aura.color = Color.red;
        aura.range = 10f;
        
        wpScript.auraLight = aura;

        Debug.Log("<color=green>[NEXUS]</color> World generated successfully. No manual clicking required.");
    }

    [MenuItem("Tools/Nexus Auto Builder/Undo Basic Open World")]
    public static void UndoWorld()
    {
        GameObject ground = GameObject.Find("Massive Ground Plane");
        if (ground != null) DestroyImmediate(ground);

        // Try to find the fake player we spawned (Capsule primitive)
        // We do not want to delete the actual Player.prefab if he dragged it in.
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach(GameObject p in players)
        {
            if (p.name == "Player" && p.GetComponent<MeshFilter>() != null && p.GetComponent<MeshFilter>().sharedMesh.name == "Capsule")
            {
                DestroyImmediate(p);
            }
        }

        GameObject waypoint = GameObject.Find("Deep Wilderness Waypoint");
        if (waypoint != null) DestroyImmediate(waypoint);

        Debug.Log("<color=orange>[NEXUS]</color> Mistake reversed. Clean slate.");
    }

    [MenuItem("Tools/Nexus Auto Builder/Spawn The Grind (Abyssal Imp)")]
    public static void SpawnImp()
    {
        // Find existing player to spawn near them
        GameObject player = GameObject.FindWithTag("Player");
        Vector3 spawnPos = player != null ? player.transform.position + new Vector3(5, 0, 5) : new Vector3(5, 1, 5);

        // Load the dummy prefab
        string prefabPath = "Assets/Prefabs/Enemy_Dummy/Enemy_Dummy.prefab";
        GameObject dummyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        
        if (dummyPrefab == null)
        {
            // Fallback to primitive if prefab missing
            GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            fallback.transform.position = spawnPos;
            fallback.name = "Abyssal Imp (Primitive Fallback)";
            fallback.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f); // Make it small like an imp
            
            Rigidbody rb = fallback.AddComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            
            fallback.AddComponent<EnemyAggroChase>();
            
            // Note: Cannot directly set sharedMaterial color on a primitive without leaking in editor
            // But good enough for a visual test block
            
            Debug.Log("<color=magenta>[NEXUS]</color> Spawned a primitive Abyssal Imp because the dummy prefab was missing.");
            return;
        }

        GameObject imp = (GameObject)PrefabUtility.InstantiatePrefab(dummyPrefab);
        imp.name = "Abyssal Imp";
        imp.transform.position = spawnPos;
        imp.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f); // Imp size

        // Ensure it has the chase script
        if (imp.GetComponent<EnemyAggroChase>() == null)
        {
            imp.AddComponent<EnemyAggroChase>();
        }

        Debug.Log("<color=magenta>[NEXUS]</color> The Grind begins. Abyssal Imp spawned and wired to chase.");
    }
}