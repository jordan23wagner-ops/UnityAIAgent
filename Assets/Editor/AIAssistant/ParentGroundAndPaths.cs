using UnityEditor;
using UnityEngine;

public class ParentGroundAndPaths : EditorWindow
{
    [MenuItem("Tools/Parent Ground and Paths")] 
    public static void ShowWindow()
    {
        GetWindow<ParentGroundAndPaths>("Parent Ground and Paths");
    }

    private void OnGUI()
    {
        if (GUILayout.Button("Parent All Ground and Paths"))
        {
            ParentObjects();
        }
    }

    private void ParentObjects()
    {
        var zone1Env = GameObject.Find("Zone1_Env");
        var zone1Paths = GameObject.Find("Zone1_Paths");
        if (zone1Env == null || zone1Paths == null)
        {
            Debug.LogError("Zone1_Env or Zone1_Paths not found in the scene.");
            return;
        }

        // Parent ground planes
        foreach (var go in GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go == zone1Env || go == zone1Paths) continue;
            if (go.transform.parent != null) continue;
            string n = go.name.ToLower();
            if (n.Contains("ground"))
            {
                Undo.SetTransformParent(go.transform, zone1Env.transform, "Parent Ground");
                Debug.Log($"Parented {go.name} to Zone1_Env");
            }
            else if (n.Contains("slab") || n.Contains("path"))
            {
                Undo.SetTransformParent(go.transform, zone1Paths.transform, "Parent Path/Slab");
                Debug.Log($"Parented {go.name} to Zone1_Paths");
            }
        }
        Debug.Log("Parenting complete.");
    }
}
