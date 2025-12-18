#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Abyss.Legacy;

namespace Abyssbound.EditorTools
{
    public static class CreateZone1BossGateAsset
    {
        private const string GateFolder = "Assets/Game/Gates";
        private const string GateAssetPath = "Assets/Game/Gates/Zone1BossGate.asset";

        [MenuItem("Abyssbound/Gates/Create Zone1 Boss Gate Definition")]
        public static void CreateOrSelectZone1BossGateDefinition()
        {
            EnsureFolderExists("Assets/Game");
            EnsureFolderExists(GateFolder);

            var gate = AssetDatabase.LoadAssetAtPath<GateDefinition>(GateAssetPath);
            if (gate == null)
            {
                gate = ScriptableObject.CreateInstance<GateDefinition>();
                AssetDatabase.CreateAsset(gate, GateAssetPath);
            }

            // GateDefinition currently defines its requirement as a LegacyItemDefinition reference.
            // We resolve "AbyssalSigil" by itemId or by asset name.
            var sigil = FindItemDefinitionByIdOrName("AbyssalSigil");
            if (sigil == null)
            {
                Debug.LogWarning("[CreateZone1BossGateAsset] Could not find ItemDefinition for 'AbyssalSigil' (matched by itemId or asset name). GateDefinition.requiredItem left unset.");
            }
            else
            {
                gate.requiredItem = sigil;
            }

            gate.lockedHintText = "Abyssal Sigil is required to enter.";

            EditorUtility.SetDirty(gate);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = gate;
            EditorGUIUtility.PingObject(gate);
        }

        private static void EnsureFolderExists(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int lastSlash = path.LastIndexOf('/');
            if (lastSlash <= 0)
                return;

            string parent = path.Substring(0, lastSlash);
            string name = path.Substring(lastSlash + 1);

            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolderExists(parent);

            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }

        private static LegacyItemDefinition FindItemDefinitionByIdOrName(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return null;

            var guids = AssetDatabase.FindAssets("t:LegacyItemDefinition");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<LegacyItemDefinition>(path);
                if (item == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(item.itemId) && string.Equals(item.itemId, token, System.StringComparison.OrdinalIgnoreCase))
                    return item;

                if (string.Equals(item.name, token, System.StringComparison.OrdinalIgnoreCase))
                    return item;
            }

            return null;
        }
    }
}
#endif
