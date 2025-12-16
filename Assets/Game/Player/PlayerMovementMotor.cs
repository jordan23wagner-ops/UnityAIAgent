using UnityEngine;

[DisallowMultipleComponent]
public class PlayerMovementMotor : MonoBehaviour
{
    [Header("Tuning")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float defaultStopDistance = 0.15f;

    private Vector3? _destination;
    private Transform _followTarget;

    private float _activeStopDistance;

    private Rigidbody _rb;

    private void Awake()
    {
        _activeStopDistance = defaultStopDistance;
        _rb = GetComponent<Rigidbody>();

        // If a Rigidbody exists and is not kinematic, we must move via MovePosition in FixedUpdate.
        // Otherwise physics will fight transform changes and you'll get tiny nudges.
        if (_rb != null && !_rb.isKinematic)
        {
            // optional: keep constraints you already set in Inspector
            Debug.Log("[Motor] Rigidbody detected; using FixedUpdate + MovePosition.", this);
        }
    }

    public void SetDestination(Vector3 worldPos)
    {
        _followTarget = null;
        _activeStopDistance = defaultStopDistance;

        _destination = new Vector3(worldPos.x, transform.position.y, worldPos.z);
        // Debug.Log($"[Motor] Destination set to {_destination.Value}", this);
    }

    public void SetFollowTarget(Transform target, float stopDist)
    {
        _followTarget = target;
        _activeStopDistance = Mathf.Max(0.01f, stopDist);
        _destination = null;

        // Debug.Log($"[Motor] Following target {target.name} stop={_activeStopDistance}", this);
    }

    public void Clear()
    {
        _destination = null;
        _followTarget = null;
        _activeStopDistance = defaultStopDistance;
    }

    private void Update()
    {
        // If we have a non-kinematic Rigidbody, movement happens in FixedUpdate
        if (_rb != null && !_rb.isKinematic)
            return;

        TickMove(Time.deltaTime);
    }

    private void FixedUpdate()
    {
        if (_rb == null || _rb.isKinematic)
            return;

        TickMove(Time.fixedDeltaTime, useRigidbody: true);
    }

    private void TickMove(float dt, bool useRigidbody = false)
    {
        Vector3 targetPos;

        if (_followTarget != null)
        {
            targetPos = new Vector3(_followTarget.position.x, transform.position.y, _followTarget.position.z);
        }
        else if (_destination.HasValue)
        {
            targetPos = _destination.Value;
        }
        else
        {
            return;
        }

        Vector3 current = transform.position;
        float dx = targetPos.x - current.x;
        float dz = targetPos.z - current.z;
        float distSq = (dx * dx) + (dz * dz);
        float stopSq = _activeStopDistance * _activeStopDistance;

        if (distSq <= stopSq)
        {
            if (_destination.HasValue) _destination = null;
            return;
        }

        Vector3 dir = new Vector3(dx, 0f, dz).normalized;
        Vector3 next = current + dir * (moveSpeed * dt);

        if (useRigidbody && _rb != null)
            _rb.MovePosition(next);
        else
            transform.position = next;
    }
}
