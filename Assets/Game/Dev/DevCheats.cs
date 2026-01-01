using System;
using System.Collections.Generic;
using Abyss.Loot;
using Abyssbound.Combat.Tiering;
using Abyssbound.BagUpgrades;
using Abyssbound.Loot;
using Game.Input;
using Game.Systems;
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
        [Tooltip("Toggles the on-screen DevCheats overlay (IMGUI).")]
        public KeyCode toggleOverlayKey = KeyCode.BackQuote;
        public KeyCode toggleGodModeKey = KeyCode.F1;
        public KeyCode spawnEnemyKey = KeyCode.F2;
        public KeyCode killSpawnedKey = KeyCode.F3;
        public KeyCode selfDamageKey = KeyCode.F4;

        [Header("Spawn (Tier Hotkeys)")]
        public KeyCode spawnTrashKey = KeyCode.F8;
        public KeyCode spawnEliteKey = KeyCode.F9;
        public KeyCode spawnBossKey = KeyCode.F10;

        [Header("Spawn (Items)")]
        public KeyCode spawnBagUpgradeT1Key = KeyCode.F11;

        [Header("Spawn (Tier HP)")]
        [Min(1)] public int qaTrashHp = 42;
        [Min(1)] public int qaEliteHp = 166;
        [Min(1)] public int qaBossHp = 1010;

        [Header("Tiering Injection")]
        [SerializeField] private DistanceTierService tierService;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private bool logTierInjection;

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

            // Ensure the lightweight TTK tracker exists for QA.
            if (GetComponent<TtkQaTracker>() == null)
                gameObject.AddComponent<TtkQaTracker>();

            // Unity warns if DontDestroyOnLoad is called on a non-root object.
            // DevCheats is safe to detach; it is a standalone QA helper.
            if (transform.parent != null)
                transform.SetParent(null, worldPositionStays: true);

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
            if (Input.GetKeyDown(toggleOverlayKey))
            {
                showOverlay = !showOverlay;
                Debug.Log($"[DevCheats] Overlay={(showOverlay ? "ON" : "OFF")}");
            }

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

            if (Input.GetKeyDown(spawnTrashKey))
                SpawnEnemyWithLootV2("Loot/Tables/Zone1_Trash", "QA_Trash");

            if (Input.GetKeyDown(spawnEliteKey))
                SpawnEnemyWithLootV2("Loot/Tables/Zone1_Elite", "QA_Elite");

            if (Input.GetKeyDown(spawnBossKey))
                SpawnEnemyWithLootV2("Loot/Tables/Zone1_Boss", "QA_Boss");

            if (Input.GetKeyDown(spawnBagUpgradeT1Key))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

                int tier;
                if (ctrl && shift) tier = 5;
                else if (ctrl) tier = 3;
                else if (alt) tier = 4;
                else if (shift) tier = 2;
                else tier = 1;

                GiveBagUpgradeTier(tier);
            }
#endif
        }

        private void GiveBagUpgradeTier(int tier)
        {
            const int qty = 1;

            tier = Mathf.Clamp(tier, 1, 5);
            string id = BagUpgradeIds.GetIdForTier(tier);
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogError("[DevCheat] Missing item id for bag upgrade.");
                return;
            }

            var reg = LootRegistryRuntime.GetOrCreate();
            if (reg == null || !reg.TryGetItem(id, out var baseItem) || baseItem == null)
            {
                Debug.LogError($"[DevCheat] Missing item definition for '{id}'. Run Tools/Bag Upgrades/Setup Bag Upgrades v1 (One-Click).", this);
                return;
            }

            var inv = PlayerInventoryResolver.GetOrFind();
            if (inv == null)
            {
                Debug.LogError("[DevCheat] No PlayerInventory found.");
                return;
            }

            int before = 0;
            try { before = inv.Count(id); } catch { before = 0; }

            try { inv.Add(id, Mathf.Max(1, qty)); }
            catch
            {
                Debug.LogError($"[DevCheat] Failed to add Bag Upgrade T{tier} (exception).", this);
                return;
            }

            int after = before;
            try { after = inv.Count(id); } catch { after = before; }

            if (after > before)
                Debug.Log($"[DevCheat] Spawned Bag Upgrade T{tier}");
            else
                Debug.LogError($"[DevCheat] Failed to spawn Bag Upgrade T{tier} (inventory full?)", this);
        }

        private void SpawnEnemyWithLootV2(string lootTableResourcesPath, string namePrefix)
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

            LootTableSO table = null;
            try { table = Resources.Load<LootTableSO>(lootTableResourcesPath); } catch { table = null; }

            Transform anchor = FindAnchor();
            Vector3 basePos = anchor != null ? anchor.position : Vector3.zero;
            Vector3 forward = anchor != null ? anchor.forward : Vector3.forward;
            Vector3 pos = basePos + forward.normalized * Mathf.Max(0.5f, spawnDistance);

            var go = Instantiate(prefab, pos, Quaternion.identity);
            go.name = namePrefix;

            InjectEnemyTiering(go);

            // Force Loot V2 table for QA and prevent double-drops.
            try
            {
                var legacy = go.GetComponentInChildren<DropOnDeath>();
                if (legacy != null) legacy.enabled = false;
            }
            catch { }

            try
            {
                var lod = go.GetComponentInChildren<LootDropOnDeath>();
                if (lod == null)
                    lod = go.AddComponent<LootDropOnDeath>();

                lod.lootTable = table;
            }
            catch { }

            try
            {
                var eh = go.GetComponentInChildren<EnemyHealth>(true);
                if (eh != null)
                {
                    int hp = qaTrashHp;
                    if (lootTableResourcesPath.IndexOf("Elite", StringComparison.OrdinalIgnoreCase) >= 0) hp = qaEliteHp;
                    else if (lootTableResourcesPath.IndexOf("Boss", StringComparison.OrdinalIgnoreCase) >= 0) hp = qaBossHp;
                    eh.SetMaxHealthForQa(hp);
                }
            }
            catch { }

            EnsureEnemyMeleeAttack(go);
            EnsureEnemyAggroChase(go);
            _spawned.Add(go);
            _lastSpawnedCount = 1;
        }

        public void SpawnDamageTestEnemy()
        {
            // Uses the same spawn path as the existing tier hotkeys.
            SpawnEnemyWithLootV2("Loot/Tables/Zone1_Trash", "QA_DamageTest");
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
                Vector3 jitter = new Vector3(UnityEngine.Random.Range(-1.5f, 1.5f), 0f, UnityEngine.Random.Range(-1.5f, 1.5f));
                Vector3 pos = basePos + forward.normalized * Mathf.Max(0.5f, spawnDistance) + jitter;
                var go = Instantiate(prefab, pos, Quaternion.identity);
                go.name = prefab.name;

                InjectEnemyTiering(go);

                ApplyLootOverrides(go);
                EnsureEnemyMeleeAttack(go);
                EnsureEnemyAggroChase(go);
                _spawned.Add(go);
            }

            _lastSpawnedCount = count;

            Debug.Log($"[DevCheats] Spawned {count}x '{prefab.name}'.");
        }

        private void InjectEnemyTiering(GameObject spawnedEnemy)
        {
            if (spawnedEnemy == null)
                return;

            var svc = ResolveTierServiceForInjection();
            var player = ResolvePlayerTransformForInjection();

            var applier = spawnedEnemy.GetComponent<EnemyTierApplier>();
            bool added = false;
            if (applier == null)
            {
                applier = spawnedEnemy.AddComponent<EnemyTierApplier>();
                added = true;
            }

            // EnemyTierApplier fields are intentionally serialized/private for prefab safety;
            // inject references via reflection without modifying the applier script.
            TrySetApplierField(applier, "tierService", svc);
            TrySetApplierField(applier, "playerTransform", player);

            if (logTierInjection)
            {
                Debug.Log($"[DevCheats] TierInjection '{spawnedEnemy.name}': tierService={(svc != null)}, playerTransform={(player != null)}, applier={(added ? "added" : "reused")}", spawnedEnemy);
            }

            // Also attach loot context so loot-on-death can read tier without recalculating distance.
            try
            {
                if (spawnedEnemy.GetComponent<EnemyLootContext>() == null)
                    spawnedEnemy.AddComponent<EnemyLootContext>();
            }
            catch { }
        }

        private DistanceTierService ResolveTierServiceForInjection()
        {
            if (tierService != null)
                return tierService;

            GameObject go = GameObject.Find("DistanceTierService");
            if (go == null)
                go = GameObject.Find("Systems_DistanceTiering");

            if (go != null)
                tierService = go.GetComponent<DistanceTierService>();

            return tierService;
        }

        private Transform ResolvePlayerTransformForInjection()
        {
            if (playerTransform != null)
                return playerTransform;

            if (_input != null)
                playerTransform = _input.transform;

            return playerTransform;
        }

        private static void TrySetApplierField(EnemyTierApplier applier, string fieldName, object value)
        {
            if (applier == null || string.IsNullOrEmpty(fieldName))
                return;

            try
            {
                const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
                var f = typeof(EnemyTierApplier).GetField(fieldName, flags);
                if (f == null)
                    return;

                f.SetValue(applier, value);
            }
            catch { }
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

            string ttk = "TTK: (tracker missing)";
            try
            {
                if (TtkQaTracker.Instance != null)
                    ttk = TtkQaTracker.Instance.GetOverlayText();
            }
            catch { }

            string text =
                $"DevCheats  |  GodMode: {(godMode ? "ON" : "OFF")}\n" +
                $"LastSpawn: {_lastSpawnedCount}  ActiveSpawned: {_spawned.Count}\n" +
                $"Keys: {toggleOverlayKey}=Overlay  {toggleGodModeKey}=GodMode  {spawnEnemyKey}=Spawn  {killSpawnedKey}=Kill  {selfDamageKey}=SelfDamage  {spawnTrashKey}=Trash  {spawnEliteKey}=Elite  {spawnBossKey}=Boss\n" +
                ttk;

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
