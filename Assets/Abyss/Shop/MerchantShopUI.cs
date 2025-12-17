using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Abyss.Shop
{
    public class MerchantShopUI : MonoBehaviour
    {
        private static MerchantShopUI _instance;
        public static bool IsOpen { get; private set; }

        public static MerchantShopUI EnsureUiExists()
        {
            if (_instance != null) return _instance;
            // Try to find existing (including inactive)
#if UNITY_2022_2_OR_NEWER
            _instance = FindFirstObjectByType<MerchantShopUI>(FindObjectsInactive.Include);
#endif
            if (_instance == null)
                _instance = FindObjectOfType<MerchantShopUI>(true);

            if (_instance != null) return _instance;

            var go = new GameObject("MerchantShopUI");
            DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            _instance = go.AddComponent<MerchantShopUI>();

            // Create a minimal runtime UI hierarchy so Open never NREs.
            // Root panel
            var root = new GameObject("RootPanel");
            root.transform.SetParent(go.transform, false);
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(560, 320);
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero;
            var img = root.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.75f);

            // Title
            var titleGo = new GameObject("TitleText");
            titleGo.transform.SetParent(root.transform, false);
            var titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -12f);
            titleRect.sizeDelta = new Vector2(520f, 40f);
            TextMeshProUGUI titleTxt = null;
            if (titleGo.GetComponent<TextMeshProUGUI>() == null)
                titleTxt = titleGo.AddComponent<TextMeshProUGUI>();
            else
                titleTxt = titleGo.GetComponent<TextMeshProUGUI>();
            titleTxt.fontSize = 32;
            titleTxt.alignment = TextAlignmentOptions.Center;

            // Gold label
            var goldGo = new GameObject("GoldText");
            goldGo.transform.SetParent(root.transform, false);
            var goldRect = goldGo.AddComponent<RectTransform>();
            goldRect.anchorMin = new Vector2(0f, 1f);
            goldRect.anchorMax = new Vector2(0f, 1f);
            goldRect.pivot = new Vector2(0f, 1f);
            goldRect.anchoredPosition = new Vector2(12f, -52f);
            goldRect.sizeDelta = new Vector2(200f, 28f);
            TextMeshProUGUI goldTxt = null;
            if (goldGo.GetComponent<TextMeshProUGUI>() == null)
                goldTxt = goldGo.AddComponent<TextMeshProUGUI>();
            else
                goldTxt = goldGo.GetComponent<TextMeshProUGUI>();
            goldTxt.fontSize = 20;

            // Items area: ScrollRect -> viewport -> content
            var scrollGo = new GameObject("ItemsScroll");
            scrollGo.transform.SetParent(root.transform, false);
            var scrollRectTf = scrollGo.AddComponent<RectTransform>();
            scrollRectTf.anchorMin = new Vector2(0f, 0f);
            scrollRectTf.anchorMax = new Vector2(1f, 1f);
            scrollRectTf.pivot = new Vector2(0.5f, 0.5f);
            scrollRectTf.anchoredPosition = new Vector2(0f, -20f);
            scrollRectTf.offsetMin = new Vector2(12f, 12f);
            scrollRectTf.offsetMax = new Vector2(-12f, -80f);

            var scroll = scrollGo.AddComponent<ScrollRect>();
            var viewportGo = new GameObject("Viewport");
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var vpRect = viewportGo.AddComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.offsetMin = Vector2.zero;
            vpRect.offsetMax = Vector2.zero;
            var vpImage = viewportGo.AddComponent<Image>();
            vpImage.color = new Color(0f, 0f, 0f, 0f);
            var mask = viewportGo.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(viewportGo.transform, false);
            var contentRect = contentGo.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);

            var layout = contentGo.AddComponent<VerticalLayoutGroup>();
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.spacing = 6f;

            var fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = contentRect;
            scroll.viewport = vpRect;
            scroll.horizontal = false;

            // Row template
            var row = new GameObject("RowTemplate");
            row.transform.SetParent(contentGo.transform, false);
            var rowRect = row.AddComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0f, 40f);
            var hLayout = row.AddComponent<HorizontalLayoutGroup>();
            hLayout.childForceExpandHeight = true;
            hLayout.childForceExpandWidth = false;
            hLayout.spacing = 8f;

            var nameGo = new GameObject("Name");
            nameGo.transform.SetParent(row.transform, false);
            var nameTxt = nameGo.AddComponent<TextMeshProUGUI>();
            nameTxt.fontSize = 18;
            nameTxt.color = Color.white;
            var nameRt = nameGo.GetComponent<RectTransform>();
            nameRt.sizeDelta = new Vector2(400f, 36f);

            var priceGo = new GameObject("Price");
            priceGo.transform.SetParent(row.transform, false);
            var priceTxt = priceGo.AddComponent<TextMeshProUGUI>();
            priceTxt.fontSize = 18;
            priceTxt.color = Color.yellow;
            var priceRt = priceGo.GetComponent<RectTransform>();
            priceRt.sizeDelta = new Vector2(120f, 36f);

            var buyGo = new GameObject("BuyButton");
            buyGo.transform.SetParent(row.transform, false);
            var buyBtnImg = buyGo.AddComponent<Image>();
            buyBtnImg.color = new Color(0.9f,0.9f,0.9f,1f);
            var buyBtn = buyGo.AddComponent<Button>();
            var buyLabelGo = new GameObject("Text");
            buyLabelGo.transform.SetParent(buyGo.transform, false);
            var buyLabel = buyLabelGo.AddComponent<TextMeshProUGUI>();
            buyLabel.text = "Buy";
            buyLabel.fontSize = 16;
            var buyRt = buyGo.GetComponent<RectTransform>();
            buyRt.sizeDelta = new Vector2(80f, 32f);

            row.SetActive(false);

            // Wire to instance
            _instance.itemsScrollRect = scroll;
            _instance.itemsContent = contentRect;
            _instance.rowTemplate = row;

            // Exit button
            var exitGo = new GameObject("ExitButton");
            exitGo.transform.SetParent(root.transform, false);
            var exitRect = exitGo.AddComponent<RectTransform>();
            exitRect.anchorMin = new Vector2(1f, 1f);
            exitRect.anchorMax = new Vector2(1f, 1f);
            exitRect.pivot = new Vector2(1f, 1f);
            exitRect.anchoredPosition = new Vector2(-12f, -12f);
            exitRect.sizeDelta = new Vector2(100f, 36f);
            var exitImg = exitGo.AddComponent<Image>();
            exitImg.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            var btn = exitGo.AddComponent<Button>();
            var exitLabelGo = new GameObject("Text");
            exitLabelGo.transform.SetParent(exitGo.transform, false);
            var exitLabel = exitLabelGo.AddComponent<TextMeshProUGUI>();
            exitLabel.text = "Exit";
            exitLabel.fontSize = 18;

            // Wire references on the instance so OpenInstance can safely use them
            _instance.canvas = canvas;
            _instance.rootPanel = rootRect;
            _instance.titleText = titleTxt;
            _instance.goldText = goldTxt;
            _instance.exitButton = btn;

            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(_instance.CloseInstance);

            _instance.CloseImmediate();
            return _instance;
        }

        public static void Close()
        {
            if (_instance == null) return;
            _instance.CloseInstance();
        }

        public static void Open(string merchantName) => Open(merchantName, 0, string.Empty);
        public static void Open(string merchantName, int gold) => Open(merchantName, gold, string.Empty);
        // Main API - keep exactly this signature for compatibility
        public static void Open(string merchantName, int gold, string merchantKey)
        {
            var ui = EnsureUiExists();
            ui.OpenInstance(merchantName, gold, merchantKey);
        }

        // Backwards-compatible: accept MerchantShop objects
        public static void Open(MerchantShop shop)
        {
            if (shop == null) return;
            var ui = EnsureUiExists();
            if (ui == null) return;
            ui.OpenInstanceFromShop(shop);
        }

        [Header("UI Refs")]
        public Canvas canvas;
        public RectTransform rootPanel;
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI goldText;
        // items area
        private ScrollRect itemsScrollRect;
        private RectTransform itemsContent;
        private GameObject rowTemplate;
        public Button exitButton;

        private bool isOpen;

        void Awake()
        {
            EnsureCanvas();
            EnsureExitButton();
            ApplyLayoutFixes();
            CloseImmediate();
        }

        // =========================
        // PUBLIC API
        // =========================

        private static bool _openFailureLogged = false;

        // Instance implementation - renamed to avoid colliding with static API
        public void OpenInstance(string merchantName, int gold, string merchantKey)
        {
            // Ensure the instance and essential UI refs exist
            var ui = EnsureUiExists();
            if (ui == null || ui.rootPanel == null || ui.titleText == null || ui.goldText == null || ui.itemsContent == null)
            {
                if (!_openFailureLogged)
                {
                    Debug.LogError($"[MerchantShopUI] OpenInstance failed: instance={(ui!=null)} root={(ui?.rootPanel!=null)} title={(ui?.titleText!=null)} gold={(ui?.goldText!=null)} content={(ui?.itemsContent!=null)}");
                    _openFailureLogged = true;
                }
                return;
            }

            isOpen = true;
            gameObject.SetActive(true);

            titleText.text = string.IsNullOrWhiteSpace(merchantName) ? "Merchant" : merchantName;
            goldText.text = $"Gold: {gold}";

            // Populate items: try to resolve a MerchantShop by name or key
            MerchantShop resolved = null;
            if (!string.IsNullOrEmpty(merchantKey))
            {
                var maybe = GameObject.Find(merchantKey);
                if (maybe != null) resolved = maybe.GetComponent<MerchantShop>();
            }
            if (resolved == null && !string.IsNullOrEmpty(merchantName))
            {
                foreach (var ms in Object.FindObjectsOfType<MerchantShop>())
                {
                    if (ms.MerchantName == merchantName) { resolved = ms; break; }
                }
            }

            int count = 0;
            if (resolved != null)
            {
                count = EnsurePopulateItems(resolved.Stock);
            }
            else
            {
                count = EnsurePopulateItems(null);
            }

            Debug.Log($"[MerchantShopUI] Open merchantKey={merchantKey} items={count}");

            BlockGameplayInput(true);
        }

        public void CloseInstance()
        {
            isOpen = false;
            gameObject.SetActive(false);
            BlockGameplayInput(false);
        }

        // Populate UI from a MerchantShop instance
        public void OpenInstanceFromShop(MerchantShop shop)
        {
            if (shop == null) return;
            OpenInstance(shop.MerchantName, 0, shop.name);

            // Populate rows from shop.Stock
            EnsurePopulateItems(shop.Stock);
        }

        private int EnsurePopulateItems(System.Collections.Generic.IReadOnlyList<MerchantShop.StockEntry> stock)
        {
            if (itemsContent == null || rowTemplate == null)
                return 0;

            // Clear old rows (destroy all children)
            foreach (Transform t in itemsContent)
            {
                Object.Destroy(t.gameObject);
            }

            int count = 0;
            if (stock != null && stock.Count > 0)
            {
                foreach (var s in stock)
                {
                    var row = Object.Instantiate(rowTemplate, itemsContent);
                    row.name = "Row_" + s.itemName;
                    row.SetActive(true);
                    var nameTxt = row.transform.Find("Name").GetComponent<TextMeshProUGUI>();
                    var priceTxt = row.transform.Find("Price").GetComponent<TextMeshProUGUI>();
                    var buyBtn = row.transform.Find("BuyButton").GetComponent<Button>();
                    nameTxt.text = s.itemName;
                    priceTxt.text = s.price.ToString();
                    buyBtn.onClick.RemoveAllListeners();
                    int price = s.price;
                    string itemName = s.itemName;
                    buyBtn.onClick.AddListener(() => { Debug.Log($"[MerchantShopUI] Buy clicked {itemName} price={price}"); });
                    count++;
                }
            }
            else
            {
                // Show "No items for sale"
                var noneGo = new GameObject("NoItems");
                noneGo.transform.SetParent(itemsContent, false);
                var txt = noneGo.AddComponent<TextMeshProUGUI>();
                txt.text = "No items for sale";
                txt.fontSize = 20;
                txt.alignment = TextAlignmentOptions.Center;
                var rt = noneGo.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(0f, 40f);
            }

            return count;
        }

        // =========================
        // SETUP / FIXES
        // =========================

        private void EnsureCanvas()
        {
            if (canvas == null)
                canvas = GetComponentInChildren<Canvas>(true);

            if (canvas == null)
                canvas = gameObject.AddComponent<Canvas>();

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;

            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
                scaler = canvas.gameObject.AddComponent<CanvasScaler>();

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        private void ApplyLayoutFixes()
        {
            if (rootPanel == null)
                rootPanel = GetComponentInChildren<RectTransform>();
            rootPanel.anchorMin = new Vector2(0.5f, 0.5f);
            rootPanel.anchorMax = new Vector2(0.5f, 0.5f);
            rootPanel.anchoredPosition = Vector2.zero;
            rootPanel.sizeDelta = new Vector2(900, 500);

            // TEXT FIXES
            if (titleText != null) { titleText.fontSize = 36; titleText.alignment = TextAlignmentOptions.Center; }
            if (goldText != null) goldText.fontSize = 20;
            if (itemsContent != null)
            {
                // ensure content has a width
                var rt = itemsContent.GetComponent<RectTransform>();
                if (rt != null && rt.sizeDelta.x == 0f) rt.sizeDelta = new Vector2(800f, rt.sizeDelta.y);
            }
        }

        private void EnsureExitButton()
        {
            if (exitButton == null)
                return;

            exitButton.onClick.RemoveAllListeners();
            exitButton.onClick.AddListener(Close);

            var rect = exitButton.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
            rect.anchoredPosition = new Vector2(-12, -12);
            rect.sizeDelta = new Vector2(100, 36);

            var txt = exitButton.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null)
            {
                txt.text = "Exit";
                txt.fontSize = 18;
                txt.color = Color.black;
                txt.alignment = TextAlignmentOptions.Center;
            }
        }

        private void CloseImmediate()
        {
            isOpen = false;
            gameObject.SetActive(false);
        }

        // =========================
        // INPUT CONTROL
        // =========================

        private void BlockGameplayInput(bool block)
        {
            // Maintain UI open state for input guards elsewhere.
            IsOpen = block;
        }
    }
}

