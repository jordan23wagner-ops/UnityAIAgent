using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Game.Town;

namespace Abyss.Shop
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public class MerchantShopUI : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject root;
        [SerializeField] private Button exitButton;

        [Header("Left List")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private MerchantShopRowUI rowPrefab;

        [Header("Right Details")]
        [SerializeField] private TMP_Text detailNameText;
        [SerializeField] private TMP_Text detailPriceText;
        [SerializeField] private TMP_Text detailDescText;

        [Header("Top")]
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private TMP_Text titleText;

        private MerchantShop _currentShop;
        private bool _isOpen;
        private Game.Input.PlayerInputAuthority _inputAuthority;

        public static bool IsOpen { get; private set; }
        public static event Action<bool> OnOpenChanged;

        private void Awake()
        {
#if UNITY_2022_2_OR_NEWER
            _inputAuthority = FindFirstObjectByType<Game.Input.PlayerInputAuthority>();
#else
            _inputAuthority = FindObjectOfType<Game.Input.PlayerInputAuthority>();
#endif
            if (_inputAuthority == null)
                Debug.LogWarning("[MerchantShopUI] PlayerInputAuthority not found; input won't be locked automatically.");

            if (root != null)
                root.SetActive(false);

            if (exitButton != null)
            {
                exitButton.onClick.RemoveAllListeners();
                exitButton.onClick.AddListener(Close);
            }
        }

        public void Open(MerchantShop shop, string displayName, int playerGold)
        {
            if (shop == null || root == null || contentRoot == null || rowPrefab == null)
            {
                Debug.LogWarning("[MerchantShopUI] Cannot open: missing references or shop.");
                return;
            }

            _currentShop = shop;
            _isOpen = true;
            IsOpen = true;
            OnOpenChanged?.Invoke(true);
            root.SetActive(true);

            try { _inputAuthority?.SetUiInputLocked(true); } catch { }

            if (titleText != null) titleText.text = string.IsNullOrWhiteSpace(displayName) ? shop.MerchantName : displayName;
            if (goldText != null) goldText.text = $"Gold: {playerGold}";

            for (int i = contentRoot.childCount - 1; i >= 0; i--)
            {
                var c = contentRoot.GetChild(i);
                Destroy(c.gameObject);
            }

            var stock = shop.GetStock();
            if (stock != null)
            {
                foreach (var s in stock)
                {
                    var go = Instantiate(rowPrefab.gameObject, contentRoot, false);
                    var row = go.GetComponent<MerchantShopRowUI>();
                    if (row != null)
                    {
                        string itemName = s.itemName;
                        int price = s.price;
                        row.Bind(itemName, price, () => { OnRowClicked(itemName, price); });
                    }
                }
            }
            else
            {
                var none = new GameObject("NoItems");
                none.transform.SetParent(contentRoot, false);
                var txt = none.AddComponent<TMP_TextProxy>();
                txt.SetText("No items for sale");
            }

            if (contentRoot.childCount > 0)
            {
                var first = contentRoot.GetChild(0).GetComponent<MerchantShopRowUI>();
                if (first != null) first.ButtonSelect();
            }

            try
            {
                if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
            }
            catch { }

            Debug.Log($"[MerchantShopUI] Opened shop={shop.gameObject.name} items={(stock!=null?stock.Count:0)}");
        }

        private void OnRowClicked(string name, int price)
        {
            if (detailNameText != null) detailNameText.text = name;
            if (detailPriceText != null) detailPriceText.text = price.ToString();
            if (detailDescText != null) detailDescText.text = "No description.";
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;
            IsOpen = false;
            OnOpenChanged?.Invoke(false);

            try { if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null); } catch { }

            if (root != null) root.SetActive(false);

            try { _inputAuthority?.SetUiInputLocked(false); } catch { }

            Debug.Log("[MerchantShopUI] Closed UI");
        }

        // Backwards-compatible static helpers
        public static void Open(MerchantShop shop)
        {
            #if UNITY_2022_2_OR_NEWER
            var inst = FindFirstObjectByType<MerchantShopUI>();
            #else
            var inst = FindObjectOfType<MerchantShopUI>();
            #endif
            int gold = PlayerGoldWallet.Instance != null ? PlayerGoldWallet.Instance.Gold : 0;
            if (inst != null) inst.Open(shop, shop?.MerchantName ?? "Merchant", gold);
            else Debug.LogWarning("[MerchantShopUI] No MerchantShopUI instance found in scene.");
        }

        public static void CloseStatic()
        {
#if UNITY_2022_2_OR_NEWER
            var inst = FindFirstObjectByType<MerchantShopUI>();
#else
            var inst = FindObjectOfType<MerchantShopUI>();
#endif
            if (inst != null) inst.Close();
        }
    }

    // Minimal TMP proxy so we can create a fallback NoItems label without pulling TextMeshPro specifics here.
    internal class TMP_TextProxy : MonoBehaviour
    {
        private TMP_Text _tmp;
        private void Awake()
        {
            _tmp = gameObject.AddComponent<TextMeshProUGUI>();
            _tmp.fontSize = 24;
            _tmp.alignment = TextAlignmentOptions.Center;
            var rt = GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(0f, 40f);
        }
        public void SetText(string t)
        {
            if (_tmp == null) _tmp = GetComponent<TextMeshProUGUI>();
            if (_tmp != null) _tmp.text = t;
        }
    }
}

