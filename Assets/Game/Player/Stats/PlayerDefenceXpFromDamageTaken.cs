using UnityEngine;
using Abyssbound.Combat;
using StatType = Abyssbound.Loot.StatType;

namespace Abyssbound.Stats
{
    [DisallowMultipleComponent]
    public sealed class PlayerDefenceXpFromDamageTaken : MonoBehaviour
    {
        [SerializeField] private bool debugLogs;

        private PlayerHealth _health;
        private PlayerStatsRuntime _stats;

        private float _pendingDefenceXpFloat;
        private float _lastBatchTime;

        private float _secondStart;
        private int _xpAwardedThisSecond;

        private void Awake()
        {
            try { _health = GetComponent<PlayerHealth>(); } catch { _health = null; }
            try { _stats = GetComponent<PlayerStatsRuntime>(); } catch { _stats = null; }

            _lastBatchTime = Time.time;
            _secondStart = Time.time;
        }

        private void OnEnable()
        {
            if (_health == null)
            {
                try { _health = GetComponent<PlayerHealth>(); } catch { _health = null; }
            }

            if (_health != null)
            {
                _health.DamageTakenFinal -= OnDamageTakenFinal;
                _health.DamageTakenFinal += OnDamageTakenFinal;

                _health.DamageTakenFinalFromEnemy -= OnDamageTakenFinalFromEnemy;
                _health.DamageTakenFinalFromEnemy += OnDamageTakenFinalFromEnemy;
            }
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.DamageTakenFinal -= OnDamageTakenFinal;
                _health.DamageTakenFinalFromEnemy -= OnDamageTakenFinalFromEnemy;
            }
        }

        private void Update()
        {
            if (!XpAwardFlags.AwardDefenceXpFromDamageTaken)
                return;

            float window = Mathf.Max(0.01f, CombatXpTuning.DefenceXpBatchWindowSeconds);
            if (Time.time - _lastBatchTime < window)
                return;

            _lastBatchTime = Time.time;

            if (_pendingDefenceXpFloat <= 0.0001f)
                return;

            // XP is damage-based, then multiplied by tier multiplier per hit (if source known), then floored.
            int desiredXp = Mathf.FloorToInt(_pendingDefenceXpFloat);
            _pendingDefenceXpFloat = 0f;

            int awarded = ApplyPerSecondCap(desiredXp);
            if (awarded <= 0)
                return;

            if (_stats == null)
            {
                try { _stats = GetComponent<PlayerStatsRuntime>(); } catch { _stats = null; }
            }

            if (_stats == null)
                return;

            try { _stats.AddXp(StatType.DefenseSkill, awarded); }
            catch { return; }

            // Show one green XP text per batch window.
            try { FloatingDamageTextManager.ShowSkillXpGain(transform.position, awarded, "Defence"); }
            catch { }

            if (debugLogs)
                Debug.Log($"[XP][Defence] +{awarded} DefenceXP (batched)", this);
        }

        private void OnDamageTakenFinal(int finalDamage)
        {
            if (!XpAwardFlags.AwardDefenceXpFromDamageTaken)
                return;

            if (finalDamage <= 0)
                return;

            // Fallback path when source isn't provided: treat as Trash multiplier.
            _pendingDefenceXpFloat += finalDamage * CombatXpTuning.DefenceXpPerDamage * CombatXpTuning.TrashXpMult;
        }

        private void OnDamageTakenFinalFromEnemy(int finalDamage, EnemyHealth sourceEnemy)
        {
            if (!XpAwardFlags.AwardDefenceXpFromDamageTaken)
                return;

            if (finalDamage <= 0)
                return;

            float mult = EnemyTierResolver.GetXpMultiplier(sourceEnemy);
            _pendingDefenceXpFloat += finalDamage * CombatXpTuning.DefenceXpPerDamage * mult;
        }

        private int ApplyPerSecondCap(int desired)
        {
            desired = Mathf.Max(0, desired);
            if (desired == 0)
                return 0;

            float now = Time.time;
            if (now - _secondStart >= 1.0f)
            {
                _secondStart = now;
                _xpAwardedThisSecond = 0;
            }

            int cap = Mathf.Max(0, CombatXpTuning.DefenceXpMaxPerSecond - _xpAwardedThisSecond);
            int awarded = Mathf.Min(desired, cap);
            _xpAwardedThisSecond += awarded;
            return awarded;
        }
    }
}
