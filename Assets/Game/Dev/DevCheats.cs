using System.Collections.Generic;
using Abyss.Loot;
using Game.Input;
using UnityEngine;

namespace Abyss.Dev
{
    // Editor/Dev-only runtime cheats for fast QA.
    public sealed class DevCheats : MonoBehaviour
    {
        public static DevCheats Instance { get; private set; }

        public static bool GodModeEnabled => Instance != null && Instance.godMode;

        [Header("Toggles")]
        [SerializeField] private bool godMode;
        [SerializeField] private bool showOverlay = true;

        [Header("Hotkeys")]
        public KeyCode toggleGodModeKey = KeyCode.F1;
        public KeyCode spawnEnemyKey = KeyCode.F2;
        public KeyCode killSpawnedKey = KeyCode.F3;
        public KeyCode selfDamageKey = KeyCode.F4;

        [Header("Spawning")]
        public List<GameObject> enemyPrefabs = new();
        public EnemyTier spawnTier = EnemyTier.Normal;
        public ZoneLootTable overrideZoneLootTable;
        public float spawnDistance = 4f;

        [Tooltip("Default enemy spawn count for F2. Clamped to 1..2 for MVP testing.")]
        public int spawnCount = 2;

        [Tooltip("If true, hold Shift while pressing F2 to spawn up to 50 enemies (useful for stress testing).")]
        public bool holdShiftForMassSpawn = true;

        [Tooltip("Spawn count when Shift-mass-spawn is enabled.")]
        public int massSpawnCount = 50;

        private readonly List<GameObject> _spawned = new();
        private int _lastSpawnedCount;

        private PlayerInputAuthority _input;

        private static Texture2D s_BgTex;
        private static GUIStyle s_LabelStyle;
        private static GUIStyle s_BoxStyle;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

#if UNITY_2022_2_OR_NEWER
            _input = FindFirstObjectByType<PlayerInputAuthority>();
#else
            _input = FindObjectOfType<PlayerInputAuthority>();
#endif
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            return;
#else
            if (Input.GetKeyDown(toggleGodModeKey))
            {
                godMode = !godMode;
                Debug.Log($"[DevCheats] GodMode={(godMode ? "ON" : "OFF")}");
            }

            if (Input.GetKeyDown(spawnEnemyKey))
                SpawnEnemies();

            if (Input.GetKeyDown(killSpawnedKey))
                KillSpawned();

            if (Input.GetKeyDown(selfDamageKey))
                SelfDamage(10);
#endif
        }

        private void SpawnEnemies()
        {
            if (enemyPrefabs == null || enemyPrefabs.Count == 0)
            {
                Debug.LogWarning("[DevCheats] No enemyPrefabs configured.");
                return;
            }

            var prefab = enemyPrefabs[0];
            if (prefab == null)
            {
                Debug.LogWarning("[DevCheats] enemyPrefabs[0] is null.");
                return;
            }

            Transform anchor = FindAnchor();
            Vector3 basePos = anchor != null ? anchor.position : Vector3.zero;
            Vector3 forward = anchor != null ? anchor.forward : Vector3.forward;

            int count = Mathf.Clamp(spawnCount, 1, 2);

            if (holdShiftForMassSpawn && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
                count = Mathf.Clamp(massSpawnCount, 1, 50);

            for (int i = 0; i < count; i++)
            {
                Vector3 jitter = new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1.5f, 1.5f));
                Vector3 pos = basePos + forward.normalized * Mathf.Max(0.5f, spawnDistance) + jitter;
                var go = Instantiate(prefab, pos, Quaternion.identity);
                go.name = prefab.name;

                ApplyLootOverrides(go);
                EnsureEnemyMeleeAttack(go);
                EnsureEnemyAggroChase(go);
                _spawned.Add(go);
            }

            _lastSpawnedCount = count;

            Debug.Log($"[DevCheats] Spawned {count}x '{prefab.name}'.");
        }

        private static void EnsureEnemyMeleeAttack(GameObject enemy)
        {
            if (enemy == null) return;

            // Try the root first, then children; if missing, add to root.
            var atk = enemy.GetComponent<EnemyMeleeAttack>();
            if (atk != null) return;

            atk = enemy.GetComponentInChildren<EnemyMeleeAttack>(true);
            if (atk != null) return;

            enemy.AddComponent<EnemyMeleeAttack>();
        }

        private void EnsureEnemyAggroChase(GameObject enemy)
        {
            if (enemy == null) return;

            // Prefer the optional helper if the user has added it to DevCheats or the spawner.
            var helper = GetComponent<EnsureEnemyAggroChaseOnSpawn>();
            if (helper != null)
            {
                helper.Ensure(enemy);
                return;
            }

            // Otherwise, ensure directly (no tuning changes).
            if (enemy.GetComponent<EnemyAggroChase>() == null)
                enemy.AddComponent<EnemyAggroChase>();
        }

        private void SelfDamage(int amount)
        {
            if (amount <= 0) return;

            var playerHealth = FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Exclude);
            if (playerHealth == null)
            {
                Debug.LogWarning("[DEV] SelfDamage: PlayerHealth not found.");
                return;
            }

            playerHealth.TakeDamage(amount);
            Debug.Log($"[DEV] SelfDamage {amount}");
        }

        private void ApplyLootOverrides(GameObject enemy)
        {
            if (enemy == null) return;

            var drop = enemy.GetComponentInChildren<DropOnDeath>();
            if (drop != null)
            {
                drop.tier = spawnTier;
                if (overrideZoneLootTable != null)
                    drop.zoneLootTable = overrideZoneLootTable;
            }
        }

        private void KillSpawned()
        {
            int killed = 0;
            for (int i = _spawned.Count - 1; i >= 0; i--)
            {
                var go = _spawned[i];
                if (go == null)
                {
                    _spawned.RemoveAt(i);
                    continue;
                }

                var enemyHealth = go.GetComponentInChildren<EnemyHealth>();
                if (enemyHealth != null)
                {
                    enemyHealth.TakeDamage(999999);
                    killed++;
                }
            }

            Debug.Log($"[DevCheats] KillSpawned: {killed} enemy(ies) signaled lethal damage.");
        }

        private void OnGUI()
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            return;
#else
            if (!showOverlay) return;

            // Avoid drawing debug overlays through/over gameplay UI.
            if (_input == null)
            {
    #if UNITY_2022_2_OR_NEWER
            _input = FindFirstObjectByType<PlayerInputAuthority>();
    #else
            _input = FindObjectOfType<PlayerInputAuthority>();
    #endif
            }
            if (_input != null && _input.IsUiInputLocked)
            return;

            EnsureGuiStyles();

            const float pad = 10f;
            var rect = new Rect(pad, pad, 520f, 80f);

            string text =
                $"DevCheats  |  GodMode: {(godMode ? "ON" : "OFF")}\n" +
                $"LastSpawn: {_lastSpawnedCount}  ActiveSpawned: {_spawned.Count}\n" +
                $"Keys: {toggleGodModeKey}=GodMode  {spawnEnemyKey}=Spawn  {killSpawnedKey}=Kill  {selfDamageKey}=SelfDamage";

            // Measure height so the background fits the content.
            float h = s_LabelStyle != null ? s_LabelStyle.CalcHeight(new GUIContent(text), rect.width) : rect.height;
            var bgRect = new Rect(rect.x, rect.y, rect.width, Mathf.Max(rect.height, h) + 8f);

            GUI.Box(bgRect, GUIContent.none, s_BoxStyle);
            GUI.Label(new Rect(rect.x + 6f, rect.y + 4f, rect.width - 12f, bgRect.height - 8f), text, s_LabelStyle);
#endif
        }

        private static void EnsureGuiStyles()
        {
            if (s_BgTex == null)
            {
                s_BgTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                s_BgTex.hideFlags = HideFlags.HideAndDontSave;
                s_BgTex.SetPixel(0, 0, new Color32(0, 0, 0, 160));
                s_BgTex.Apply();
            }

            if (s_BoxStyle == null)
            {
                s_BoxStyle = new GUIStyle(GUI.skin.box);
                s_BoxStyle.normal.background = s_BgTex;
                s_BoxStyle.border = new RectOffset(0, 0, 0, 0);
                s_BoxStyle.margin = new RectOffset(0, 0, 0, 0);
                s_BoxStyle.padding = new RectOffset(0, 0, 0, 0);
            }

            if (s_LabelStyle == null)
            {
                s_LabelStyle = new GUIStyle(GUI.skin.label);
                s_LabelStyle.normal.textColor = Color.white;
                s_LabelStyle.fontSize = 14;
                s_LabelStyle.richText = false;
                s_LabelStyle.wordWrap = true;
            }
        }

        private static Transform FindAnchor()
        {
            // Prefer PlayerHealth (player root), then camera.
            var playerHealth = FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Exclude);
            if (playerHealth != null) return playerHealth.transform;

            if (Camera.main != null) return Camera.main.transform;

            return null;
        }
    }
}
