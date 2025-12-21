using UnityEngine;

// Minimal MVP melee attack for enemies.
// - No AI/state machine: just range + cooldown.
// - Designed to be safe to add to spawned enemies via DevCheats.
[DisallowMultipleComponent]
public sealed class EnemyMeleeAttack : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField] private int damage = 5;
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private float cooldownSeconds = 1.0f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs;

    private float _nextAttackTime;
    private PlayerHealth _playerHealth;
    private Transform _playerTransform;

    private void OnEnable()
    {
        TryResolvePlayer();
        _nextAttackTime = Time.time + Random.Range(0f, 0.25f);
    }

    private void Update()
    {
        if (Time.time < _nextAttackTime)
            return;

        if (_playerHealth == null || _playerTransform == null)
            TryResolvePlayer();

        if (_playerHealth == null || _playerTransform == null)
            return;

        if (_playerHealth.IsDead)
            return;

        if (damage <= 0)
            return;

        float range = Mathf.Max(0.25f, attackRange);

        Vector3 delta = _playerTransform.position - transform.position;
        delta.y = 0f;

        if (delta.sqrMagnitude > range * range)
            return;

        _playerHealth.TakeDamage(damage);
        _nextAttackTime = Time.time + Mathf.Max(0.05f, cooldownSeconds);

        if (debugLogs)
            Debug.Log($"[ENEMY ATK] enemy={name} dmg={damage} range={range}", this);
    }

    private void TryResolvePlayer()
    {
#if UNITY_2022_2_OR_NEWER
        _playerHealth = Object.FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Exclude);
#else
        _playerHealth = Object.FindObjectOfType<PlayerHealth>();
#endif
        _playerTransform = _playerHealth != null ? _playerHealth.transform : null;
    }
}
