using UnityEngine;
using UnityEngine.EventSystems;

namespace Abyss.Shop
{
    /// <summary>
    /// Global click handler that opens MerchantShop when you click any collider under it.
    /// Avoids Unity OnMouseDown quirks.
    /// </summary>
    public sealed class MerchantClickRaycaster : MonoBehaviour
    {
        private Camera _cam;

        private void Awake()
        {
            _cam = Camera.main;
            if (_cam == null) _cam = FindAnyObjectByType<Camera>();
        }

        private void Update()
        {
            // Left click only
            if (!Input.GetMouseButtonDown(0)) return;

            // If the shop UI is open, DO NOT raycast into the world (prevents immediate reopen on Exit click).
            if (MerchantShopUI.IsOpen) return;

            // If pointer is over any UI, do not raycast into the world.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            var ray = _cam.ScreenPointToRay(Input.mousePosition);

            if (!Physics.Raycast(ray, out var hit, 500f))
                return;

            // Walk up parent chain to find MerchantShop
            var shop = hit.transform.GetComponentInParent<MerchantShop>();
            if (shop == null) return;

            // Open the inspector-driven UI with the resolved shop reference.
            MerchantShopUI.Open(shop);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var existing = FindAnyObjectByType<MerchantClickRaycaster>();
            if (existing != null) return;

            var go = new GameObject("MerchantClickRaycaster");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            go.AddComponent<MerchantClickRaycaster>();
        }
    }
}
