using UnityEngine;
using UnityEngine.InputSystem;

public class DebugPlayerMover_NewInput : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;

    private void Update()
    {
        // Works with the New Input System without needing an InputActionAsset.
        var kb = Keyboard.current;
        if (kb == null) return;

        float x = 0f;
        float z = 0f;

        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) z -= 1f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) z += 1f;

        Vector3 dir = new Vector3(x, 0f, z);
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        transform.position += dir * moveSpeed * Time.deltaTime;
    }
}
