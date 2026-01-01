using UnityEngine;

namespace Abyssbound.Cooking
{
    [DisallowMultipleComponent]
    public sealed class BonfireVisuals : MonoBehaviour
    {
        private static bool s_logged;

        private void Awake()
        {
            if (s_logged)
                return;

            s_logged = true;
            Debug.Log("[Cooking] Bonfire visuals initialized");
        }
    }
}
