# Phase 1 — Combat Input Authority (Attachment Pack)

Date: 2025-12-14
Repo: UnityAIAgent

This single file contains the requested sources in priority order, with clear separators.

## Note on “Player prefab Inspector”
I did not find a `Player*.prefab` in the project under `Assets/Game/Player/` (that folder contains only scripts).

Current behavior (from `GameBootstrapper`) is:
- It **finds** an existing player (by `PlayerInventory` or by tag), or
- It **instantiates** a serialized `playerPrefab` (if assigned), then
- It **adds** runtime components if missing:
  - `PlayerInventory`
  - `DebugPlayerMover_NewInput`
  - `PlayerHealth`
  - `SimplePlayerCombat`

Also: there is no `PlayerInput` component usage in the scripts shown below; input is currently **polled directly** inside gameplay scripts.

If you still want an Inspector screenshot, the fastest accurate capture is:
- Enter Play Mode, select the Player GameObject in the Hierarchy, and screenshot the Inspector (components top-to-bottom).

---

# 1) Player combat logic (current reality)

===== FILE: Assets/Game/Player/SimplePlayerCombat.cs =====

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

public class SimplePlayerCombat : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField] private int damage = 1;
    [SerializeField] private float range = 1.75f;
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("Target (optional)")]
    [SerializeField] private EnemyHealth selectedTarget;

    private void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;

        bool attackPressed = false;

        if (kb != null && kb.spaceKey.wasPressedThisFrame)
            attackPressed = true;

        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            attackPressed = true;

        if (attackPressed)
            TryAttack();
    }

    private void TryAttack()
    {
        if (TryAttackSelectedTarget())
            return;

        var hits = Physics.OverlapSphere(transform.position, Mathf.Max(0.1f, range), hitMask, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0)
            return;

        EnemyHealth best = null;
        float bestDistSq = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            var c = hits[i];
            if (c == null) continue;

            var eh = c.GetComponentInParent<EnemyHealth>();
            if (eh == null) continue;

            float d = (eh.transform.position - transform.position).sqrMagnitude;
            if (d < bestDistSq)
            {
                bestDistSq = d;
                best = eh;
            }
        }

        if (best == null)
            return;

        best.TakeDamage(Mathf.Max(1, damage));
    }

    private bool TryAttackSelectedTarget()
    {
        if (selectedTarget == null)
            return false;

        float distSq = (selectedTarget.transform.position - transform.position).sqrMagnitude;
        if (distSq > range * range)
            return false;

        selectedTarget.TakeDamage(Mathf.Max(1, damage));
        return true;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.1f, range));
    }
#endif
}
```

---

# 2) Player input / movement script

===== FILE: Assets/Game/Player/DebugPlayerMover_NewInput.cs =====

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

public class DebugPlayerMover_NewInput : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;

    private void Update()
    {
        // Works with the New Input System without needing an InputActionAsset.
        var kb = Keyboard.current;
        if (kb == null) return;

        float x = 0f;
        float z = 0f;

        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) z -= 1f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) z += 1f;

        Vector3 dir = new Vector3(x, 0f, z);
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        transform.position += dir * moveSpeed * Time.deltaTime;
    }
}
```

---

# 3) Player prefab Inspector

No `Player*.prefab` found in repo at the time of packaging. See the note at the top of this file.

---

# 4) DevCheats.cs

===== FILE: Assets/Game/Debug/DevCheats.cs =====

```csharp
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Abyssbound.DebugTools
{
    public sealed class DevCheats : MonoBehaviour
    {
#if ENABLE_INPUT_SYSTEM
        [Header("Keys")]
        [SerializeField] private Key spawnBossSigilKey = Key.F6;
        [SerializeField] private Key addTestSwordKey = Key.F7;
#endif

        private void Update()
        {
            if (!Application.isPlaying)
                return;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null)
                return;

            if (kb[spawnBossSigilKey].wasPressedThisFrame)
            {
                Debug.Log("[DevCheats] Spawn Sigil (F6)");
                DevCheatActions.SpawnBossSigil();
            }

            if (kb[addTestSwordKey].wasPressedThisFrame)
            {
                Debug.Log("[DevCheats] Add Test Sword (F7)");
                DevCheatActions.AddTestSword();
            }
#else
            // Legacy input path for projects set to "Input Manager" only.
            // F6/F7 bindings are fixed to preserve current behavior.
            if (Input.GetKeyDown(KeyCode.F6))
            {
                Debug.Log("[DevCheats] Spawn Sigil (F6)");
                DevCheatActions.SpawnBossSigil();
            }

            if (Input.GetKeyDown(KeyCode.F7))
            {
                Debug.Log("[DevCheats] Add Test Sword (F7)");
                DevCheatActions.AddTestSword();
            }
#endif
        }
    }
}
```

---

# 5) DevCheatActions.cs

===== FILE: Assets/Game/Debug/DevCheatActions.cs =====

```csharp
using System;
using System.Reflection;
using UnityEngine;

namespace Abyssbound.DebugTools
{
    public static class DevCheatActions
    {
        public static void SpawnBossSigil()
        {
            // Grant the boss sigil item directly to the player's inventory.
            // Item id in this repo: "AbyssalSigil".
            const string itemId = "AbyssalSigil";

            if (TryGrantInventoryItem(itemId, 1, out var details))
            {
                Debug.Log("[DevCheats] Granted AbyssalSigil x1");
                return;
            }

            Debug.LogWarning($"[DevCheats] Could not grant AbyssalSigil. Tried: {details}");
        }

        public static void AddTestSword()
        {
            const string itemId = "Test_Rare_Sword";

            if (TryGrantInventoryItem(itemId, 1, out var details))
            {
                Debug.Log("[DevCheats] Granted Test_Rare_Sword x1");
                return;
            }

            Debug.LogWarning($"[DevCheats] Could not grant Test_Rare_Sword. Tried: {details}");
        }

        private static PlayerInventory FindPlayerInventory()
        {
            // Prefer tagged Player when present.
            GameObject player = null;
            try { player = GameObject.FindWithTag("Player"); }
            catch { player = null; }

            if (player != null)
            {
                var inv = player.GetComponentInChildren<PlayerInventory>(true);
                if (inv != null) return inv;
            }

            // Fallback: any inventory instance (bootstrapper-created players included).
            var all = UnityEngine.Object.FindObjectsByType<PlayerInventory>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (all != null && all.Length > 0) return all[0];
            return null;
        }

        private static bool TryGrantInventoryItem(string itemId, int amount, out string tried)
        {
            tried = "";
            if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            {
                tried = "invalid itemId/amount";
                return false;
            }

            // Prefer the strongly-typed inventory if it exists.
            var playerInv = FindPlayerInventory();
            if (playerInv != null)
            {
                tried = "PlayerInventory." + GetTriedSignatures();
                return TryInvokeGrant(playerInv, itemId, amount);
            }

            // Fallback: look for a generic Inventory component (by name) if the project swaps implementations.
            var inventoryType = FindTypeByName("Inventory");
            if (inventoryType != null)
            {
                var invObj = FindFirstObjectByType(inventoryType);
                if (invObj != null)
                {
                    tried = "Inventory." + GetTriedSignatures();
                    return TryInvokeGrant(invObj, itemId, amount);
                }
            }

            tried = "no inventory instance found";
            return false;
        }

        private static string GetTriedSignatures()
        {
            // Required order (Add first, then AddItem).
            return "Add(string,int), Add(string), AddItem(string,int), AddItem(string)";
        }

        private static bool TryInvokeGrant(object inventoryInstance, string itemId, int amount)
        {
            if (inventoryInstance == null) return false;

            var type = inventoryInstance.GetType();

            // Add(string,int)
            if (TryInvoke(type, inventoryInstance, "Add", new[] { typeof(string), typeof(int) }, new object[] { itemId, amount })) return true;
            // Add(string)
            if (TryInvoke(type, inventoryInstance, "Add", new[] { typeof(string) }, new object[] { itemId })) return true;
            // AddItem(string,int)
            if (TryInvoke(type, inventoryInstance, "AddItem", new[] { typeof(string), typeof(int) }, new object[] { itemId, amount })) return true;
            // AddItem(string)
            if (TryInvoke(type, inventoryInstance, "AddItem", new[] { typeof(string) }, new object[] { itemId })) return true;

            return false;
        }

        private static bool TryInvoke(Type type, object instance, string methodName, Type[] parameterTypes, object[] args)
        {
            MethodInfo mi = null;
            try
            {
                mi = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic, null, parameterTypes, null);
            }
            catch
            {
                mi = null;
            }

            if (mi == null) return false;

            try
            {
                mi.Invoke(instance, args);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static UnityEngine.Object FindFirstObjectByType(Type type)
        {
            if (type == null) return null;
            var all = UnityEngine.Object.FindObjectsByType(type, FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (all == null || all.Length == 0) return null;
            return all[0];
        }

        private static Type FindTypeByName(string typeName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = null;
                try { t = asm.GetType(typeName); } catch { }
                if (t != null) return t;

                // Also try namespace-agnostic match:
                Type[] types = null;
                try { types = asm.GetTypes(); } catch { continue; }
                foreach (var cand in types)
                {
                    if (cand.Name == typeName) return cand;
                }
            }
            return null;
        }
    }
}
```

---

# 6) GameBootstrapper.cs

===== FILE: Assets/Game/Bootstrap/GameBootstrapper.cs =====

```csharp
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
```

---

# 7) Input Actions asset (export)

For attaching to ChatGPT, use the exact exported asset file copy here:

===== FILE COPY (exact): Docs/ChatGPT_Attachments/Files/InputSystem_Actions.inputactions =====

(It’s the full JSON export from `Assets/InputSystem_Actions.inputactions`, copied verbatim for easy attachment.)
