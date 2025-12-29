using UnityEngine;
using Abyssbound.DeathDrop;

[DisallowMultipleComponent]
public sealed class PlayerClickToMoveController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera raycastCamera;
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private SimplePlayerCombat combat;

    [Header("Raycast")]
    [SerializeField] private float maxRayDistance = 500f;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private LayerMask enemyMask = 0;
    [SerializeField] private LayerMask interactableMask = 0;

    [Header("Ranges")]
    [SerializeField] private float interactRange = 1.75f;

    [Header("Debug")]
    [SerializeField] private bool logClicks = true;

    private Vector2 _pointerPosition;

    private enum IntentType { None, MoveToPoint, AttackTarget, InteractTarget }

    private IntentType _intent;
    private Vector3 _movePoint;
    private EnemyHealth _enemyTarget;
    private Transform _interactTarget;
    private bool _resolved;

    private void Awake()
    {
        if (raycastCamera == null)
            raycastCamera = Camera.main;

        if (movement == null)
            movement = GetComponent<PlayerMovement>();

        if (combat == null)
            combat = GetComponent<SimplePlayerCombat>();
    }

    public void SetPointerPosition(Vector2 screenPosition)
    {
        _pointerPosition = screenPosition;
    }

    public void HandleClick()
    {
        if (!Application.isPlaying)
            return;

        try
        {
            if (Time.unscaledTime < DeathDropManager.SuppressGameplayInputUntil)
                return;
        }
        catch { }

        if (raycastCamera == null)
            raycastCamera = Camera.main;

        if (raycastCamera == null)
        {
            Debug.LogWarning("[ClickToMove] No camera available for raycasts.", this);
            return;
        }

        var ray = raycastCamera.ScreenPointToRay(_pointerPosition);
        int mask = groundMask | enemyMask | interactableMask;

        if (!Physics.Raycast(ray, out var hit, maxRayDistance, mask, QueryTriggerInteraction.Ignore))
            return;

        var hitObj = hit.collider != null ? hit.collider.gameObject : null;

        // Enemy click
        if (hitObj != null && ((enemyMask.value & (1 << hitObj.layer)) != 0))
        {
            var eh = hit.collider.GetComponentInParent<EnemyHealth>();
            if (eh != null)
            {
                _intent = IntentType.AttackTarget;
                _enemyTarget = eh;
                _interactTarget = null;
                _resolved = false;

                if (combat != null)
                    combat.SetSelectedTarget(eh);

                if (movement != null)
                    movement.MoveToTransform(eh.transform, GetAttackStopRange());

                if (logClicks)
                    Debug.Log($"[ClickToMove] AttackTarget: {eh.name}", this);

                return;
            }
        }

        // Interactable click
        if (hitObj != null && ((interactableMask.value & (1 << hitObj.layer)) != 0))
        {
            _intent = IntentType.InteractTarget;
            _interactTarget = hit.collider.transform;
            _enemyTarget = null;
            _resolved = false;

            if (movement != null)
                movement.MoveToTransform(_interactTarget, Mathf.Max(0.1f, interactRange));

            if (logClicks)
                Debug.Log($"[ClickToMove] InteractTarget: {_interactTarget.name}", this);

            return;
        }

        // Ground click (fallback)
        _intent = IntentType.MoveToPoint;
        _movePoint = hit.point;
        _enemyTarget = null;
        _interactTarget = null;
        _resolved = false;

        if (movement != null)
            movement.MoveToPoint(_movePoint);

        if (logClicks)
            Debug.Log($"[ClickToMove] MoveToPoint: {_movePoint}", this);
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        if (_resolved)
            return;

        switch (_intent)
        {
            case IntentType.AttackTarget:
                TickAttackIntent();
                break;
            case IntentType.InteractTarget:
                TickInteractIntent();
                break;
        }
    }

    private void TickAttackIntent()
    {
        if (_enemyTarget == null)
        {
            _resolved = true;
            return;
        }

        float stopRange = GetAttackStopRange();
        float distSq = (_enemyTarget.transform.position - transform.position).sqrMagnitude;
        if (distSq > stopRange * stopRange)
            return;

        if (combat != null)
        {
            Debug.Log("[ClickToMove] In range -> TryAttack", this);
            combat.TryAttack();
        }

        _resolved = true;
    }

    private void TickInteractIntent()
    {
        if (_interactTarget == null)
        {
            _resolved = true;
            return;
        }

        float distSq = (_interactTarget.position - transform.position).sqrMagnitude;
        if (distSq > interactRange * interactRange)
            return;

        Debug.Log($"[ClickToMove] In range -> Interact({_interactTarget.name})", this);
        _interactTarget.gameObject.SendMessage("Interact", SendMessageOptions.DontRequireReceiver);
        _resolved = true;
    }

    private float GetAttackStopRange()
    {
        // Prefer combat range if available; otherwise a safe default.
        if (combat != null)
            return Mathf.Max(0.1f, combat.Range);

        return 1.75f;
    }
}
