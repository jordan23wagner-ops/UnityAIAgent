#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class Zone1BossGateTools
{
    private const string DefaultGateAssetPath = "Assets/Game/Gates/Zone1BossGate.asset";

    [MenuItem("Tools/Gates/Create Zone1 Boss Gate Definition")]
    public static void CreateZone1BossGateDefinition()
    {
        EnsureFolder("Assets/Game");
        EnsureFolder("Assets/Game/Gates");

        var existing = AssetDatabase.LoadAssetAtPath<GateDefinition>(DefaultGateAssetPath);
        if (existing != null)
        {
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            Debug.Log($"[Zone1BossGateTools] GateDefinition already exists at {DefaultGateAssetPath}");
            return;
        }

        var sigil = FindItemDefinition("AbyssalSigil");
        if (sigil == null)
        {
            Debug.LogWarning("[Zone1BossGateTools] Could not find ItemDefinition for 'AbyssalSigil' (matched by itemId or asset name). Creating GateDefinition with requiredItem unset.");
        }

        var gate = ScriptableObject.CreateInstance<GateDefinition>();
        gate.requiredItem = sigil;
        gate.lockedHintText = "Abyssal Sigil is required to enter.";

        AssetDatabase.CreateAsset(gate, DefaultGateAssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = gate;
        EditorGUIUtility.PingObject(gate);
        Debug.Log($"[Zone1BossGateTools] Created GateDefinition at {DefaultGateAssetPath}");
    }

    [MenuItem("Tools/Gates/Create Zone1 Boss Gate Placeholder (Scene)")]
    public static void CreateZone1BossGatePlaceholder()
    {
        var existing = GameObject.Find("Zone1_BossGate");
        if (existing != null)
        {
            Selection.activeGameObject = existing;
            EditorGUIUtility.PingObject(existing);
            Debug.Log("[Zone1BossGateTools] Scene placeholder already exists: Zone1_BossGate");
            return;
        }

        var gateDef = AssetDatabase.LoadAssetAtPath<GateDefinition>(DefaultGateAssetPath);

        var go = new GameObject("Zone1_BossGate");
        Undo.RegisterCreatedObjectUndo(go, "Create Zone1 Boss Gate Placeholder");
        go.transform.position = Vector3.zero;

        var collider = go.AddComponent<BoxCollider>();
        collider.isTrigger = true;

        var bossGate = go.AddComponent<BossGate>();

        // Assign via SerializedObject so we don't need BossGate fields to be public.
        var so = new SerializedObject(bossGate);
        var gateProp = so.FindProperty("gateDefinition");
        if (gateProp != null) gateProp.objectReferenceValue = gateDef;
        var gateColliderProp = so.FindProperty("gateCollider");
        if (gateColliderProp != null) gateColliderProp.objectReferenceValue = collider;
        so.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeGameObject = go;
        Debug.Log("[Zone1BossGateTools] Created scene placeholder at (0,0,0). Move it to your real boss gate entrance when ready.");
    }

    private static void EnsureFolder(string path)
    {
        // Creates nested folders one level at a time. Expects paths like "Assets/Game".
        if (AssetDatabase.IsValidFolder(path)) return;

        var lastSlash = path.LastIndexOf('/');
        if (lastSlash <= 0) return;

        var parent = path.Substring(0, lastSlash);
        var name = path.Substring(lastSlash + 1);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static ItemDefinition FindItemDefinition(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var guids = AssetDatabase.FindAssets("t:ItemDefinition");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (item == null) continue;

            if (!string.IsNullOrWhiteSpace(item.itemId) && string.Equals(item.itemId, token, System.StringComparison.OrdinalIgnoreCase))
                return item;

            if (string.Equals(item.name, token, System.StringComparison.OrdinalIgnoreCase))
                return item;
        }

        return null;
    }
}
#endif
