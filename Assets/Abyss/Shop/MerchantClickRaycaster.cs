using UnityEngine;

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

            MerchantShopUI.EnsureUiExists();
        }

        private void Update()
        {
            // Left click only
            if (!Input.GetMouseButtonDown(0)) return;

            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            var ray = _cam.ScreenPointToRay(Input.mousePosition);

            if (!Physics.Raycast(ray, out var hit, 500f))
                return;

            // Walk up parent chain to find MerchantShop
            var shop = hit.transform.GetComponentInParent<MerchantShop>();
            if (shop == null) return;

            // TODO: pull player gold from economy system instead of 0.
            MerchantShopUI.Open(shop.MerchantName, 0, string.Empty);
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
