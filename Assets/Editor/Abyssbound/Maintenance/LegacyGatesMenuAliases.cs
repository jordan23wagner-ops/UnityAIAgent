#if UNITY_EDITOR
using UnityEditor;

namespace Abyssbound.EditorTools
{
    internal static class LegacyGatesMenuAliases
    {
        // Compatibility aliases: older shortcut profiles and tooling may still reference the legacy Tools/Gates menu paths.
        // These forward to the current Abyssbound menu implementations without relying on ExecuteMenuItem.

        [MenuItem("Tools/Gates/Create Zone1 Boss Gate Definition")]
        private static void CreateZone1BossGateDefinition_Alias()
        {
            CreateZone1BossGateAsset.CreateOrSelectZone1BossGateDefinition();
        }

        [MenuItem("Tools/Gates/Create Zone1 Boss Gate Placeholder (Scene)")]
        private static void CreateZone1BossGatePlaceholder_Alias()
        {
            global::Zone1BossGateTools.CreateZone1BossGatePlaceholder();
        }
    }
}
#endif
