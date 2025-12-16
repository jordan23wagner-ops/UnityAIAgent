using System;
using UnityEngine;
using UnityEngine.UI;

public static class HudFactory
{
    private const string HudCanvasName = "Abyss_HUDCanvas";
    private const string PlayerHealthBarName = "Abyss_PlayerHealthBar";

    private static bool _loggedHudCanvasCreated;
    private static bool _loggedHealthBarLayout;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ForceHudCanvasBeforeSceneLoad()
    {
        try
        {
            var c = EnsureHudCanvas();
            Debug.Log("[HUD] RuntimeInitialize created/confirmed Abyss_HUDCanvas", c);
        }
        catch (Exception e)
        {
            Debug.LogError("[HUD] RuntimeInitialize FAILED to create Abyss_HUDCanvas: " + e);
        }
    }

    public static Canvas EnsureHudCanvas()
    {
        Canvas existing = null;
        try
        {
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                var c = canvases[i];
                if (c == null)
                    continue;

                if (string.Equals(c.gameObject.name, HudCanvasName, StringComparison.Ordinal))
                {
                    existing = c;
                    break;
                }
            }
        }
        catch { }

        if (existing == null)
        {
            var go = new GameObject(HudCanvasName);
            existing = go.AddComponent<Canvas>();

            if (!_loggedHudCanvasCreated)
            {
                _loggedHudCanvasCreated = true;
                Debug.Log("[HudFactory] Created Abyss_HUDCanvas.");
            }

            if (Application.isPlaying)
                UnityEngine.Object.DontDestroyOnLoad(go);
        }

        // Enforce deterministic settings every time.
        existing.renderMode = RenderMode.ScreenSpaceOverlay;
        existing.sortingOrder = 1000;

        if (existing.GetComponent<CanvasScaler>() == null)
        {
            var scaler = existing.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        }

        if (existing.GetComponent<GraphicRaycaster>() == null)
            existing.gameObject.AddComponent<GraphicRaycaster>();

        if (!existing.gameObject.activeSelf)
            existing.gameObject.SetActive(true);

        return existing;
    }

    public static HealthBarUI EnsurePlayerHealthBar(Canvas canvas)
    {
        if (canvas == null)
            return null;

        DisableStrayHudCanvasImages(canvas);

        var rootTf = canvas.transform.Find(PlayerHealthBarName);
        GameObject rootGo;
        if (rootTf == null)
        {
            rootGo = new GameObject(PlayerHealthBarName);
            rootGo.transform.SetParent(canvas.transform, false);
        }
        else
        {
            rootGo = rootTf.gameObject;
        }

        if (!rootGo.activeSelf)
            rootGo.SetActive(true);

        var rootRt = rootGo.GetComponent<RectTransform>();
        if (rootRt == null)
            rootRt = rootGo.AddComponent<RectTransform>();

        // Root layout: top-center, fixed size (NOT stretching).
        rootRt.anchorMin = new Vector2(0.5f, 1f);
        rootRt.anchorMax = new Vector2(0.5f, 1f);
        rootRt.pivot = new Vector2(0.5f, 1f);
        rootRt.sizeDelta = new Vector2(320f, 22f);
        rootRt.anchoredPosition = new Vector2(0f, -20f);
        rootRt.localScale = Vector3.one;

        // Standard Unity Slider hierarchy (exactly as requested):
        // Abyss_PlayerHealthBar (RectTransform 320x22)
        //   Background (Image)
        //   Fill Area (RectTransform)
        //     Fill (Image)
        // Slider component lives on the root (so no extra child can accidentally stretch to canvas).
        var legacySliderChild = rootGo.transform.Find("Slider");
        if (legacySliderChild != null)
            legacySliderChild.gameObject.SetActive(false);

        var slider = rootGo.GetComponent<Slider>();
        if (slider == null)
            slider = rootGo.AddComponent<Slider>();

        slider.minValue = 0f;
        slider.maxValue = 1f; // HealthBarUI drives normalized [0..1]
        slider.value = 1f;
        slider.direction = Slider.Direction.LeftToRight;
        slider.wholeNumbers = false;
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };

        // Background (stretch to root)
        var bg = EnsureChild(rootGo.transform, "Background");
        var bgRt = EnsureRectTransform(bg);
        StretchToParent(bgRt);
        var bgImg = EnsureComponent<Image>(bg);
        // #1A1A1A
        bgImg.color = new Color(0.102f, 0.102f, 0.102f, 0.90f);
        bgImg.raycastTarget = false;

        // Fill Area (stretch with padding)
        var fillArea = EnsureChild(rootGo.transform, "Fill Area");
        var fillAreaRt = EnsureRectTransform(fillArea);
        fillAreaRt.anchorMin = Vector2.zero;
        fillAreaRt.anchorMax = Vector2.one;
        fillAreaRt.offsetMin = new Vector2(3f, 3f);
        fillAreaRt.offsetMax = new Vector2(-3f, -3f);
        fillAreaRt.localScale = Vector3.one;

        // Fill (stretch inside fill area)
        var fill = EnsureChild(fillArea.transform, "Fill");
        var fillRt = EnsureRectTransform(fill);
        StretchToParent(fillRt);
        var fillImg = EnsureComponent<Image>(fill);
        fillImg.color = new Color(0.85f, 0.1f, 0.1f, 0.9f);
        fillImg.raycastTarget = false;
        fillImg.type = Image.Type.Simple;

        slider.targetGraphic = bgImg;
        slider.fillRect = fillRt;
        slider.handleRect = null;

        var ui = rootGo.GetComponent<HealthBarUI>();
        if (ui == null)
            ui = rootGo.AddComponent<HealthBarUI>();

        if (!_loggedHealthBarLayout)
        {
            _loggedHealthBarLayout = true;
            Debug.Log($"[HUD] HealthBar root size={rootRt.sizeDelta} pos={rootRt.anchoredPosition} anchors={rootRt.anchorMin}/{rootRt.anchorMax}", rootGo);
        }

        return ui;
    }

    public static HealthBarUI EnsurePlayerHealthBar(Canvas canvas, PlayerHealth playerHealth)
    {
        var ui = EnsurePlayerHealthBar(canvas);
        if (ui == null)
            return null;

        var slider = ui.GetComponent<Slider>();
        if (slider == null)
            slider = ui.GetComponentInChildren<Slider>(true);

        if (slider != null && playerHealth != null)
        {
            slider.minValue = 0f;
            slider.maxValue = playerHealth.MaxHealth;
            slider.wholeNumbers = false;
            slider.value = playerHealth.CurrentHealth;
        }

        return ui;
    }

    private static void DisableStrayHudCanvasImages(Canvas canvas)
    {
        // Prevent any legacy/stray full-screen overlay images from hiding the game.
        // Only allow the health bar root to contribute images.
        if (canvas == null)
            return;

        var t = canvas.transform;
        for (int i = 0; i < t.childCount; i++)
        {
            var child = t.GetChild(i);
            if (child == null)
                continue;

            if (string.Equals(child.name, PlayerHealthBarName, StringComparison.Ordinal))
                continue;

            var img = child.GetComponent<Image>();
            if (img != null && img.enabled)
                child.gameObject.SetActive(false);
        }
    }

    private static GameObject EnsureChild(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (t != null)
            return t.gameObject;

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static RectTransform EnsureRectTransform(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt == null)
            rt = go.AddComponent<RectTransform>();
        rt.localScale = Vector3.one;
        return rt;
    }

    private static void StretchToParent(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    private static T EnsureComponent<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c == null)
            c = go.AddComponent<T>();
        return c;
    }
}
