using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class PlayerInputAuthority : MonoBehaviour
{
    public event Action<Vector2> CameraPan;
    public event Action<Vector2> PointerPosition;
    public event Action Click;
    public event Action AttackDebug;

    [Header("Map / Action Names")]
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string cameraPanAction = "CameraPan";
    [SerializeField] private string pointerPosAction = "PointerPosition";
    [SerializeField] private string clickAction = "Click";
    [SerializeField] private string attackDebugAction = "AttackDebug";

    private PlayerInput _playerInput;
    private InputActionMap _map;
    private InputAction _pan, _pointer, _click, _attackDbg;

    private bool _bound;

    private void Awake()
    {
        _playerInput = GetComponent<PlayerInput>();
    }

    private void Update()
    {
        // Retry binding until PlayerInput.actions is assigned
        if (!_bound)
            TryBind();
    }

    private void TryBind()
    {
        if (_playerInput == null)
            _playerInput = GetComponent<PlayerInput>();

        if (_playerInput == null || _playerInput.actions == null)
            return; // wait until bootstrap assigns actions

        _map = _playerInput.actions.FindActionMap(actionMapName, true);
        if (_map == null)
        {
            Debug.LogError($"[InputAuthority] Action map '{actionMapName}' not found.", this);
            return;
        }

        _playerInput.SwitchCurrentActionMap(actionMapName);
        _map.Enable();

        _pan = _map.FindAction(cameraPanAction, true);
        _pointer = _map.FindAction(pointerPosAction, true);
        _click = _map.FindAction(clickAction, true);
        _attackDbg = _map.FindAction(attackDebugAction, false);

        _pan.performed += OnPan;
        _pan.canceled += OnPan;
        _pointer.performed += OnPointer;
        _pointer.canceled += OnPointer;
        _click.performed += OnClick;

        if (_attackDbg != null)
            _attackDbg.performed += OnAttackDebug;

        _bound = true;
        Debug.Log($"[InputAuthority] Active. actions={_playerInput.actions.name} map={_map.name}", this);
    }

    private void OnDisable()
    {
        if (!_bound) return;

        if (_pan != null) { _pan.performed -= OnPan; _pan.canceled -= OnPan; }
        if (_pointer != null) { _pointer.performed -= OnPointer; _pointer.canceled -= OnPointer; }
        if (_click != null) { _click.performed -= OnClick; }
        if (_attackDbg != null) { _attackDbg.performed -= OnAttackDebug; }

        _map?.Disable();
        _bound = false;
    }

    private void OnPan(InputAction.CallbackContext ctx) => CameraPan?.Invoke(ctx.ReadValue<Vector2>());
    private void OnPointer(InputAction.CallbackContext ctx) => PointerPosition?.Invoke(ctx.ReadValue<Vector2>());

    private void OnClick(InputAction.CallbackContext ctx)
    {
        Debug.Log("[InputAuthority] Click performed", this);
        Click?.Invoke();
    }

    private void OnAttackDebug(InputAction.CallbackContext ctx) => AttackDebug?.Invoke();
}
