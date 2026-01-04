using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HealthBarUI : MonoBehaviour
{
    [SerializeField] private PlayerHealth target;
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text hpText;

    private bool _warnedMissingText;

    private void Awake()
    {
        if (slider == null)
            slider = GetComponentInChildren<Slider>(true);

        if (hpText == null)
        {
            try { hpText = transform.Find("HPText")?.GetComponent<TMP_Text>(); }
            catch { hpText = null; }
        }
    }

    private void OnEnable()
    {
        if (target == null)
            AutoBindToPlayer();

        if (target != null)
            target.OnHealthChanged += OnHealthChanged;

        Refresh();
    }

    private void OnDisable()
    {
        if (target != null)
            target.OnHealthChanged -= OnHealthChanged;
    }

    public void Bind(PlayerHealth health)
    {
        if (target != null)
            target.OnHealthChanged -= OnHealthChanged;

        target = health;

        if (target != null)
        {
            target.OnHealthChanged += OnHealthChanged;
            Debug.Log($"[HealthBarUI] Bound to PlayerHealth on '{target.gameObject.name}'.", this);
        }

        Refresh();
    }

    private void AutoBindToPlayer()
    {
        var player = GameObject.FindWithTag("Player");
        if (player == null)
            return;

        var health = player.GetComponent<PlayerHealth>();
        if (health == null)
            return;

        Bind(health);
    }

    private void OnHealthChanged(float normalized)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (slider == null)
            return;

        if (hpText == null && !_warnedMissingText)
        {
            _warnedMissingText = true;
            Debug.LogWarning("[HealthBarUI] Missing HPText (expected child named 'HPText').", this);
        }

        if (target == null)
        {
            slider.value = 0f;
            if (hpText != null)
                hpText.text = "HP ? / ?";
            return;
        }

        // Drive slider by absolute health so slider.maxValue can be MaxHealth.
        slider.minValue = 0f;
        slider.maxValue = target.MaxHealth;
        slider.wholeNumbers = false;
        slider.value = target.CurrentHealth;

        if (hpText != null)
            hpText.text = $"HP {target.CurrentHealth} / {target.MaxHealth}";
    }
}
