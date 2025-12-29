#if UNITY_EDITOR
using Abyss.Dev;
using UnityEditor;
using UnityEngine;

// NOTE: Legacy/QA editor tools are hidden unless ABYSS_LEGACY_QA_TOOLS is defined.
// Enable via Project Settings > Player > Scripting Define Symbols.

namespace Abyssbound.QA
{
    public static class TtkSimMenu
    {
        private const int DefaultKills = 50;

    // Old menu path: Tools/Abyssbound/QA/TTK Sim/Run Trash (50)
#if ABYSS_LEGACY_QA_TOOLS
    [MenuItem("Tools/Legacy QA/TTK Sim/Run Trash (50)")]
    private static void RunTrash50() => Run("Loot/Tables/Zone1_Trash", DefaultKills);

    // Old menu path: Tools/Abyssbound/QA/TTK Sim/Run Elite (50)
    [MenuItem("Tools/Legacy QA/TTK Sim/Run Elite (50)")]
    private static void RunElite50() => Run("Loot/Tables/Zone1_Elite", DefaultKills);

    // Old menu path: Tools/Abyssbound/QA/TTK Sim/Run Boss (50)
    [MenuItem("Tools/Legacy QA/TTK Sim/Run Boss (50)")]
    private static void RunBoss50() => Run("Loot/Tables/Zone1_Boss", DefaultKills);
#endif

        private static void Run(string lootTableResourcesPath, int kills)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[TTK Sim] Enter Play Mode first.");
                return;
            }

            var runner = Object.FindFirstObjectByType<TtkSimRunner>(FindObjectsInactive.Include);
            if (runner == null)
            {
                var go = new GameObject("TTK_SimRunner");
                runner = go.AddComponent<TtkSimRunner>();
            }

            if (runner.IsRunning)
            {
                Debug.LogWarning("[TTK Sim] Already running.");
                return;
            }

            runner.StartSim(lootTableResourcesPath, kills);
        }
    }
}
#endif
