using Game.Input;
using UnityEngine;

[DisallowMultipleComponent]
public class ClickToMoveController : MonoBehaviour
{
    [Header("Layers (optional; leave empty to raycast everything)")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private LayerMask npcMask;

    [Header("Ranges")]
    [SerializeField] private float attackStopDistance = 1.75f;
    [SerializeField] private float interactStopDistance = 1.25f;

    private PlayerInputAuthority _input;
    private PlayerMovementMotor _motor;
    private CombatLoopController _combatLoop;

    private Vector2 _pointerPos;

    private void Awake()
    {
        _input = GetComponent<PlayerInputAuthority>();
        _motor = GetComponent<PlayerMovementMotor>();
        _combatLoop = GetComponent<CombatLoopController>();
    }

    private void OnEnable()
    {
        if (_combatLoop == null)
            _combatLoop = GetComponent<CombatLoopController>();

        if (_input == null)
            _input = GetComponent<PlayerInputAuthority>();

        if (_motor == null)
            _motor = GetComponent<PlayerMovementMotor>();

        if (_input == null) Debug.LogError("[ClickToMove] Missing PlayerInputAuthority.", this);
        if (_motor == null) Debug.LogError("[ClickToMove] Missing PlayerMovementMotor.", this);
        if (_combatLoop == null) Debug.LogError("[ClickToMove] Missing CombatLoopController.", this);

        if (_input != null)
        {
            _input.Click += OnClick;
            _input.PointerPosition += OnPointer;
            Debug.Log("[ClickToMove] Enabled + subscribed", this);
        }
    }

    private void OnDisable()
    {
        if (_input != null)
        {
            _input.Click -= OnClick;
            _input.PointerPosition -= OnPointer;
        }
    }

    private void OnPointer(Vector2 pos) => _pointerPos = pos;

    private void OnClick()
    {
        if (Camera.main == null)
        {
            Debug.LogError("[ClickToMove] Camera.main is NULL. Tag your camera as MainCamera.", this);
            return;
        }

        Ray ray = Camera.main.ScreenPointToRay(_pointerPos);

        bool hasAnyMask = groundMask.value != 0 || enemyMask.value != 0 || npcMask.value != 0;
        int combinedMask = hasAnyMask ? (groundMask.value | enemyMask.value | npcMask.value) : ~0;

        if (!Physics.Raycast(ray, out RaycastHit hit, 500f, combinedMask))
            return;

        int hitLayer = hit.collider.gameObject.layer;

        // Enemy click - engage target + auto-attack
        bool enemyMaskSet = enemyMask.value != 0;
        bool hitIsEnemyLayer = enemyMaskSet && (((1 << hitLayer) & enemyMask.value) != 0);
        var enemyHealth = hit.collider.GetComponentInParent<EnemyHealth>();

        if (hitIsEnemyLayer || (!enemyMaskSet && enemyHealth != null))
        {
            if (enemyHealth != null)
            {
                if (_combatLoop != null) _combatLoop.SetTarget(enemyHealth, attackStopDistance);
                return;
            }

            if (_motor != null) _motor.SetFollowTarget(hit.collider.transform, attackStopDistance);
            return;
        }

        // NPC click - follow to interact range (interaction system comes next)
        if (npcMask.value != 0 && ((1 << hitLayer) & npcMask.value) != 0)
        {
            if (_combatLoop != null)
                _combatLoop.ClearTarget();
            if (_motor != null)
                _motor.SetFollowTarget(hit.collider.transform, interactStopDistance);
            return;
        }

        // Ground click - move + cancel combat
        if (groundMask.value == 0 || ((1 << hitLayer) & groundMask.value) != 0)
        {
            if (_combatLoop != null)
                _combatLoop.ClearTarget();
            if (_motor != null)
                _motor.SetDestination(hit.point);
            return;
        }
    }
}
