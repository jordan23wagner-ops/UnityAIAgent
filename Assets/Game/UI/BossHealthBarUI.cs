using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class BossHealthBarUI : MonoBehaviour
{
    private const string LogPrefix = "[BossHealthBarUI]";

    [SerializeField] private EnemyHealth target;
    [SerializeField] private Slider slider;

    private UnityAction _onDiedAction;

    private void Awake()
    {
        if (slider == null)
            slider = GetComponentInChildren<Slider>(true);

        Hide();
    }

    private void OnDisable()
    {
        Unhook();
    }

    public void Bind(EnemyHealth boss)
    {
        Unhook();
        target = boss;

        if (target == null)
        {
            Debug.LogWarning($"{LogPrefix} Bind called with null EnemyHealth; hiding bar.", this);
            Hide();
            return;
        }

        if (slider == null)
        {
            Debug.LogWarning($"{LogPrefix} Missing Slider; hiding bar.", this);
            Hide();
            return;
        }

        _onDiedAction = OnBossDied;
        try { target.OnDied.AddListener(_onDiedAction); }
        catch { }

        Show();
        Refresh();
        Debug.Log($"{LogPrefix} Bound to {target.gameObject.name}", this);
    }

    private void OnBossDied()
    {
        Debug.Log($"{LogPrefix} Boss defeated, hiding bar", this);
        Hide();
        Unhook();
    }

    private void Update()
    {
        if (target == null)
            return;

        // If the boss gets destroyed or disabled, hide.
        if (!target.isActiveAndEnabled)
        {
            Hide();
            Unhook();
            return;
        }

        Refresh();
    }

    private void Refresh()
    {
        if (slider == null)
            return;

        if (target == null)
        {
            slider.value = 0f;
            return;
        }

        float normalized = TryGetNormalizedHealth(target);
        slider.value = Mathf.Clamp01(normalized);

        if (normalized <= 0f)
        {
            // In case OnDied wasn't invoked for any reason.
            Hide();
            Unhook();
        }
    }

    private void Show()
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
    }

    private void Hide()
    {
        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    private void Unhook()
    {
        if (target != null && _onDiedAction != null)
        {
            try { target.OnDied.RemoveListener(_onDiedAction); }
            catch { }
        }
        _onDiedAction = null;
        target = null;
    }

    private static float TryGetNormalizedHealth(EnemyHealth health)
    {
        // EnemyHealth currently keeps health private; read fields via reflection (best-effort).
        try
        {
            var t = health.GetType();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var maxF = t.GetField("maxHealth", flags);
            var curF = t.GetField("currentHealth", flags);
            if (maxF == null || curF == null)
                return 0f;

            int max = (int)maxF.GetValue(health);
            int cur = (int)curF.GetValue(health);
            if (max <= 0) return 0f;
            return (float)cur / max;
        }
        catch
        {
            return 0f;
        }
    }

    public static BossHealthBarUI EnsureExists()
    {
        var existing = FindFirstInScene<BossHealthBarUI>();
        if (existing != null)
            return existing;

        // Find an existing HUD canvas (screen-space preferred).
        var canvas = FindFirstInScene<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning($"{LogPrefix} No Canvas found; cannot create boss bar.");
            return null;
        }

        var go = new GameObject("BossHealthBar");
        go.transform.SetParent(canvas.transform, false);

        // Create slider
        var slider = go.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;

        var bg = new GameObject("Background");
        bg.transform.SetParent(go.transform, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.6f);

        var fill = new GameObject("Fill");
        fill.transform.SetParent(go.transform, false);
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.7f, 0.05f, 0.05f, 0.95f);

        slider.targetGraphic = bgImg;
        slider.fillRect = fillImg.rectTransform;

        // Layout at top center
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -10f);
        rt.sizeDelta = new Vector2(520f, 20f);

        bgImg.rectTransform.anchorMin = Vector2.zero;
        bgImg.rectTransform.anchorMax = Vector2.one;
        bgImg.rectTransform.offsetMin = Vector2.zero;
        bgImg.rectTransform.offsetMax = Vector2.zero;

        fillImg.rectTransform.anchorMin = Vector2.zero;
        fillImg.rectTransform.anchorMax = Vector2.one;
        fillImg.rectTransform.offsetMin = Vector2.zero;
        fillImg.rectTransform.offsetMax = Vector2.zero;

        var ui = go.AddComponent<BossHealthBarUI>();
        ui.slider = slider;

        // Hidden by default
        go.SetActive(false);

        return ui;
    }

    private static T FindFirstInScene<T>() where T : UnityEngine.Object
    {
        var all = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (all == null || all.Length == 0)
            return null;
        return all[0];
    }
}
