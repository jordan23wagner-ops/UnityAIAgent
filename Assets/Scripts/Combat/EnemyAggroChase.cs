using UnityEngine;

// Robust aggro + chase behavior.
// - Supports both 2D (Rigidbody2D) and 3D (Rigidbody) physics.
// - Does NOT implement damage; it only moves into range so existing attack scripts can fire.
[DisallowMultipleComponent]
public sealed class EnemyAggroChase : MonoBehaviour
{
    private enum AggroState
    {
        Idle,
        Aggro,
    }

    [Header("Aggro")]
    [SerializeField] private float aggroRadius = 6f;
    [SerializeField] private float leashRadius = 12f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float stopDistance = 1.6f;

    [Header("Target")]
    [SerializeField] private float reacquireInterval = 0.5f;
    [SerializeField] private string playerTag = "Player";

    [Header("Debug")]
    [SerializeField] private bool debugStateChanges = true;

    private AggroState _state;

    private Transform _player;
    private bool _hadPlayer;

    private Rigidbody2D _rb2d;
    private Rigidbody _rb;

    private float _nextReacquireTime;

    private float _warnNoPlayerAtTime;
    private bool _didWarnNoPlayer;

    private void Awake()
    {
        _rb2d = GetComponent<Rigidbody2D>();
        _rb = _rb2d == null ? GetComponent<Rigidbody>() : null;
    }

    private void OnEnable()
    {
        _state = AggroState.Idle;

        _nextReacquireTime = 0f;
        _warnNoPlayerAtTime = Time.time + 2f;
        _didWarnNoPlayer = false;

        TryAcquirePlayer(now: true);
    }

    private void Update()
    {
        // Target acquisition (no per-frame Find)
        if (_player == null)
        {
            TryAcquirePlayer(now: false);

            if (!_didWarnNoPlayer && Time.time >= _warnNoPlayerAtTime)
            {
                _didWarnNoPlayer = true;
                if (_player == null)
                    Debug.LogWarning("EnemyAggroChase: No GameObject tagged 'Player' found.", this);
            }
        }

        // Detect player lost/acquired for state-change-only logging.
        if (_player != null)
        {
            if (!_hadPlayer)
            {
                _hadPlayer = true;
                if (debugStateChanges)
                    Debug.Log("[EnemyAggroChase] Player acquired.", this);
            }
        }
        else
        {
            if (_hadPlayer)
            {
                _hadPlayer = false;
                if (debugStateChanges)
                    Debug.Log("[EnemyAggroChase] Player lost.", this);
            }
        }

        TickStateMachine();

        // Transform-based movement happens in Update.
        if (_state == AggroState.Aggro)
        {
            if (_rb2d == null && _rb == null)
                TickMoveTransform(Time.deltaTime);
        }
    }

    private void FixedUpdate()
    {
        if (_state != AggroState.Aggro)
            return;

        // Rigidbody movement in FixedUpdate to play nicer with physics.
        if (_rb2d != null)
            TickMoveRigidbody2D();
        else if (_rb != null)
            TickMoveRigidbody3D();
    }

    private void TickStateMachine()
    {
        if (_state == AggroState.Idle)
        {
            if (_player == null)
                return;

            if (PlanarDistanceSqToPlayer() <= Sqr(Mathf.Max(0.01f, aggroRadius)))
                SetState(AggroState.Aggro);

            return;
        }

        // Aggro
        if (_player == null)
        {
            SetState(AggroState.Idle);
            return;
        }

        if (PlanarDistanceSqToPlayer() > Sqr(Mathf.Max(0.01f, leashRadius)))
        {
            SetState(AggroState.Idle);
            return;
        }

        // Stop when in range (so existing attack scripts can fire).
        if (PlanarDistanceSqToPlayer() <= Sqr(Mathf.Max(0.01f, stopDistance)))
            StopMoving();
    }

    private void SetState(AggroState newState)
    {
        if (_state == newState)
            return;

        _state = newState;

        if (debugStateChanges)
            Debug.Log(newState == AggroState.Aggro ? "[EnemyAggroChase] State -> Aggro" : "[EnemyAggroChase] State -> Idle", this);

        if (_state == AggroState.Idle)
            StopMoving();
    }

    private void TickMoveRigidbody2D()
    {
        if (_player == null || _rb2d == null)
            return;

        float stop = Mathf.Max(0.01f, stopDistance);
        if (PlanarDistanceSqToPlayer() <= stop * stop)
        {
            StopMoving();
            return;
        }

        Vector2 dir = GetPlanarDirection2D(transform.position, _player.position);
        float magSq = dir.sqrMagnitude;
        if (magSq > 0.001f * 0.001f)
            dir /= Mathf.Sqrt(magSq);
        else
            dir = Vector2.zero;

        Vector2 desired = dir * Mathf.Max(0f, moveSpeed);

#if UNITY_6000_0_OR_NEWER
        _rb2d.linearVelocity = desired;
#else
        _rb2d.velocity = desired;
#endif
    }

    private void TickMoveRigidbody3D()
    {
        if (_player == null || _rb == null)
            return;

        float stop = Mathf.Max(0.01f, stopDistance);
        if (PlanarDistanceSqToPlayer() <= stop * stop)
        {
            StopMoving();
            return;
        }

        Vector3 dir = GetPlanarDirection3D(transform.position, _player.position);
        float magSq = dir.sqrMagnitude;
        if (magSq > 0.001f * 0.001f)
            dir /= Mathf.Sqrt(magSq);
        else
            dir = Vector3.zero;

        Vector3 desired = dir * Mathf.Max(0f, moveSpeed);

#if UNITY_6000_0_OR_NEWER
        _rb.linearVelocity = desired;
#else
        _rb.velocity = desired;
#endif
    }

    private void TickMoveTransform(float dt)
    {
        if (_player == null)
            return;

        float stop = Mathf.Max(0.01f, stopDistance);
        if (PlanarDistanceSqToPlayer() <= stop * stop)
            return;

        float step = Mathf.Max(0f, moveSpeed) * Mathf.Max(0f, dt);
        Vector3 current = transform.position;

        if (Is2D())
        {
            Vector3 target = _player.position;
            target.z = current.z;

            Vector3 next = Vector3.MoveTowards(current, target, step);
            next.z = current.z;
            transform.position = next;
        }
        else
        {
            Vector3 target = _player.position;
            target.y = current.y;

            Vector3 next = Vector3.MoveTowards(current, target, step);
            next.y = current.y;
            transform.position = next;
        }
    }

    private void StopMoving()
    {
        if (_rb2d != null)
        {
#if UNITY_6000_0_OR_NEWER
            _rb2d.linearVelocity = Vector2.zero;
#else
            _rb2d.velocity = Vector2.zero;
#endif
        }

        if (_rb != null)
        {
#if UNITY_6000_0_OR_NEWER
            _rb.linearVelocity = Vector3.zero;
#else
            _rb.velocity = Vector3.zero;
#endif
        }
    }

    private void TryAcquirePlayer(bool now)
    {
        if (string.IsNullOrEmpty(playerTag))
            return;

        if (!now)
        {
            if (Time.time < _nextReacquireTime)
                return;

            _nextReacquireTime = Time.time + Mathf.Max(0.05f, reacquireInterval);
        }

        GameObject go = null;
        try
        {
            go = GameObject.FindGameObjectWithTag(playerTag);
        }
        catch
        {
            go = null;
        }

        _player = go != null ? go.transform : null;
    }

    private bool Is2D() => _rb2d != null;

    private float PlanarDistanceSqToPlayer()
    {
        if (_player == null)
            return float.PositiveInfinity;

        Vector3 a = transform.position;
        Vector3 b = _player.position;

        if (Is2D())
        {
            float dx = b.x - a.x;
            float dy = b.y - a.y;
            return (dx * dx) + (dy * dy);
        }
        else
        {
            float dx = b.x - a.x;
            float dz = b.z - a.z;
            return (dx * dx) + (dz * dz);
        }
    }

    private static Vector2 GetPlanarDirection2D(Vector3 from, Vector3 to)
    {
        return new Vector2(to.x - from.x, to.y - from.y);
    }

    private static Vector3 GetPlanarDirection3D(Vector3 from, Vector3 to)
    {
        return new Vector3(to.x - from.x, 0f, to.z - from.z);
    }

    private static float Sqr(float v) => v * v;

    // Retaliation hook: can be called by damage receivers.
    public void ForceAggro(Transform t)
    {
        if (t != null)
            _player = t;

        SetState(AggroState.Aggro);
    }

    // Optional: used by spawn helpers to apply tuning without poking private fields.
    public void SetTuning(float newAggroRadius, float newLeashRadius, float newMoveSpeed, float newStopDistance)
    {
        aggroRadius = Mathf.Max(0.01f, newAggroRadius);
        leashRadius = Mathf.Max(aggroRadius, newLeashRadius);
        moveSpeed = Mathf.Max(0f, newMoveSpeed);
        stopDistance = Mathf.Max(0.01f, newStopDistance);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.65f, 0f, 1f); // aggro
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.01f, aggroRadius));

        Gizmos.color = Color.red; // stop
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.01f, stopDistance));

        Gizmos.color = Color.cyan; // leash
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.01f, leashRadius));
    }
#endif
}
