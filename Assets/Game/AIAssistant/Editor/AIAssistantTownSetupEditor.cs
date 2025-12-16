#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AIAssistantTownSetup))]
public class AIAssistantTownSetupEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        GUILayout.Space(10);
        if (GUILayout.Button("Setup Town (AI Assistant)", GUILayout.Height(40)))
        {
            var setup = (AIAssistantTownSetup)target;
            setup.SetupTown();
        }
    }
}
#endif
