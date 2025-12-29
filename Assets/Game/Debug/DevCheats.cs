using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Abyssbound.Loot;

namespace Abyssbound.DebugTools
{
    public sealed class DevCheats : MonoBehaviour
    {
#if ENABLE_INPUT_SYSTEM
        [Header("Keys")]
        // NOTE: F6 is reserved for Waypoints (Abyss.Waypoints.WaypointManager).
        // This legacy/dev cheat is intentionally NOT bound to F6.
        [SerializeField] private Key spawnBossSigilKey = Key.F8;
        [SerializeField] private Key addTestSwordKey = Key.F7;
#endif

        private void Update()
        {
            if (!Application.isPlaying)
                return;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null)
                return;

            if (kb[spawnBossSigilKey].wasPressedThisFrame)
            {
                if (LootQaSettings.DebugLogsEnabled)
                    Debug.Log("[DevCheats] Spawn Sigil (F8)");
                DevCheatActions.SpawnBossSigil();
            }

            if (kb[addTestSwordKey].wasPressedThisFrame)
            {
                if (LootQaSettings.DebugLogsEnabled)
                    Debug.Log("[DevCheats] Spawn Selected Magic+ Items (F7)");
                DevCheatActions.SpawnSelectedItemMagicPlus();
            }
#else
            // Legacy input path for projects set to "Input Manager" only.
            // F6 is reserved for Waypoints now.
            if (Input.GetKeyDown(KeyCode.F8))
            {
                if (LootQaSettings.DebugLogsEnabled)
                    Debug.Log("[DevCheats] Spawn Sigil (F8)");
                DevCheatActions.SpawnBossSigil();
            }

            if (Input.GetKeyDown(KeyCode.F7))
            {
                if (LootQaSettings.DebugLogsEnabled)
                    Debug.Log("[DevCheats] Spawn Selected Magic+ Items (F7)");
                DevCheatActions.SpawnSelectedItemMagicPlus();
            }
#endif
        }
    }
}
