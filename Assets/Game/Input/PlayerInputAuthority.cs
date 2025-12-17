using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Game.Input
{
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

        [Header("Blocking")]
        [Tooltip("If true, gameplay clicks/attacks are ignored (useful while UI is open).")]
        [SerializeField] private bool gameplayInputBlocked;

        private PlayerInput _playerInput;
        private InputActionMap _map;
        private InputAction _pan, _pointer, _click, _attackDbg;

        private bool _bound;
        private bool _cachedPointerOverUI;
        private int _cachedPointerFrame = -1;

        public void SetGameplayInputBlocked(bool blocked) => gameplayInputBlocked = blocked;

        // Support for UI blocking from MerchantShopUI
        public void SetUIBlocked(bool blocked)
        {
            enabled = !blocked;
        }

        private void Awake()
        {
            _playerInput = GetComponent<PlayerInput>();
        }

        private void Update()
        {
            // Retry binding until PlayerInput.actions is assigned
            if (!_bound)
                TryBind();

            // Cache pointer-over-UI once per frame to avoid calling IsPointerOverGameObject inside input callbacks
            if (Time.frameCount != _cachedPointerFrame)
            {
                _cachedPointerFrame = Time.frameCount;
                _cachedPointerOverUI = (EventSystem.current != null) && EventSystem.current.IsPointerOverGameObject();
            }
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

            if (gameplayInputBlocked)
                return;

            // If a UI (like merchant/shop) is open, block gameplay clicks.
            if (Abyss.Shop.MerchantShopUI.IsOpen)
                return;

            // Use cached per-frame value instead of calling IsPointerOverGameObject from callbacks.
            if (_cachedPointerOverUI)
                return;

            Click?.Invoke();
        }

        private void OnAttackDebug(InputAction.CallbackContext ctx)
        {
            if (gameplayInputBlocked)
                return;

            AttackDebug?.Invoke();
        }
    }

}
