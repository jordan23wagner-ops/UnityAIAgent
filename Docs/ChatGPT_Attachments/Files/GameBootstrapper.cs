using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameBootstrapper : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool dontDestroyPlayer = true;

    [Header("UI")]
    [SerializeField] private bool dontDestroyHud = true;

    private static GameBootstrapper _instance;

    private GameObject _player;
    private GameObject _hudCanvas;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        if (Application.isPlaying)
        {
            DontDestroyRoot(gameObject);
        }

        SceneManager.sceneLoaded += OnSceneLoaded;

        EnsureFoundation();
    }

    private void OnDestroy()
    {
        if (_instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureFoundation();
    }

    private void EnsureFoundation()
    {
        _player = EnsurePlayer();
        EnsureEventSystem();
        _hudCanvas = EnsureHud(_player);
        EnsureCameraTarget(_player);
        CleanupDuplicatePlayers(_player);
    }

    private GameObject EnsurePlayer()
    {
        var existing = FindExistingPlayer();
        if (existing != null)
        {
            bool hadHealth = existing.GetComponent<PlayerHealth>() != null;
            bool hadCombat = existing.GetComponent<SimplePlayerCombat>() != null;
            EnsurePlayerComponents(existing);
            if (dontDestroyPlayer && Application.isPlaying)
            {
                DontDestroyRoot(existing);
            }
            TryTag(existing, playerTag);
            Debug.Log($"[Bootstrap] Found Player '{existing.name}'. PlayerHealth={(hadHealth ? "found" : "created")}, SimplePlayerCombat={(hadCombat ? "found" : "created")}", existing);
            return existing;
        }

        if (playerPrefab == null)
        {
            Debug.LogWarning("[Bootstrap] No player found and playerPrefab is not assigned.");
            return null;
        }

        var created = Instantiate(playerPrefab);
        created.name = playerPrefab.name;
        bool createdHealth = created.GetComponent<PlayerHealth>() == null;
        bool createdCombat = created.GetComponent<SimplePlayerCombat>() == null;
        EnsurePlayerComponents(created);
        TryTag(created, playerTag);
        if (dontDestroyPlayer && Application.isPlaying)
        {
            DontDestroyRoot(created);
        }
        Debug.Log($"[Bootstrap] Created Player '{created.name}'. PlayerHealth={(createdHealth ? "created" : "found")}, SimplePlayerCombat={(createdCombat ? "created" : "found")}", created);
        return created;
    }

    private GameObject FindExistingPlayer()
    {
        try
        {
            var byInventory = FindFirstObjectByType<PlayerInventory>();
            if (byInventory != null)
                return byInventory.gameObject;
        }
        catch { }

        if (!string.IsNullOrWhiteSpace(playerTag))
        {
            try
            {
                var byTag = GameObject.FindWithTag(playerTag);
                if (byTag != null)
                    return byTag;
            }
            catch { }
        }

        return null;
    }

    private static void EnsurePlayerComponents(GameObject player)
    {
        if (player == null) return;

        if (player.GetComponent<PlayerInventory>() == null)
            player.AddComponent<PlayerInventory>();

        if (player.GetComponent<DebugPlayerMover_NewInput>() == null)
            player.AddComponent<DebugPlayerMover_NewInput>();

        if (player.GetComponent<PlayerHealth>() == null)
            player.AddComponent<PlayerHealth>();

        if (player.GetComponent<SimplePlayerCombat>() == null)
            player.AddComponent<SimplePlayerCombat>();
    }

    private static void EnsureEventSystem()
    {
        var es = FindFirstObjectByType<EventSystem>();
        if (es != null)
            return;

        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();

#if ENABLE_INPUT_SYSTEM
        // New Input System
        go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        Debug.Log("[Bootstrap] Created EventSystem (InputSystemUIInputModule).", go);
#else
        // Legacy input for UI
        go.AddComponent<StandaloneInputModule>();
        Debug.Log("[Bootstrap] Created EventSystem (StandaloneInputModule).", go);
#endif
    }

    private GameObject EnsureHud(GameObject player)
    {
        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            var canvasGo = new GameObject("HUDCanvas");
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
            Debug.Log("[Bootstrap] Created Canvas 'HUDCanvas' (Screen Space - Overlay).", canvasGo);

            if (dontDestroyHud && Application.isPlaying)
            {
                DontDestroyRoot(canvasGo);
            }
        }

        // Ensure HealthBarUI exists.
        var ui = FindFirstObjectByType<HealthBarUI>();
        if (ui == null)
        {
            ui = CreateDefaultHealthBarUI(canvas.transform);
            Debug.Log("[Bootstrap] Created HealthBarUI under HUDCanvas.", ui.gameObject);
        }

        // Ensure HealthBarUI is under a Canvas.
        var uiCanvas = ui.GetComponentInParent<Canvas>();
        if (uiCanvas == null)
        {
            ui.transform.SetParent(canvas.transform, false);
            Debug.Log("[Bootstrap] Reparented existing HealthBarUI under HUDCanvas.", ui.gameObject);
        }
        else if (!ReferenceEquals(uiCanvas, canvas) && uiCanvas.renderMode != RenderMode.WorldSpace)
        {
            // If we found a different screen-space canvas, prefer to keep it there.
            canvas = uiCanvas;
        }

        // Ensure the UI actually has a slider to drive.
        if (!EnsureHealthBarSlider(ui))
        {
            // Worst-case: create a fresh one under the canvas.
            var replacement = CreateDefaultHealthBarUI(canvas.transform);
            Debug.LogWarning("[Bootstrap] HealthBarUI existed but had no Slider; created a replacement HealthBarUI.", replacement.gameObject);
            ui = replacement;
        }

        // Bind HUD to player health.
        BindHud(ui, player);
        return canvas.gameObject;
    }

    private static HealthBarUI CreateDefaultHealthBarUI(Transform parent)
    {
        var barGo = new GameObject("HealthBar");
        barGo.transform.SetParent(parent, false);

        var slider = barGo.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;

        // Minimal visuals (built-in UI Images)
        var bg = new GameObject("Background");
        bg.transform.SetParent(barGo.transform, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.6f);

        var fill = new GameObject("Fill");
        fill.transform.SetParent(barGo.transform, false);
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.8f, 0.1f, 0.1f, 0.9f);

        slider.targetGraphic = bgImg;
        slider.fillRect = fillImg.rectTransform;

        // Layout
        var rt = barGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -20f);
        rt.sizeDelta = new Vector2(300f, 20f);

        bgImg.rectTransform.anchorMin = Vector2.zero;
        bgImg.rectTransform.anchorMax = Vector2.one;
        bgImg.rectTransform.offsetMin = Vector2.zero;
        bgImg.rectTransform.offsetMax = Vector2.zero;

        fillImg.rectTransform.anchorMin = Vector2.zero;
        fillImg.rectTransform.anchorMax = Vector2.one;
        fillImg.rectTransform.offsetMin = Vector2.zero;
        fillImg.rectTransform.offsetMax = Vector2.zero;

        var ui = barGo.AddComponent<HealthBarUI>();
        return ui;
    }

    private static bool EnsureHealthBarSlider(HealthBarUI ui)
    {
        if (ui == null) return false;
        var slider = ui.GetComponentInChildren<Slider>(true);
        if (slider != null)
            return true;

        // If HealthBarUI exists without any UI, create a minimal slider under it.
        var sliderGo = new GameObject("HealthBar_Slider");
        sliderGo.transform.SetParent(ui.transform, false);
        var created = sliderGo.AddComponent<Slider>();
        created.minValue = 0f;
        created.maxValue = 1f;
        created.value = 1f;

        var bg = new GameObject("Background");
        bg.transform.SetParent(sliderGo.transform, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.6f);

        var fill = new GameObject("Fill");
        fill.transform.SetParent(sliderGo.transform, false);
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.8f, 0.1f, 0.1f, 0.9f);

        created.targetGraphic = bgImg;
        created.fillRect = fillImg.rectTransform;

        var rt = sliderGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -20f);
        rt.sizeDelta = new Vector2(300f, 20f);

        bgImg.rectTransform.anchorMin = Vector2.zero;
        bgImg.rectTransform.anchorMax = Vector2.one;
        bgImg.rectTransform.offsetMin = Vector2.zero;
        bgImg.rectTransform.offsetMax = Vector2.zero;

        fillImg.rectTransform.anchorMin = Vector2.zero;
        fillImg.rectTransform.anchorMax = Vector2.one;
        fillImg.rectTransform.offsetMin = Vector2.zero;
        fillImg.rectTransform.offsetMax = Vector2.zero;

        return true;
    }

    private static void BindHud(HealthBarUI ui, GameObject player)
    {
        if (ui == null) return;
        var health = player != null ? player.GetComponent<PlayerHealth>() : null;
        ui.Bind(health);
        Debug.Log($"[Bootstrap] Bound HealthBarUI to PlayerHealth: {(health != null ? "ok" : "missing")}", ui.gameObject);
    }

    private static void EnsureCameraTarget(GameObject player)
    {
        if (player == null) return;

        var cam = Camera.main;
        if (cam == null) return;

        var follow = cam.GetComponent<TopDownFollowCamera>();
        if (follow == null) return;

        follow.SetTarget(player.transform);
        Debug.Log($"[Bootstrap] Assigned camera target to Player '{player.name}'.", cam.gameObject);
    }

    private static void CleanupDuplicatePlayers(GameObject keep)
    {
        try
        {
            var allInventories = FindObjectsByType<PlayerInventory>(FindObjectsSortMode.None);
            if (allInventories == null) return;

            for (int i = 0; i < allInventories.Length; i++)
            {
                var inv = allInventories[i];
                if (inv == null) continue;
                if (keep != null && inv.gameObject == keep) continue;

                Destroy(inv.gameObject);
            }
        }
        catch { }
    }

    private static void TryTag(GameObject go, string tag)
    {
        if (go == null) return;
        if (string.IsNullOrWhiteSpace(tag)) return;

        try
        {
            if (go.CompareTag(tag))
                return;
            go.tag = tag;
            Debug.Log($"[Bootstrap] Tagged '{go.name}' as '{tag}'.", go);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Bootstrap] Could not set tag '{tag}' on '{go.name}': {ex.Message}", go);
        }
    }

    private static void DontDestroyRoot(GameObject obj)
    {
        if (obj == null)
            return;

        var t = obj.transform;
        if (t != null && t.parent != null)
            t.SetParent(null, true);

        DontDestroyOnLoad(obj);
    }
}
