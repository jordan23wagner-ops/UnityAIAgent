using UnityEngine;

[DisallowMultipleComponent]
public class CombatLoopController : MonoBehaviour
{
    [Header("Combat")]
    [SerializeField] private float engageStopDistance = 1.75f;
    [SerializeField] private float recheckRate = 0.05f;

    private PlayerMovementMotor _motor;
    private SimplePlayerCombat _combat;

    private EnemyHealth _target;
    private EnemyHealth _subscribedTarget;
    private float _nextCheckTime;
    private bool _loggedAttackState;

    private void Awake()
    {
        _motor = GetComponent<PlayerMovementMotor>();
        _combat = GetComponent<SimplePlayerCombat>();

        if (_motor == null) Debug.LogError("[CombatLoop] Missing PlayerMovementMotor.", this);
        if (_combat == null) Debug.LogError("[CombatLoop] Missing SimplePlayerCombat.", this);
    }

    public void SetTarget(EnemyHealth enemy, float stopDistance)
    {
        UnsubscribeFromTargetDeath();

        _target = enemy;
        _loggedAttackState = false;

        SubscribeToTargetDeath();

        if (stopDistance > 0f)
            engageStopDistance = stopDistance;
        else if (_combat != null)
            engageStopDistance = _combat.Range;

        if (_target != null && _motor != null)
            _motor.SetFollowTarget(_target.transform, engageStopDistance);
    }

    public void ClearTarget()
    {
        UnsubscribeFromTargetDeath();

        _target = null;
        _loggedAttackState = false;

        if (_combat != null)
            _combat.SelectedTarget = null;

        if (_motor != null)
            _motor.Clear();
    }

    private void SubscribeToTargetDeath()
    {
        if (_target == null)
            return;

        _subscribedTarget = _target;

        // Prefer new API, but also hook legacy event for safety.
        _subscribedTarget.OnDeath += OnTargetDeath;
        _subscribedTarget.Died += OnTargetDiedLegacy;
    }

    private void UnsubscribeFromTargetDeath()
    {
        if (_subscribedTarget == null)
            return;

        _subscribedTarget.OnDeath -= OnTargetDeath;
        _subscribedTarget.Died -= OnTargetDiedLegacy;
        _subscribedTarget = null;
    }

    private void OnTargetDeath(EnemyHealth dead)
    {
        if (_target == null)
            return;

        if (dead != _target)
            return;

        ClearTarget();
    }

    private void OnTargetDiedLegacy()
    {
        // If legacy-only enemies still exist, clear on any Died callback.
        if (_target != null && _target.IsDead)
            ClearTarget();
    }

    private void Update()
    {
        // Target destroyed or missing
        if (_target == null)
            return;

        // Authoritative death check
        if (_target.IsDead)
        {
            ClearTarget();
            return;
        }

        // throttle
        if (Time.time < _nextCheckTime) return;
        _nextCheckTime = Time.time + recheckRate;

        Vector3 myPos = transform.position;
        Vector3 targetPos = _target.transform.position;

        // XZ plane only (ignore Y)
        float dx = targetPos.x - myPos.x;
        float dz = targetPos.z - myPos.z;
        float distSq = (dx * dx) + (dz * dz);
        float stopSq = engageStopDistance * engageStopDistance;

        if (distSq > stopSq)
        {
            _loggedAttackState = false;
            if (_motor != null)
                _motor.SetFollowTarget(_target.transform, engageStopDistance);
            return;
        }

        // In range: stop following and attack
        if (_motor != null)
            _motor.Clear();

        if (_combat != null)
        {
            _combat.SelectedTarget = _target;

            if (!_loggedAttackState)
            {
                Debug.Log($"[CombatLoop] In range, attacking {_target.name}", this);
                _loggedAttackState = true;
            }

            _combat.TryAttack();
        }
    }
}
