using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;

    [Header("Arrival")]
    [SerializeField] private float stopDistance = 0.1f;

    private bool _hasDestination;
    private Vector3 _destination;
    private Transform _followTarget;
    private float _followStopRange;

    public void MoveToPoint(Vector3 worldPoint)
    {
        _followTarget = null;
        _followStopRange = 0f;
        _destination = worldPoint;
        _hasDestination = true;
    }

    public void MoveToTransform(Transform target, float stopRange)
    {
        _followTarget = target;
        _followStopRange = Mathf.Max(0.05f, stopRange);
        _hasDestination = target != null;
    }

    public void Stop()
    {
        _hasDestination = false;
        _followTarget = null;
    }

    private void Update()
    {
        if (!_hasDestination)
            return;

        if (_followTarget != null)
            _destination = _followTarget.position;

        var to = _destination - transform.position;
        to.y = 0f;

        float stop = _followTarget != null ? _followStopRange : stopDistance;
        if (to.sqrMagnitude <= stop * stop)
        {
            if (_followTarget == null)
                _hasDestination = false;
            return;
        }

        var dir = to.normalized;

        transform.position += dir * moveSpeed * Time.deltaTime;
    }
}
