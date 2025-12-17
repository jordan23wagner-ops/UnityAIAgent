using Game.Input;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerInputGameplayBinder : MonoBehaviour
{
    [SerializeField] private PlayerInputAuthority authority;
    [SerializeField] private PlayerClickToMoveController clickToMove;
    [SerializeField] private SimplePlayerCombat combat;

    private void Awake()
    {
        if (authority == null)
            authority = GetComponent<PlayerInputAuthority>();

        if (clickToMove == null)
            clickToMove = GetComponent<PlayerClickToMoveController>();

        if (combat == null)
            combat = GetComponent<SimplePlayerCombat>();
    }

    private void OnEnable()
    {
        if (authority == null)
            authority = GetComponent<PlayerInputAuthority>();

        if (authority == null)
        {
            Debug.LogWarning("[PlayerInputGameplayBinder] Missing PlayerInputAuthority.", this);
            return;
        }

        authority.PointerPosition += HandlePointerPosition;
        authority.Click += HandleClick;
        authority.AttackDebug += HandleAttack;
    }

    private void OnDisable()
    {
        if (authority == null)
            return;

        authority.PointerPosition -= HandlePointerPosition;
        authority.Click -= HandleClick;
        authority.AttackDebug -= HandleAttack;
    }

    private void HandlePointerPosition(Vector2 screenPosition)
    {
        if (clickToMove != null)
            clickToMove.SetPointerPosition(screenPosition);
    }

    private void HandleClick()
    {
        if (clickToMove != null)
            clickToMove.HandleClick();
    }

    private void HandleAttack()
    {
        if (combat != null)
            combat.TryAttack();
    }
}
