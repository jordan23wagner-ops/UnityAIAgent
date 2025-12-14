using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class BossGate : MonoBehaviour
{
    [SerializeField] private GateDefinition gateDefinition;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool consumeKeyItem = false;
    [SerializeField] private Collider gateCollider;
    [SerializeField] private Renderer blockerRenderer;

    public UnityEvent OnOpened;
    public UnityEvent OnDenied;

    private bool _opened;
    private Vector3 _initialLocalPos;
    private Coroutine _shakeRoutine;

    private void Reset()
    {
        gateCollider = GetComponent<Collider>();
        blockerRenderer = GetComponentInChildren<Renderer>(true);
    }

    private void Awake()
    {
        _initialLocalPos = transform.localPosition;
        if (gateCollider == null) gateCollider = GetComponent<Collider>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_opened) return;
        if (other == null) return;

        bool isPlayerByTag = string.IsNullOrWhiteSpace(playerTag) ? true : other.CompareTag(playerTag);
        var inventory = other.GetComponentInParent<PlayerInventory>();
        if (!isPlayerByTag && inventory == null)
            return;

        if (inventory == null)
        {
            Debug.LogWarning($"[Gate] No PlayerInventory found on '{other.name}' or its parents.", this);
            return;
        }

        if (gateDefinition == null)
        {
            Debug.LogWarning("[Gate] No GateDefinition assigned.", this);
            Deny(inventory, "<unknown>", 1);
            return;
        }

        var requiredItem = gateDefinition.requiredItem;
        if (requiredItem == null)
        {
            Debug.LogWarning($"[Gate] GateDefinition '{gateDefinition.name}' has no requiredItem.", this);
            Deny(inventory, "<missing-required-item>", 1);
            return;
        }

        var requiredItemId = string.IsNullOrWhiteSpace(requiredItem.itemId) ? requiredItem.name : requiredItem.itemId;
        const int requiredAmount = 1;

        if (inventory.Has(requiredItemId, requiredAmount))
        {
            if (consumeKeyItem)
            {
                if (!inventory.TryConsume(requiredItemId, requiredAmount))
                {
                    Debug.LogWarning($"[Gate] consumeKeyItem requested but could not consume {requiredAmount}x {requiredItemId}.", this);
                }
            }

            OpenGate(requiredItemId, requiredAmount);
        }
        else
        {
            Deny(inventory, requiredItemId, requiredAmount);
        }
    }

    private void OpenGate(string itemId, int amount)
    {
        _opened = true;

        if (_shakeRoutine != null)
        {
            StopCoroutine(_shakeRoutine);
            _shakeRoutine = null;
        }

        if (gateCollider != null)
            gateCollider.enabled = false;

        if (blockerRenderer != null)
            blockerRenderer.enabled = false;

        var gateName = gateDefinition != null ? gateDefinition.name : name;
        Debug.Log($"[Gate] Opened {gateName} (required {itemId}x{amount})", this);

        try { OnOpened?.Invoke(); }
        catch (System.Exception ex) { Debug.LogError($"[Gate] OnOpened event threw: {ex.Message}", this); }
    }

    private void Deny(PlayerInventory inventory, string itemId, int amount)
    {
        int count = inventory != null ? inventory.Count(itemId) : 0;
        Debug.Log($"[Gate] Locked. Need {itemId}x{amount}. You have {count}", this);

        try { OnDenied?.Invoke(); }
        catch (System.Exception ex) { Debug.LogError($"[Gate] OnDenied event threw: {ex.Message}", this); }

        if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
        _shakeRoutine = StartCoroutine(ShakeRoutine(0.2f, 0.05f));
    }

    private IEnumerator ShakeRoutine(float durationSeconds, float amplitude)
    {
        float t = 0f;
        while (t < durationSeconds)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / durationSeconds);
            float strength = 1f - p;

            float x = (Mathf.PerlinNoise(0f, Time.time * 60f) - 0.5f) * 2f;
            float z = (Mathf.PerlinNoise(10f, Time.time * 60f) - 0.5f) * 2f;
            transform.localPosition = _initialLocalPos + new Vector3(x, 0f, z) * amplitude * strength;
            yield return null;
        }

        transform.localPosition = _initialLocalPos;
        _shakeRoutine = null;
    }
}
