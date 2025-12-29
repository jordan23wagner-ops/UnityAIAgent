using Game.Input;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using Game.Systems;

using Abyssbound.DebugTools;
using Abyssbound.Loot;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[ExecuteAlways]
public class GameBootstrapper : MonoBehaviour
{
    // NOTE (Dec 2025): HUD health bar layout hardening
    // - EnsureHud() now ignores world-space canvases and enforces a deterministic bottom-center layout for HealthBarUI
    // - This prevents our world-space runtime UI canvas from affecting player HUD sizing

    [Header("Player")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool dontDestroyPlayer = true;

    [Header("UI")]

#if ENABLE_INPUT_SYSTEM
    [Header("Input")]
    [SerializeField] private InputActionAsset playerInputActions;
    [SerializeField] private string playerActionMap = "Player";
#endif

    private static GameBootstrapper _instance;

    private GameObject _player;
    private GameObject _hudCanvas;

    private static bool _loggedHudBound;
    private static bool _loggedLegacyHudDisabled;
    private static bool _loggedUsingAbyssHud;

    private static bool _loggedBootAwake;
    private static bool _loggedBootEnsureFoundation;
    private static bool _loggedBootEnsureHud;
    private static bool _loggedBootLegacyHudDisabled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetBootstrapperStaticsForPlay()
    {
        // Domain reload may be disabled: reset one-time log guards for each play session.
        _loggedBootAwake = false;
        _loggedBootEnsureFoundation = false;
        _loggedBootEnsureHud = false;
        _loggedBootLegacyHudDisabled = false;

        _loggedHudBound = false;
        _loggedLegacyHudDisabled = false;
        _loggedUsingAbyssHud = false;
    }

    private void Awake()
    {
        if (!Application.isPlaying)
        {
            EnsureEditorGuards();
            return;
        }

        if (!_loggedBootAwake)
        {
            _loggedBootAwake = true;
            Debug.Log("[BOOT] GameBootstrapper Awake", this);
        }

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

    private void OnEnable()
    {
        if (!Application.isPlaying)
            EnsureEditorGuards();
    }

    private void Update()
    {
        if (!Application.isPlaying)
            EnsureEditorGuards();
    }

    private static void EnsureEditorGuards()
    {
        if (Application.isPlaying)
            return;

        GameObject guards = null;
        try { guards = GameObject.Find("Abyss_EditorGuards"); }
        catch { }

        if (guards == null)
        {
            guards = new GameObject("Abyss_EditorGuards");
            guards.hideFlags = HideFlags.DontSaveInEditor;
        }

        if (guards.GetComponent<LegacyHudCanvasHider>() == null)
            guards.AddComponent<LegacyHudCanvasHider>();
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
        if (!_loggedBootEnsureFoundation)
        {
            _loggedBootEnsureFoundation = true;
            Debug.Log("[BOOT] EnsureFoundation", this);
        }

        EnsureDevCheats();
        _player = EnsurePlayer();
        EnsureEventSystem();
        _hudCanvas = EnsureHud(_player);
        DisableLegacyHudCanvasesOnce();
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
#if ENABLE_INPUT_SYSTEM
            EnsurePlayerInputRuntime(existing);
#endif
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
    #if ENABLE_INPUT_SYSTEM
        EnsurePlayerInputRuntime(created);
    #endif
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
            var byInventory = PlayerInventoryResolver.GetOrFind();
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

        if (player.GetComponent<PlayerHealth>() == null)
            player.AddComponent<PlayerHealth>();

        if (player.GetComponent<SimplePlayerCombat>() == null)
            player.AddComponent<SimplePlayerCombat>();

#if ENABLE_INPUT_SYSTEM
        // Ensure PlayerInput first
        var pi = player.GetComponent<PlayerInput>();
        if (pi == null)
        {
            pi = player.AddComponent<PlayerInput>();
            Debug.Log("[Bootstrap] Added PlayerInput.", player);
        }
#endif

        // Ensure PlayerInputAuthority exists (authoritative input listener)
        if (player.GetComponent<PlayerInputAuthority>() == null)
        {
            player.AddComponent<PlayerInputAuthority>();
            Debug.Log("[Bootstrap] Added PlayerInputAuthority.", player);
        }

        // Ensure movement motor exists
        if (player.GetComponent<PlayerMovementMotor>() == null)
        {
            player.AddComponent<PlayerMovementMotor>();
            Debug.Log("[Bootstrap] Added PlayerMovementMotor.", player);
        }

        // Inventory diagnostics (quiet by default; enable in PlayerInventoryResolver to troubleshoot duplicate inventories).
        try
        {
            Game.Systems.PlayerInventoryResolver.LogAllInventoriesOnStart("GameBootstrapper.EnsurePlayerComponents");
            Game.Systems.PlayerInventoryResolver.EnforceSingleAuthoritativeInventoryOptional(destroyDuplicateComponents: false);
        }
        catch { }

        if (player.GetComponent<CombatLoopController>() == null)
        {
            player.AddComponent<CombatLoopController>();
            Debug.Log("[Bootstrap] Added CombatLoopController.", player);
        }

        // Ensure click-to-move exists
        if (player.GetComponent<ClickToMoveController>() == null)
        {
            player.AddComponent<ClickToMoveController>();
            Debug.Log("[Bootstrap] Added ClickToMoveController.", player);
        }
    }

#if ENABLE_INPUT_SYSTEM
    private void EnsurePlayerInputRuntime(GameObject player)
    {
        if (player == null)
            return;

        var allPlayerInputs = player.GetComponents<PlayerInput>();
        if (allPlayerInputs != null && allPlayerInputs.Length > 1)
            Debug.LogWarning($"[Bootstrap] Multiple PlayerInput components found on '{player.name}'. Using the first.", player);

        var pi = (allPlayerInputs != null && allPlayerInputs.Length > 0) ? allPlayerInputs[0] : null;
        if (pi == null)
        {
            // This should be rare because EnsurePlayerComponents() adds PlayerInput first.
            pi = player.AddComponent<PlayerInput>();
            Debug.Log("[Bootstrap] Added PlayerInput (runtime fallback).", player);
        }

        pi.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
        pi.defaultActionMap = string.IsNullOrWhiteSpace(playerActionMap) ? "Player" : playerActionMap;

        if (pi.actions == null)
            pi.actions = ResolvePlayerInputActions();

        if (pi.actions == null)
        {
            Debug.LogError("[Bootstrap] PlayerInput has no InputActionAsset assigned. Movement/attack input will be inactive.", player);
            return;
        }

        try { pi.actions.Enable(); } catch { }
        try { pi.ActivateInput(); } catch { }

        Debug.Log($"[Bootstrap] PlayerInput configured. ActionMap='{pi.defaultActionMap}'.", player);

        var pi2 = player.GetComponent<PlayerInput>();
        Debug.Log($"[Bootstrap] PlayerInput actions={(pi2 != null && pi2.actions != null ? pi2.actions.name : "NULL")} currentMap={(pi2 != null ? pi2.currentActionMap?.name : "NULL")}", player);

        // If PlayerInputAuthority disabled itself due to ordering, re-enable now.
        var ia = player.GetComponent<PlayerInputAuthority>();
        if (ia != null && !ia.enabled)
            ia.enabled = true;
    }

    private InputActionAsset ResolvePlayerInputActions()
    {
        if (playerInputActions != null)
            return playerInputActions;

        var fromResources = Resources.Load<InputActionAsset>("Input/InputSystem_Actions");
        if (fromResources != null)
            return fromResources;

#if UNITY_EDITOR
        try
        {
            return UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Input/InputSystem_Actions.inputactions");
        }
        catch
        {
            return null;
        }
#else
        // Build fallback actions in code so a player can still move/act in builds
        // even if the asset isn't in Resources and wasn't wired via inspector.
        return CreateFallbackInputActions();
#endif
    }

    private static InputActionAsset CreateFallbackInputActions()
    {
        var asset = ScriptableObject.CreateInstance<InputActionAsset>();
        var map = new InputActionMap("Player");

        var cameraPan = map.AddAction("CameraPan", InputActionType.Value, null, null, null, "Vector2");
        cameraPan.AddCompositeBinding("Dpad")
            .With("Up", "<Keyboard>/w")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/s")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/a")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/d")
            .With("Right", "<Keyboard>/rightArrow");

        var click = map.AddAction("Click", InputActionType.Button, null, null, null, "Button");
        click.AddBinding("<Mouse>/leftButton");

        var pointerPosition = map.AddAction("PointerPosition", InputActionType.Value, null, null, null, "Vector2");
        pointerPosition.AddBinding("<Pointer>/position");

        var attackDebug = map.AddAction("AttackDebug", InputActionType.Button, null, null, null, "Button");
        attackDebug.AddBinding("<Keyboard>/space");

        asset.AddActionMap(map);
        return asset;
    }
#endif

    private static bool _devCheatsStartupLogged;

    private static void EnsureDevCheats()
    {
        var cheats = FindFirstObjectByType<DevCheats>();
        if (cheats == null)
        {
            var systemsRoot = GameObject.Find("[SYSTEMS]") ?? new GameObject("[SYSTEMS]");

            var child = systemsRoot.transform.Find("DevCheats");
            GameObject cheatsGo;
            if (child == null)
            {
                cheatsGo = new GameObject("DevCheats");
                cheatsGo.transform.SetParent(systemsRoot.transform, false);
            }
            else
            {
                cheatsGo = child.gameObject;
            }

            cheats = cheatsGo.GetComponent<DevCheats>();
            if (cheats == null)
                cheats = cheatsGo.AddComponent<DevCheats>();

            Debug.Log("[Bootstrap] Ensured DevCheats exists.", cheatsGo);
        }

        if (!_devCheatsStartupLogged)
        {
            if (LootQaSettings.DebugLogsEnabled)
                Debug.Log("[DevCheats] Active: F6 Spawn Sigil, F7 Spawn Selected Magic+ Items", cheats);
            _devCheatsStartupLogged = true;
        }
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
        if (!_loggedBootEnsureHud)
        {
            _loggedBootEnsureHud = true;
            Debug.Log("[BOOT] EnsureHud invoked", this);
        }

        var canvas = HudFactory.EnsureHudCanvas();
        var health = player != null ? player.GetComponent<PlayerHealth>() : null;
        var ui = HudFactory.EnsurePlayerHealthBar(canvas, health);

        if (!_loggedUsingAbyssHud)
        {
            _loggedUsingAbyssHud = true;
            Debug.Log("[HUD] Using Abyss_HUDCanvas", canvas);
        }

        // Bind HUD to player health.
        BindHud(ui, player);
        return canvas != null ? canvas.gameObject : null;
    }

    private void DisableLegacyHudCanvasesOnce()
    {
        if (_loggedLegacyHudDisabled)
            return;

        Canvas[] canvases;
        try
        {
            canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }
        catch
        {
            return;
        }

        for (int i = 0; i < canvases.Length; i++)
        {
            var c = canvases[i];
            if (c == null)
                continue;

            if (string.Equals(c.gameObject.name, "Abyss_HUDCanvas", StringComparison.Ordinal))
                continue;

            if (!string.Equals(c.gameObject.name, "HUDCanvas", StringComparison.Ordinal))
                continue;

            c.gameObject.SetActive(false);
            _loggedLegacyHudDisabled = true;

            if (!_loggedBootLegacyHudDisabled)
            {
                _loggedBootLegacyHudDisabled = true;
                Debug.LogWarning("[BOOT] Disabled legacy HUDCanvas", c.gameObject);
            }

            return;
        }
    }

    private static bool LooksLikeFullscreenOverlay(Canvas canvas)
    {
        if (canvas == null)
            return false;

        Image[] images;
        try
        {
            images = canvas.GetComponentsInChildren<Image>(true);
        }
        catch
        {
            return false;
        }

        for (int i = 0; i < images.Length; i++)
        {
            var img = images[i];
            if (img == null || !img.enabled)
                continue;

            var rt = img.rectTransform;
            if (rt == null)
                continue;

            bool stretchAnchors = rt.anchorMin.x <= 0.001f && rt.anchorMin.y <= 0.001f && rt.anchorMax.x >= 0.999f && rt.anchorMax.y >= 0.999f;
            bool hugeSize = rt.sizeDelta.x >= 900f || rt.sizeDelta.y >= 500f;
            if (stretchAnchors || hugeSize)
                return true;
        }

        return false;
    }

    private static void EnsureHudHealthBarObjectLayout(GameObject healthBar)
    {
        if (healthBar == null)
            return;

        if (!healthBar.activeSelf)
            healthBar.SetActive(true);

        var rt = healthBar.GetComponent<RectTransform>();
        if (rt == null)
            rt = healthBar.AddComponent<RectTransform>();

        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(320f, 22f);
        rt.anchoredPosition = new Vector2(0f, 10f);
        rt.localScale = Vector3.one;
    }

    private static void EnsureHudSliderLayout(HealthBarUI ui)
    {
        if (ui == null)
            return;

        var canvas = ui.GetComponentInParent<Canvas>();
        if (canvas == null)
            return;

        // IMPORTANT: do not touch world-space canvases.
        if (canvas.renderMode == RenderMode.WorldSpace)
            return;

        var slider = ui.GetComponentInChildren<Slider>(true);
        if (slider == null)
            return;

        var sliderRt = slider.GetComponent<RectTransform>();
        if (sliderRt == null)
            sliderRt = slider.gameObject.AddComponent<RectTransform>();

        sliderRt.anchorMin = Vector2.zero;
        sliderRt.anchorMax = Vector2.one;
        sliderRt.offsetMin = Vector2.zero;
        sliderRt.offsetMax = Vector2.zero;
        sliderRt.localScale = Vector3.one;
    }

    private static bool EnsureHudHealthBarVisuals(HealthBarUI ui, Transform canvas)
    {
        if (ui == null)
            return false;

        if (canvas == null)
            return false;

        var uiCanvas = ui.GetComponentInParent<Canvas>();
        if (uiCanvas == null || uiCanvas.renderMode == RenderMode.WorldSpace)
            return true; // don't touch world-space

        var slider = ui.GetComponentInChildren<Slider>(true);
        if (slider == null)
            return false;

        // If Slider exists but core visuals are missing, treat as incomplete.
        if (slider.fillRect == null || slider.targetGraphic == null)
            return false;

        return true;
    }

    private static void EnsureHudHealthBarLayout(HealthBarUI ui)
    {
        if (ui == null)
            return;

        var canvas = ui.GetComponentInParent<Canvas>();
        if (canvas == null)
            return;

        // IMPORTANT: do not touch world-space canvases.
        if (canvas.renderMode == RenderMode.WorldSpace)
            return;

        var rt = ui.GetComponent<RectTransform>();
        if (rt == null)
            rt = ui.gameObject.AddComponent<RectTransform>();

        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 10f);
        rt.sizeDelta = new Vector2(320f, 22f);
        rt.localScale = Vector3.one;
    }

    private static HealthBarUI CreateDefaultHealthBarUI(Transform parent)
    {
        var barGo = new GameObject("HealthBar");
        barGo.transform.SetParent(parent, false);

        // Parent layout (deterministic sizing)
        var barRt = barGo.GetComponent<RectTransform>();
        if (barRt == null)
            barRt = barGo.AddComponent<RectTransform>();
        barRt.anchorMin = new Vector2(0.5f, 0f);
        barRt.anchorMax = new Vector2(0.5f, 0f);
        barRt.pivot = new Vector2(0.5f, 0f);
        barRt.anchoredPosition = new Vector2(0f, 10f);
        barRt.sizeDelta = new Vector2(320f, 22f);
        barRt.localScale = Vector3.one;

        // Slider as a child that stretches to fill the HealthBar.
        var sliderGo = new GameObject("Slider");
        sliderGo.transform.SetParent(barGo.transform, false);
        var sliderRt = sliderGo.AddComponent<RectTransform>();
        sliderRt.anchorMin = Vector2.zero;
        sliderRt.anchorMax = Vector2.one;
        sliderRt.offsetMin = Vector2.zero;
        sliderRt.offsetMax = Vector2.zero;
        sliderRt.localScale = Vector3.one;

        var slider = sliderGo.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;

        // Minimal visuals (built-in UI Images)
        var bg = new GameObject("Background");
        bg.transform.SetParent(sliderGo.transform, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.6f);

        var fill = new GameObject("Fill");
        fill.transform.SetParent(sliderGo.transform, false);
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.8f, 0.1f, 0.1f, 0.9f);

        slider.targetGraphic = bgImg;
        slider.fillRect = fillImg.rectTransform;

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

        // If HealthBarUI exists without any Slider, create a minimal child Slider that stretches to the parent.
        var sliderGo = new GameObject("Slider");
        sliderGo.transform.SetParent(ui.transform, false);
        var sliderRt = sliderGo.AddComponent<RectTransform>();
        sliderRt.anchorMin = Vector2.zero;
        sliderRt.anchorMax = Vector2.one;
        sliderRt.offsetMin = Vector2.zero;
        sliderRt.offsetMax = Vector2.zero;
        sliderRt.localScale = Vector3.one;

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

        if (!_loggedHudBound)
        {
            _loggedHudBound = true;
            Debug.Log($"[Bootstrap] Bound HealthBarUI to PlayerHealth: {(health != null ? "ok" : "missing")}", ui.gameObject);
        }
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

        if (cam.GetComponent<CameraPanController>() == null)
        {
            cam.gameObject.AddComponent<CameraPanController>();
            Debug.Log("[Bootstrap] Added CameraPanController to Main Camera.", cam.gameObject);
        }
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
