using System;
using System.Collections.Generic;
using Abyss.Items;
using Abyssbound.Loot;
using Abyssbound.Stats;
using Game.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Abyssbound.Cooking
{
    /*
     QA checklist:
     - Spawn bonfire via Tools/Cooking/Setup Bonfire (Town)
     - Play mode: walk to bonfire, interact -> UI opens
     - With fish_raw_shrimp in inventory: recipe appears
     - Cook -> raw decreases, cooked increases (or burns), UI refreshes
     - No errors, no inventory UI regressions

     Burn chance quick checks:
     - L1  => 0.50
     - L25 => 0.40
     - L50 => 0.30
     - L75 => 0.20
     - L80 => 0.15
     - L90 => 0.10
     - L99 => 0.01
    */
    [DisallowMultipleComponent]
    public sealed class CookingUIController : MonoBehaviour
    {
        public event Action OnClosed;

        [Header("Wiring")]
        [SerializeField] private RectTransform panel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Button closeButton;
        [SerializeField] private RectTransform listRoot;
        [SerializeField] private TMP_Text emptyText;

        [Header("Prefabs")]
        [SerializeField] private GameObject recipeRowPrefab;

        [Header("Optional")]
        [Tooltip("ItemDefinitions referenced here will be loaded when the UI prefab is instantiated (helps inventory UI show icons).")]
        [SerializeField] private ItemDefinition[] preloadItemDefinitions;

        // Burn curve anchors (level -> burnChance)
        // 1->0.50, 25->0.40, 50->0.30, 75->0.20, 80->0.15, 90->0.10, 99->0.01
        private static readonly int[] s_BurnLevels = { 1, 25, 50, 75, 80, 90, 99 };
        private static readonly float[] s_BurnChances = { 0.50f, 0.40f, 0.30f, 0.20f, 0.15f, 0.10f, 0.01f };

        private readonly List<CookingRecipeSO> _recipes = new List<CookingRecipeSO>(16);

        private sealed class RowRuntime
        {
            public CookingRecipeSO recipe;
            public GameObject root;
            public TMP_Text nameText;
            public TMP_Text countsText;
            public Button cookButton;
        }

        private readonly List<RowRuntime> _rows = new List<RowRuntime>(32);

        private PlayerInventory _inventory;
        private PlayerStatsRuntime _stats;
        private bool _closeInvoked;

        private float _lastRefreshLogUnscaledTime = -999f;

        private void Awake()
        {
            if (panel == null)
                panel = transform as RectTransform;

            if (titleText != null && string.IsNullOrWhiteSpace(titleText.text))
                titleText.text = "Cooking";

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Close);
            }

            // The bottom text doubles as an empty-state message and a small hint.
            // Default active; content is managed by RefreshUI().
            if (emptyText != null)
                emptyText.gameObject.SetActive(true);
        }

        public void Show(IEnumerable<CookingRecipeSO> recipes)
        {
            _inventory = PlayerInventoryResolver.GetOrFind();
            if (_inventory == null)
            {
                Close();
                return;
            }

            if (_stats == null)
            {
                try
                {
#if UNITY_2022_2_OR_NEWER
                    _stats = FindFirstObjectByType<PlayerStatsRuntime>(FindObjectsInactive.Exclude);
#else
                    _stats = FindObjectOfType<PlayerStatsRuntime>();
#endif
                }
                catch { _stats = null; }
            }

            _recipes.Clear();
            if (recipes != null)
            {
                foreach (var r in recipes)
                {
                    if (r != null)
                        _recipes.Add(r);
                }
            }

            BuildList();
            RefreshUI();

            if (panel != null)
                panel.SetAsLastSibling();
        }

        private void BuildList()
        {
            ClearList();

            if (listRoot == null || recipeRowPrefab == null)
                return;

            for (int i = 0; i < _recipes.Count; i++)
            {
                var r = _recipes[i];
                if (r == null)
                    continue;

                var rowGo = Instantiate(recipeRowPrefab, listRoot);
                rowGo.name = $"RecipeRow_{(!string.IsNullOrWhiteSpace(r.recipeId) ? r.recipeId : i.ToString())}";

                var row = new RowRuntime
                {
                    recipe = r,
                    root = rowGo,
                    nameText = null,
                    countsText = null,
                    cookButton = null,
                };

                try
                {
                    var nameTr = rowGo.transform.Find("Name");
                    if (nameTr != null)
                        row.nameText = nameTr.GetComponent<TMP_Text>();
                }
                catch { row.nameText = null; }

                try
                {
                    var countsTr = rowGo.transform.Find("Counts");
                    if (countsTr != null)
                        row.countsText = countsTr.GetComponent<TMP_Text>();
                }
                catch { row.countsText = null; }

                try
                {
                    var btnTr = rowGo.transform.Find("CookButton");
                    if (btnTr != null)
                        row.cookButton = btnTr.GetComponent<Button>();
                }
                catch { row.cookButton = null; }

                if (row.nameText != null)
                {
                    string display = !string.IsNullOrWhiteSpace(r.displayName) ? r.displayName : r.recipeId;
                    if (string.IsNullOrWhiteSpace(display))
                        display = "Recipe";

                    row.nameText.text = $"Cook {display}";
                }

                if (row.cookButton != null)
                {
                    var captured = r;
                    row.cookButton.onClick.RemoveAllListeners();
                    row.cookButton.onClick.AddListener(() => TryCook(captured));
                }

                _rows.Add(row);
            }
        }

        private void RefreshUI()
        {
            if (_inventory == null)
                _inventory = PlayerInventoryResolver.GetOrFind();

            int visibleRecipes = 0;

            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                var r = row != null ? row.recipe : null;
                if (r == null)
                    continue;

                visibleRecipes++;

                int inCount = Mathf.Max(1, r.inputCount);
                int outCount = Mathf.Max(1, r.outputCount);

                int rawCount = 0;
                int cookedCount = 0;

                if (_inventory != null)
                {
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(r.inputItemId))
                            rawCount = _inventory.Count(r.inputItemId);
                    }
                    catch { rawCount = 0; }

                    try
                    {
                        if (!string.IsNullOrWhiteSpace(r.outputItemId))
                            cookedCount = _inventory.Count(r.outputItemId);
                    }
                    catch { cookedCount = 0; }
                }

                if (row.countsText != null)
                    row.countsText.text = $"Raw: {rawCount}   Cooked: {cookedCount}";

                bool hasInputs = rawCount >= inCount;
                bool hasOutputs = !string.IsNullOrWhiteSpace(r.outputItemId);
                bool hasRoom = true;
                if (_inventory != null && hasOutputs)
                {
                    try { hasRoom = _inventory.HasRoomForAdd(r.outputItemId, outCount); }
                    catch { hasRoom = true; }
                }

                if (row.cookButton != null)
                    row.cookButton.interactable = hasInputs && hasOutputs && hasRoom;
            }

            if (emptyText != null)
            {
                if (visibleRecipes == 0)
                {
                    emptyText.text = "No recipes.";
                    emptyText.gameObject.SetActive(true);
                }
                else
                {
                    emptyText.text = "Cook at the bonfire. Higher Cooking reduces burns.";
                    emptyText.gameObject.SetActive(true);
                }
            }

            // Optional debug log (rate-limited so it's not spammy).
            if (Time.unscaledTime - _lastRefreshLogUnscaledTime > 0.75f)
            {
                _lastRefreshLogUnscaledTime = Time.unscaledTime;
                Debug.Log("[CookingUI] Refreshed counts");
            }
        }

        private void TryCook(CookingRecipeSO r)
        {
            if (r == null || _inventory == null)
                return;

            int inCount = Mathf.Max(1, r.inputCount);
            int outCount = Mathf.Max(1, r.outputCount);

            if (_inventory.Count(r.inputItemId) < inCount)
            {
                RefreshUI();
                return;
            }

            if (!_inventory.HasRoomForAdd(r.outputItemId, outCount))
            {
                RefreshUI();
                return;
            }

            if (!_inventory.TryConsume(r.inputItemId, inCount))
            {
                RefreshUI();
                return;
            }

            int cookingLevel = 1;
            try
            {
                if (_stats != null)
                    cookingLevel = _stats.GetLevel(StatType.Cooking);
            }
            catch { cookingLevel = 1; }

            float burnChance = GetBurnChance(cookingLevel);
            bool burned = false;
            try { burned = UnityEngine.Random.value < burnChance; }
            catch { burned = false; }

            if (!burned)
            {
                _inventory.Add(r.outputItemId, outCount);
                Debug.Log($"[Cooking] Cooked {outCount}x {r.outputItemId} from {inCount}x {r.inputItemId} (level={Mathf.Clamp(cookingLevel, 1, 99)} burnChance={burnChance:0.00})");
            }
            else
            {
                Debug.Log($"[Cooking] Burned {inCount}x {r.inputItemId} (level={Mathf.Clamp(cookingLevel, 1, 99)} burnChance={burnChance:0.00})");
            }

            // No lingering tooltip/selection when clicking the cook button.
            try { EventSystem.current?.SetSelectedGameObject(null); } catch { }

            RefreshUI();
        }

        private static float GetBurnChance(int cookingLevel)
        {
            // Piecewise linear interpolation between design anchors.
            int level = Mathf.Clamp(cookingLevel, 1, 99);

            if (s_BurnLevels == null || s_BurnChances == null || s_BurnLevels.Length < 2 || s_BurnLevels.Length != s_BurnChances.Length)
                return 0.50f;

            if (level <= s_BurnLevels[0])
                return Mathf.Clamp(s_BurnChances[0], 0.01f, 0.50f);

            int lastIndex = s_BurnLevels.Length - 1;
            if (level >= s_BurnLevels[lastIndex])
                return Mathf.Clamp(s_BurnChances[lastIndex], 0.01f, 0.50f);

            for (int i = 0; i < lastIndex; i++)
            {
                int aLvl = s_BurnLevels[i];
                int bLvl = s_BurnLevels[i + 1];
                if (level < aLvl)
                    continue;

                if (level > bLvl)
                    continue;

                float a = s_BurnChances[i];
                float b = s_BurnChances[i + 1];
                float t = (bLvl == aLvl) ? 0f : Mathf.InverseLerp(aLvl, bLvl, level);
                float c = Mathf.Lerp(a, b, t);
                return Mathf.Clamp(c, 0.01f, 0.50f);
            }

            // Should not happen, but keep safe.
            return Mathf.Clamp(s_BurnChances[lastIndex], 0.01f, 0.50f);
        }

        private void ClearList()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                try
                {
                    if (_rows[i]?.root != null)
                        Destroy(_rows[i].root);
                }
                catch { }
            }

            _rows.Clear();
        }

        public void Close()
        {
            if (_closeInvoked)
                return;

            _closeInvoked = true;

            Debug.Log("[Cooking] UI closed");

            try { OnClosed?.Invoke(); }
            catch { }

            // Prevent tooltip/selection lingering.
            try
            {
                EventSystem.current?.SetSelectedGameObject(null);
            }
            catch { }

            try
            {
                var tooltip = FindFirstObjectByType<ItemTooltipUI>(FindObjectsInactive.Include);
                if (tooltip != null)
                    tooltip.HideAndClear();
            }
            catch { }

            Destroy(gameObject);
        }
    }
}
