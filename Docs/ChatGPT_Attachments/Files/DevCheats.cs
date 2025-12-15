using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Abyssbound.DebugTools
{
    public sealed class DevCheats : MonoBehaviour
    {
#if ENABLE_INPUT_SYSTEM
        [Header("Keys")]
        [SerializeField] private Key spawnBossSigilKey = Key.F6;
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
                Debug.Log("[DevCheats] Spawn Sigil (F6)");
                DevCheatActions.SpawnBossSigil();
            }

            if (kb[addTestSwordKey].wasPressedThisFrame)
            {
                Debug.Log("[DevCheats] Add Test Sword (F7)");
                DevCheatActions.AddTestSword();
            }
#else
            // Legacy input path for projects set to "Input Manager" only.
            // F6/F7 bindings are fixed to preserve current behavior.
            if (Input.GetKeyDown(KeyCode.F6))
            {
                Debug.Log("[DevCheats] Spawn Sigil (F6)");
                DevCheatActions.SpawnBossSigil();
            }

            if (Input.GetKeyDown(KeyCode.F7))
            {
                Debug.Log("[DevCheats] Add Test Sword (F7)");
                DevCheatActions.AddTestSword();
            }
#endif
        }
    }
}
