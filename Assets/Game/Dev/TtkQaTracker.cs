using System;
using UnityEngine;

namespace Abyss.Dev
{
    // Lightweight runtime QA helper:
    // - Counts player hits landed on the current enemy target.
    // - Starts stopwatch on first hit.
    // - Stops stopwatch on enemy death.
    // - Designed to be driven by SimplePlayerCombat (authoritative hit source).
    public sealed class TtkQaTracker : MonoBehaviour
    {
        public static TtkQaTracker Instance { get; private set; }

        private EnemyHealth _target;
        private int _hitCount;
        private float _startRealtime;
        private float _endRealtime;
        private bool _running;

        private string _cachedTierLabel;
        private string _cachedLootTableLabel;

        public int HitCount => _hitCount;
        public bool IsRunning => _running;
        public bool HasTarget => _target != null;
        public string TargetName => _target != null ? _target.name : string.Empty;
        public string TierLabel => _cachedTierLabel;
        public string LootTableLabel => _cachedLootTableLabel;
        public float ElapsedSeconds => GetElapsedSeconds();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            DetachFromTarget();
        }

        public void NotifyPlayerHit(EnemyHealth enemy)
        {
            if (enemy == null)
                return;

            if (_target == null || !ReferenceEquals(_target, enemy))
                SetTarget(enemy);

            _hitCount++;

            if (!_running)
            {
                _running = true;
                _startRealtime = Time.realtimeSinceStartup;
                _endRealtime = 0f;
            }
        }

        public string GetOverlayText()
        {
            if (_target == null)
                return "TTK: (no target)";

            string name = _target != null ? _target.name : "(null)";
            int hp = _target != null ? _target.CurrentHealth : 0;
            int max = _target != null ? _target.MaxHealth : 0;

            float elapsed = GetElapsedSeconds();
            string timeLabel = _running ? $"{elapsed:0.000}s" : $"{elapsed:0.000}s (stopped)";

            return $"TTK: {timeLabel}  Hits: {_hitCount}  HP: {hp}/{max}  Tier: {_cachedTierLabel}  Table: {_cachedLootTableLabel}  Target: {name}";
        }

        private float GetElapsedSeconds()
        {
            if (_target == null)
                return 0f;

            if (_running)
                return Mathf.Max(0f, Time.realtimeSinceStartup - _startRealtime);

            if (_endRealtime > 0f)
                return Mathf.Max(0f, _endRealtime - _startRealtime);

            return 0f;
        }

        private void SetTarget(EnemyHealth enemy)
        {
            DetachFromTarget();

            _target = enemy;
            _hitCount = 0;
            _startRealtime = 0f;
            _endRealtime = 0f;
            _running = false;

            CacheTargetLabels(enemy);

            if (_target != null)
                _target.OnDeath += OnTargetDeath;
        }

        private void DetachFromTarget()
        {
            if (_target != null)
                _target.OnDeath -= OnTargetDeath;

            _target = null;
        }

        private void OnTargetDeath(EnemyHealth dead)
        {
            if (_target == null)
                return;

            if (!ReferenceEquals(dead, _target))
                return;

            if (_running)
            {
                _running = false;
                _endRealtime = Time.realtimeSinceStartup;
            }
        }

        private void CacheTargetLabels(EnemyHealth enemy)
        {
            _cachedTierLabel = "Unknown";
            _cachedLootTableLabel = "(none)";

            if (enemy == null)
                return;

            try
            {
                var lod = enemy.GetComponentInParent<LootDropOnDeath>();
                if (lod == null)
                    lod = enemy.GetComponentInChildren<LootDropOnDeath>(true);

                if (lod != null)
                {
                    var table = lod.lootTable;
                    if (table != null)
                    {
                        _cachedLootTableLabel = table.name;
                        _cachedTierLabel = InferTierFromTableName(table.name);
                        return;
                    }

                    _cachedLootTableLabel = "(default)";
                    _cachedTierLabel = "Trash";
                    return;
                }
            }
            catch
            {
                // Intentionally silent (QA helper should never spam logs).
            }

            try
            {
                var legacy = enemy.GetComponentInParent<DropOnDeath>();
                if (legacy == null)
                    legacy = enemy.GetComponentInChildren<DropOnDeath>(true);

                if (legacy != null)
                {
                    _cachedLootTableLabel = "Legacy";
                    _cachedTierLabel = legacy.tier.ToString();
                    return;
                }
            }
            catch
            {
                // Silent.
            }
        }

        private static string InferTierFromTableName(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                return "Unknown";

            if (tableName.IndexOf("Boss", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Boss";

            if (tableName.IndexOf("Elite", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Elite";

            if (tableName.IndexOf("Trash", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Trash";

            return "Unknown";
        }
    }
}
