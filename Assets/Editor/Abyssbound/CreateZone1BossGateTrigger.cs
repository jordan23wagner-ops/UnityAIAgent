#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Abyssbound.EditorTools
{
    public static class CreateZone1BossGateTrigger
    {
        private const string GateAssetPath = "Assets/Game/Gates/Zone1BossGate.asset";

        [MenuItem("Abyssbound/Gates/Create Zone1 Boss Gate Trigger (Scene)")]
        public static void CreateTrigger()
        {
            var existing = GameObject.Find("Zone1_BossGate_Trigger");
            if (existing != null)
            {
                Selection.activeGameObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            var gateDef = AssetDatabase.LoadAssetAtPath<GateDefinition>(GateAssetPath);
            if (gateDef == null)
            {
                Debug.LogWarning($"[CreateZone1BossGateTrigger] GateDefinition not found at {GateAssetPath}. Trigger will be created with gateDefinition unset.");
            }

            var go = new GameObject("Zone1_BossGate_Trigger");
            Undo.RegisterCreatedObjectUndo(go, "Create Zone1 Boss Gate Trigger");

            go.transform.position = Vector3.zero;

            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(2f, 2f, 2f);

            var bossGate = go.AddComponent<BossGate>();

            // Assign private serialized fields via SerializedObject.
            var so = new SerializedObject(bossGate);
            var gateDefProp = so.FindProperty("gateDefinition");
            if (gateDefProp != null) gateDefProp.objectReferenceValue = gateDef;
            var gateColliderProp = so.FindProperty("gateCollider");
            if (gateColliderProp != null) gateColliderProp.objectReferenceValue = box;
            so.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
        }
    }
}
#endif
