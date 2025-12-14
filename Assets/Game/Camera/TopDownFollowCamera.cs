using UnityEngine;

public class TopDownFollowCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Follow")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 14f, -12f);
    [SerializeField] private float positionLerpSpeed = 12f;

    [Header("Look At")]
    [SerializeField] private bool lookAtTarget = true;
    [SerializeField] private Vector3 lookAtOffset = new Vector3(0f, 1f, 0f);
    [SerializeField] private float rotationSlerpSpeed = 12f;

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public Transform GetTarget()
    {
        return target;
    }

    private void LateUpdate()
    {
        EnsureTarget();
        if (target == null)
            return;

        // Position
        Vector3 desiredPosition = target.position + offset;

        // Safety clamp: never let camera go below a minimum height above the target
        float minHeight = target.position.y + 8f;
        if (desiredPosition.y < minHeight)
            desiredPosition.y = minHeight;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, positionLerpSpeed * Time.deltaTime);

        // Rotation
        if (!lookAtTarget)
            return;

        Vector3 lookPoint = target.position + lookAtOffset;
        Vector3 toLookPoint = lookPoint - transform.position;
        if (toLookPoint.sqrMagnitude <= 0.000001f)
            return;

        Quaternion desiredRotation = Quaternion.LookRotation(toLookPoint, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationSlerpSpeed * Time.deltaTime);
    }

    private void EnsureTarget()
    {
        if (target != null)
            return;

        var player = GameObject.FindWithTag("Player");
        if (player != null)
            target = player.transform;
    }
}
