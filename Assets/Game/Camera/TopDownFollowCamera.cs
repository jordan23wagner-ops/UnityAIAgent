using UnityEngine;

public class TopDownFollowCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Offset")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 10f, -10f);

    [Header("Smoothing")]
    [SerializeField] private float followSpeed = 10f;

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
        if (target == null)
        {
            var player = GameObject.FindWithTag("Player");
            if (player == null)
                return;
            target = player.transform;
        }

        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            followSpeed * Time.deltaTime
        );
    }
}
