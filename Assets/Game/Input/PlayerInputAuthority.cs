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
        private bool _blockedClickLogged = false;

        // UI lock state driven by UI open/close events
        private bool _uiInputLocked = false;
        private bool _ignoreNextWorldClick = false;

        public bool IsUiInputLocked => _uiInputLocked;

        public void SetGameplayInputBlocked(bool blocked) => gameplayInputBlocked = blocked;

        // Support for UI blocking from MerchantShopUI
        public void SetUIBlocked(bool blocked)
        {
            enabled = !blocked;
        }

        // Explicit API for UI to lock/unlock gameplay input. When unlocking, the next world click will be ignored.
        public void SetUiInputLocked(bool locked)
        {
            _uiInputLocked = locked;
            if (!locked)
            {
                _ignoreNextWorldClick = true;
                _blockedClickLogged = false;
            }
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

        private void OnEnable()
        {
            Abyss.Shop.MerchantShopUI.OnOpenChanged += HandleMerchantUiOpenChanged;
        }

        private void HandleMerchantUiOpenChanged(bool open)
        {
            _uiInputLocked = open;
            if (!open)
            {
                // next world click (often the click that closed UI) should be ignored
                _ignoreNextWorldClick = true;
                _blockedClickLogged = false;
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
            Abyss.Shop.MerchantShopUI.OnOpenChanged -= HandleMerchantUiOpenChanged;
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
            if (gameplayInputBlocked)
                return;

            // If UI input lock is active, block world clicks unless the click is over UI.
            if (_uiInputLocked)
            {
                if (!_cachedPointerOverUI)
                {
                    if (!_blockedClickLogged)
                    {
                        Debug.Log("[InputAuthority] Click blocked because UI is open", this);
                        _blockedClickLogged = true;
                    }
                    return;
                }
                // if pointer is over UI, allow UI to handle the click and do not process world click
                return;
            }

            // Ignore one world click immediately after UI closes (prevents the "click-out" problem)
            if (_ignoreNextWorldClick)
            {
                _ignoreNextWorldClick = false;
                return;
            }

            // If pointer is over UI, don't process as world click
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
