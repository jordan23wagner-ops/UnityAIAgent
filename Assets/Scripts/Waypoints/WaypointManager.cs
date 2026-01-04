using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abyss.Waypoints
{
    /*
     * Waypoints v1
     *
     * Run Tools/Waypoints/Setup Waypoints System (One-Click) once per project.
     * Then: tag your player as Player, place waypoint prefabs in a scene,
     * press Play, walk into a waypoint to activate, press F6 to teleport.
     */

    [DisallowMultipleComponent]
    public sealed class WaypointManager : MonoBehaviour
    {
        public static WaypointManager Instance { get; private set; }

        // A) Input-gating API requested for click-to-move guard.
        public static bool IsMenuOpen { get; private set; }
        public static float SuppressGameplayClicksUntil { get; private set; }

        // Back-compat for earlier polish iterations (safe alias).
        public static bool WaypointsUIOpen { get; private set; }

        private const string PlayerPrefsKey = "ABYSS_WAYPOINTS_V1";

        [Header("Registry")]
        [SerializeField] private WaypointRegistrySO registry;

        [Header("UI")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F6;

        private bool _menuOpen;
        private Rect _windowRect = new Rect(20, 20, 320, 420);

        private HashSet<string> _activatedIds;
        private WaypointSaveData _saveData;

        private GameObject _cachedPlayer;
        private bool _warnedTownUnavailableThisOpen;

        // B) Scene-based discovery cache (do not depend on registry runtime lists).
        private readonly Dictionary<string, WaypointComponent> _byId = new Dictionary<string, WaypointComponent>(StringComparer.OrdinalIgnoreCase);
        private readonly List<WaypointComponent> _all = new List<WaypointComponent>(64);
        private float _nextDiscoveryRefreshTime;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _saveData = WaypointSaveData.Load(PlayerPrefsKey);
            _activatedIds = _saveData.ToSetOrdinalIgnoreCase();

            try
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                SceneManager.sceneLoaded += OnSceneLoaded;
            }
            catch { }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            SetMenuOpen(false);

            try { SceneManager.sceneLoaded -= OnSceneLoaded; } catch { }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Ensure we refresh after scene transitions if the menu is already open.
            if (_menuOpen)
                RefreshDiscovery(force: true);
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                _menuOpen = !_menuOpen;
                SetMenuOpen(_menuOpen);

                if (_menuOpen)
                {
                    _warnedTownUnavailableThisOpen = false;
                    RefreshDiscovery(force: true);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[Waypoints] Menu opened. Discovered={_byId.Count} Activated={_activatedIds?.Count ?? 0}", this);
#endif
                }
            }
            else
            {
                // Keep statics honest even if the menu is closed by button.
                SetMenuOpen(_menuOpen);
            }
        }

        public void OpenMenu()
        {
            _menuOpen = true;
            SetMenuOpen(true);

            // Match the F6 open behavior.
            _warnedTownUnavailableThisOpen = false;
            RefreshDiscovery(force: true);

            // Reduce click-to-move bleed-through when opened via world click.
            SuppressGameplayClicksUntil = Time.unscaledTime + 0.10f;
        }

        private static void SetMenuOpen(bool open)
        {
            IsMenuOpen = open;
            WaypointsUIOpen = open;
        }

        public bool IsActivated(string id)
        {
            if (_activatedIds == null || string.IsNullOrWhiteSpace(id))
                return false;
            return _activatedIds.Contains(id);
        }

        public bool TryGetWaypoint(string id, out WaypointComponent waypoint)
        {
            waypoint = null;
            if (string.IsNullOrWhiteSpace(id))
                return false;

            RefreshDiscovery(force: false);
            if (_byId.TryGetValue(id, out waypoint) && waypoint != null)
                return true;

            // Still keep registry as a fallback resolver.
            if (registry == null)
                return false;

            try { return registry.TryGet(id, out waypoint) && waypoint != null; }
            catch { waypoint = null; return false; }
        }

        public void Activate(string waypointId)
        {
            TryGetWaypoint(waypointId, out var wp);
            Activate(waypointId, wp);
        }

        public void Activate(string waypointId, WaypointComponent source)
        {
            if (string.IsNullOrWhiteSpace(waypointId))
                return;

            _activatedIds ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (_activatedIds.Contains(waypointId))
                return;

            _activatedIds.Add(waypointId);
            _saveData ??= new WaypointSaveData();
            _saveData.Add(waypointId);
            _saveData.Save(PlayerPrefsKey);

            // Ensure activation uses the same discovered waypoint data to resolve DisplayName.
            string name = waypointId;
            if (source != null)
            {
                name = source.DisplayName;
            }
            else
            {
                RefreshDiscovery(force: true);
                if (_byId.TryGetValue(waypointId, out var discovered) && discovered != null)
                {
                    name = discovered.DisplayName;
                }
                else
                {
                    try
                    {
                        if (registry != null && registry.TryGet(waypointId, out var wp) && wp != null)
                            name = wp.DisplayName;
                    }
                    catch { }
                }
            }

            Debug.Log($"Waypoint activated: {name} ({waypointId})", this);
        }

        private void OnGUI()
        {
            if (!_menuOpen)
                return;

            // Refresh discovery while drawing (per requirement) so UI never depends on registry runtime lists.
            RefreshDiscovery(force: false);

            // Prevent common click-through when using IMGUI.
            if (ShouldConsumeMouseEvent(Event.current))
                Event.current.Use();

            // Also consume mouse events briefly after teleport to reduce click-to-move bleed-through.
            if (Time.unscaledTime < SuppressGameplayClicksUntil && Event.current != null && Event.current.isMouse)
                Event.current.Use();

            _windowRect = GUI.Window(GetInstanceID(), _windowRect, DrawWindow, "Waypoints");
        }

        private static bool ShouldConsumeMouseEvent(Event e)
        {
            if (e == null)
                return false;
            if (!e.isMouse)
                return false;
            return e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.MouseDrag;
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.BeginVertical();

            if (GUILayout.Button("Close", GUILayout.Height(28)))
            {
                _menuOpen = false;
                SetMenuOpen(false);
                GUILayout.EndVertical();
                GUI.DragWindow();
                return;
            }

            var townWp = ResolveTownWaypoint();
            var townFallback = ResolveTownSpawnFallback();

            bool showTown = townWp != null || townFallback != null;
            if (showTown)
            {
                if (GUILayout.Button("Teleport to Town", GUILayout.Height(28)))
                {
                    // A) Suppress gameplay clicks right after teleport.
                    SuppressGameplayClicksUntil = Time.unscaledTime + 0.15f;

                    if (townWp != null)
                        TeleportToWaypoint(townWp);
                    else
                        TeleportToTransform(townFallback);
                }
            }
            else
            {
                if (!_warnedTownUnavailableThisOpen)
                {
                    _warnedTownUnavailableThisOpen = true;
                    Debug.LogWarning("[Waypoints] Town destination unavailable (no activated IsTown waypoint and no object tagged TownSpawn).", this);
                }
            }

            GUILayout.Space(8);

            var activated = GetActivatedWaypointsFromDiscovery();
            if (activated.Count == 0)
            {
                GUILayout.Label("No activated waypoints yet.");
                GUILayout.Label("Enter a waypoint trigger to activate.");
            }
            else
            {
                for (int i = 0; i < activated.Count; i++)
                {
                    var wp = activated[i];
                    if (wp == null) continue;
                    if (GUILayout.Button(wp.DisplayName, GUILayout.Height(26)))
                    {
                        SuppressGameplayClicksUntil = Time.unscaledTime + 0.15f;
                        TeleportToWaypoint(wp);
                    }
                }
            }

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void RefreshDiscovery(bool force)
        {
            if (!force && Time.unscaledTime < _nextDiscoveryRefreshTime)
                return;

            _nextDiscoveryRefreshTime = Time.unscaledTime + 0.25f;

            _byId.Clear();
            _all.Clear();

            WaypointComponent[] found;
            found = UnityEngine.Object.FindObjectsByType<WaypointComponent>(FindObjectsSortMode.None);

            if (found == null)
                return;

            for (int i = 0; i < found.Length; i++)
            {
                var wp = found[i];
                if (wp == null)
                    continue;

                _all.Add(wp);

                var id = wp.Id;
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                if (!_byId.ContainsKey(id))
                    _byId.Add(id, wp);
            }
        }

        private List<WaypointComponent> GetActivatedWaypointsFromDiscovery()
        {
            var list = new List<WaypointComponent>(16);
            if (_activatedIds == null || _activatedIds.Count == 0)
                return list;

            RefreshDiscovery(force: false);

            for (int i = 0; i < _all.Count; i++)
            {
                var wp = _all[i];
                if (wp == null) continue;
                if (wp.IsTown) continue;
                if (string.IsNullOrWhiteSpace(wp.Id)) continue;
                if (_activatedIds.Contains(wp.Id))
                    list.Add(wp);
            }

            list.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
            return list;
        }

        private WaypointComponent ResolveTownWaypoint()
        {
            if (_activatedIds == null || _activatedIds.Count == 0)
                return null;

            RefreshDiscovery(force: false);

            for (int i = 0; i < _all.Count; i++)
            {
                var wp = _all[i];
                if (wp == null) continue;
                if (!wp.IsTown) continue;
                if (string.IsNullOrWhiteSpace(wp.Id)) continue;
                if (_activatedIds.Contains(wp.Id))
                    return wp;
            }

            // Keep existing registry-based resolution as a fallback.
            try
            {
                if (registry != null)
                    return registry.FindActivatedTown(_activatedIds);
            }
            catch { }

            return null;
        }

        private Transform ResolveTownSpawnFallback()
        {
            // Tag may not exist; handle gracefully.
            try
            {
                var go = GameObject.FindGameObjectWithTag("TownSpawn");
                return go != null ? go.transform : null;
            }
            catch
            {
                return null;
            }
        }

        private void TeleportToWaypoint(WaypointComponent waypoint)
        {
            if (waypoint == null)
                return;

            TeleportToTransform(waypoint.GetSpawnPoint());
        }

        private void TeleportToTransform(Transform destination)
        {
            if (destination == null)
                return;

            var player = GetPlayer();
            if (player == null)
                return;

            player.transform.position = destination.position;

            // Keep previous kinematic-safe velocity reset behavior.
            try
            {
                var rb = player.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    if (!rb.isKinematic)
                    {
                        try { rb.linearVelocity = Vector3.zero; } catch { }
                        try { rb.linearVelocity = Vector3.zero; } catch { }
                    }
                    else
                    {
                        try { rb.Sleep(); } catch { }
                    }
                }
            }
            catch { }

            try
            {
                var rb2d = player.GetComponent<Rigidbody2D>();
                if (rb2d != null)
                {
                    bool kinematic = false;
                    try { kinematic = rb2d.bodyType == RigidbodyType2D.Kinematic; } catch { kinematic = false; }

                    if (!kinematic)
                    {
                        try { rb2d.linearVelocity = Vector2.zero; } catch { }
                        try { rb2d.linearVelocity = Vector2.zero; } catch { }
                    }
                    else
                    {
                        try { rb2d.Sleep(); } catch { }
                    }
                }
            }
            catch { }
        }

        public WaypointRegistrySO Registry
        {
            get => registry;
            set => registry = value;
        }

        private GameObject GetPlayer()
        {
            if (_cachedPlayer != null)
                return _cachedPlayer;

            try
            {
                _cachedPlayer = GameObject.FindGameObjectWithTag("Player");
            }
            catch
            {
                _cachedPlayer = null;
            }

            return _cachedPlayer;
        }
    }
}
