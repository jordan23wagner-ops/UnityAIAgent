using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    [SerializeField] private PlayerHealth target;
    [SerializeField] private Slider slider;

    private void Awake()
    {
        if (slider == null)
            slider = GetComponentInChildren<Slider>(true);
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

        if (target == null)
        {
            slider.value = 0f;
            return;
        }

        slider.value = target.Normalized;
    }
}
