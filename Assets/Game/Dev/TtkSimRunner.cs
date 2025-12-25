using System;
using System.Collections;
using System.Collections.Generic;
using Abyssbound.Loot;
using UnityEngine;

namespace Abyss.Dev
{
    // Play Mode-only TTK simulator.
    // Spawns a dummy enemy near the player, assigns a Loot V2 table for tier labeling,
    // sets it as the SimplePlayerCombat selected target, and auto-attacks until dead.
    public sealed class TtkSimRunner : MonoBehaviour
    {
        [Header("Spawn")]
        [SerializeField] private string dummyPrefabAssetPath = "Assets/Prefabs/Enemy_Dummy/Enemy_Dummy.prefab";
        [SerializeField] private float spawnDistance = 3.0f;

        [Header("Sim")]
        [SerializeField] private int maxSecondsPerKill = 20;

        [Header("Sim HP (by tier)")]
        [SerializeField] private int trashHp = 50;
        [SerializeField] private int eliteHp = 125;
        [SerializeField] private int bossHp = 700;

        private Coroutine _routine;

        public bool IsRunning => _routine != null;

        public void StartSim(string lootTableResourcesPath, int kills)
        {
            if (!Application.isPlaying)
                return;

            if (IsRunning)
                return;

            kills = Mathf.Clamp(kills, 1, 1000);
            _routine = StartCoroutine(RunSimRoutine(lootTableResourcesPath, kills));
        }

        private IEnumerator RunSimRoutine(string lootTableResourcesPath, int kills)
        {
            var combat = FindFirstObjectByType<SimplePlayerCombat>(FindObjectsInactive.Exclude);
            if (combat == null)
            {
                Debug.LogWarning("[TTK Sim] SimplePlayerCombat not found.");
                _routine = null;
                yield break;
            }

            var tracker = TtkQaTracker.Instance;
            if (tracker == null)
                tracker = gameObject.AddComponent<TtkQaTracker>();

            LootTableSO table = null;
            try { table = Resources.Load<LootTableSO>(lootTableResourcesPath); } catch { table = null; }

            var times = new List<float>(kills);
            var hits = new List<int>(kills);

            for (int i = 0; i < kills; i++)
            {
                var enemy = SpawnDummyWithLootV2(combat.transform, combat.Range, table);
                if (enemy == null)
                    break;

                // Let OnEnable hooks settle (health reset, pooling restores, etc.)
                yield return null;

                combat.SetSelectedTarget(enemy);

                float start = Time.realtimeSinceStartup;
                float timeoutAt = start + Mathf.Max(1f, maxSecondsPerKill);

                // Drive attacks until death or timeout.
                while (!enemy.IsDead && Time.realtimeSinceStartup < timeoutAt)
                {
                    KeepEnemyInRange(enemy, combat.transform, combat.Range);
                    combat.TryAttack();
                    yield return null;
                }

                // Give one frame for death events to settle.
                yield return null;

                float elapsed = tracker.ElapsedSeconds;
                int hitCount = tracker.HitCount;

                // If we timed out (enemy didn't die), record as maxSecondsPerKill.
                if (!enemy.IsDead)
                {
                    elapsed = Mathf.Max(1f, maxSecondsPerKill);
                }

                times.Add(elapsed);
                hits.Add(hitCount);

                // Clean up the spawned enemy (pool-friendly).
                try { enemy.gameObject.SetActive(false); } catch { }

                // Clear selection so the next run starts clean.
                combat.SetSelectedTarget(null);

                // Small spacing to avoid overlapping physics / selection confusion.
                yield return null;
            }

            EmitSummary(lootTableResourcesPath, times, hits);
            _routine = null;
        }

        private EnemyHealth SpawnDummyWithLootV2(Transform player, float playerRange, LootTableSO table)
        {
            if (player == null)
                return null;

            GameObject prefab = null;
            try
            {
#if UNITY_EDITOR
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(dummyPrefabAssetPath);
#endif
            }
            catch { prefab = null; }

            if (prefab == null)
            {
                // Fallback: try Resources by common name.
                try { prefab = Resources.Load<GameObject>("Enemy_Dummy"); } catch { prefab = null; }
            }

            if (prefab == null)
            {
                Debug.LogWarning("[TTK Sim] Dummy enemy prefab not found.");
                return null;
            }

            float desiredDist = Mathf.Clamp(playerRange * 0.75f, 0.75f, Mathf.Max(0.75f, spawnDistance));
            Vector3 pos = player.position + player.forward.normalized * desiredDist;
            var go = Instantiate(prefab, pos, Quaternion.identity);
            go.name = "QA_TTK_Target";

            // Prevent loot side effects during TTK sim (keeps console quiet and results consistent).
            try
            {
                var legacyAll = go.GetComponentsInChildren<DropOnDeath>(true);
                if (legacyAll != null)
                {
                    for (int i = 0; i < legacyAll.Length; i++)
                    {
                        if (legacyAll[i] != null)
                            legacyAll[i].enabled = false;
                    }
                }
            }
            catch { }

            try
            {
                var lod = go.GetComponentInChildren<LootDropOnDeath>(true);
                if (lod == null)
                    lod = go.AddComponent<LootDropOnDeath>();

                lod.lootTable = table;
                lod.enabled = false;
            }
            catch { }

            var eh = go.GetComponentInChildren<EnemyHealth>(true);
            try
            {
                if (eh != null)
                {
                    int hp = trashHp;
                    string tn = table != null ? table.name : string.Empty;
                    if (!string.IsNullOrWhiteSpace(tn) && tn.IndexOf("Elite", StringComparison.OrdinalIgnoreCase) >= 0) hp = eliteHp;
                    else if (!string.IsNullOrWhiteSpace(tn) && tn.IndexOf("Boss", StringComparison.OrdinalIgnoreCase) >= 0) hp = bossHp;
                    eh.SetMaxHealthForQa(hp);
                }
            }
            catch { }
            return eh;
        }

        private static void KeepEnemyInRange(EnemyHealth enemy, Transform player, float range)
        {
            if (enemy == null || player == null)
                return;

            // Match SimplePlayerCombat's XZ-only distance logic.
            Vector3 ePos = enemy.transform.position;
            Vector3 pPos = player.position;
            float dx = ePos.x - pPos.x;
            float dz = ePos.z - pPos.z;
            float distSq = (dx * dx) + (dz * dz);
            float r = Mathf.Max(0.25f, range * 0.75f);
            float rSq = r * r;

            if (distSq <= rSq)
                return;

            Vector3 forward = player.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;

            Vector3 desired = pPos + forward.normalized * r;
            desired.y = ePos.y;
            enemy.transform.position = desired;

            try
            {
                var rb = enemy.GetComponentInChildren<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
            catch { }
        }

        private static void EmitSummary(string lootTableResourcesPath, List<float> times, List<int> hits)
        {
            int n = times != null ? times.Count : 0;
            if (n <= 0)
            {
                Debug.Log("[TTK Sim] No results.");
                return;
            }

            float sumT = 0f;
            float minT = float.MaxValue;
            float maxT = 0f;

            int sumH = 0;
            int minH = int.MaxValue;
            int maxH = 0;

            for (int i = 0; i < n; i++)
            {
                float t = times[i];
                int h = hits[i];

                sumT += t;
                if (t < minT) minT = t;
                if (t > maxT) maxT = t;

                sumH += h;
                if (h < minH) minH = h;
                if (h > maxH) maxH = h;
            }

            float avgT = sumT / Mathf.Max(1, n);
            float avgH = (float)sumH / Mathf.Max(1, n);

            Debug.Log($"[TTK Sim] {lootTableResourcesPath}  kills={n}  time avg={avgT:0.000}s min={minT:0.000}s max={maxT:0.000}s  hits avg={avgH:0.0} min={minH} max={maxH}");
        }
    }
}
