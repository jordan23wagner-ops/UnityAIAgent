using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed class BossHealthUI : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Image fillImage;
    [SerializeField] private Text bossNameText;

    [Header("Follow")]
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 2.5f, 0f);

    private EnemyHealth _enemyHealth;
    private Component _genericHealth;
    private Transform _followTarget;
    private UnityAction _onDied;

    private static FieldInfo _maxHealthField;
    private static FieldInfo _currentHealthField;

    private static FieldInfo _genericMaxField;
    private static FieldInfo _genericCurField;
    private static System.Type _genericCachedType;

    private void Awake()
    {
        if (healthSlider == null)
            healthSlider = GetComponentInChildren<Slider>(true);

        if (fillImage == null)
        {
            // Prefer a child explicitly named Fill when present.
            var fillT = transform.Find("Fill");
            if (fillT != null)
                fillImage = fillT.GetComponent<Image>();

            if (fillImage == null)
                fillImage = GetComponentInChildren<Image>(true);
        }

        if (bossNameText == null)
            bossNameText = GetComponentInChildren<Text>(true);

        SetVisible(false);
    }

    private void OnDisable()
    {
        Unhook();
    }

    public void SetTarget(Transform followTarget)
    {
        _followTarget = followTarget;
    }

    public void Bind(EnemyHealth enemyHealth, Transform followTarget, string displayName)
    {
        Unhook();

        _enemyHealth = enemyHealth;
        _genericHealth = null;
        _followTarget = followTarget != null ? followTarget : (enemyHealth != null ? enemyHealth.transform : null);

        if (bossNameText != null)
            bossNameText.text = string.IsNullOrWhiteSpace(displayName) ? "Boss" : displayName;

        if (_enemyHealth == null)
        {
            SetVisible(false);
            return;
        }

        _onDied = OnBossDied;
        try { _enemyHealth.OnDied.AddListener(_onDied); }
        catch { }

        SetVisible(true);
        Refresh();
    }

    public void Bind(Component healthComponent, Transform followTarget, string displayName)
    {
        Unhook();

        _enemyHealth = healthComponent as EnemyHealth;
        _genericHealth = _enemyHealth == null ? healthComponent : null;
        _followTarget = followTarget != null ? followTarget : (healthComponent != null ? healthComponent.transform : null);

        if (bossNameText != null)
            bossNameText.text = string.IsNullOrWhiteSpace(displayName) ? "Boss" : displayName;

        if (_enemyHealth == null && _genericHealth == null)
        {
            SetVisible(false);
            return;
        }

        // Only hook death event for EnemyHealth (best-effort).
        if (_enemyHealth != null)
        {
            _onDied = OnBossDied;
            try { _enemyHealth.OnDied.AddListener(_onDied); }
            catch { }
        }

        SetVisible(true);
        Refresh();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
            return;

        if (_enemyHealth == null && _genericHealth == null)
        {
            SetVisible(false);
            return;
        }

        if (_followTarget == null)
        {
            if (_enemyHealth != null) _followTarget = _enemyHealth.transform;
            else if (_genericHealth != null) _followTarget = _genericHealth.transform;
        }

        if (_followTarget != null)
            transform.position = _followTarget.position + followOffset;

        Refresh();
    }

    private void Refresh()
    {
        if (healthSlider == null && fillImage == null)
            return;

        if (_enemyHealth == null && _genericHealth == null)
        {
            if (healthSlider != null) healthSlider.value = 0f;
            if (fillImage != null) fillImage.fillAmount = 0f;
            return;
        }

        int max;
        int cur;
        if (_enemyHealth != null)
        {
            max = TryGetMaxHealth(_enemyHealth);
            cur = TryGetCurrentHealth(_enemyHealth);
        }
        else
        {
            max = TryGetGenericMax(_genericHealth);
            cur = TryGetGenericCurrent(_genericHealth);
        }

        if (max <= 0)
        {
            if (healthSlider != null)
            {
                healthSlider.maxValue = 1f;
                healthSlider.value = 0f;
            }

            if (fillImage != null)
                fillImage.fillAmount = 0f;
            return;
        }

        if (healthSlider != null)
        {
            healthSlider.maxValue = max;
            healthSlider.value = Mathf.Clamp(cur, 0, max);
        }

        if (fillImage != null)
            fillImage.fillAmount = Mathf.Clamp01((float)cur / max);

        if (cur <= 0)
            SetVisible(false);
    }

    private void OnBossDied()
    {
        SetVisible(false);
        Unhook();
    }

    private void Unhook()
    {
        if (_enemyHealth != null && _onDied != null)
        {
            try { _enemyHealth.OnDied.RemoveListener(_onDied); }
            catch { }
        }

        _onDied = null;
        _enemyHealth = null;
        _genericHealth = null;
        _followTarget = null;
    }

    private void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible)
            gameObject.SetActive(visible);
    }

    private static int TryGetMaxHealth(EnemyHealth health)
    {
        EnsureHealthFields(health);
        if (_maxHealthField == null) return 0;
        try { return (int)_maxHealthField.GetValue(health); }
        catch { return 0; }
    }

    private static int TryGetCurrentHealth(EnemyHealth health)
    {
        EnsureHealthFields(health);
        if (_currentHealthField == null) return 0;
        try { return (int)_currentHealthField.GetValue(health); }
        catch { return 0; }
    }

    private static void EnsureHealthFields(EnemyHealth health)
    {
        if (health == null)
            return;

        if (_maxHealthField != null && _currentHealthField != null)
            return;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var t = health.GetType();
        _maxHealthField = t.GetField("maxHealth", flags);
        _currentHealthField = t.GetField("currentHealth", flags);
    }

    private static int TryGetGenericMax(Component health)
    {
        EnsureGenericFields(health);
        if (_genericMaxField == null) return 0;
        try { return (int)_genericMaxField.GetValue(health); }
        catch { return 0; }
    }

    private static int TryGetGenericCurrent(Component health)
    {
        EnsureGenericFields(health);
        if (_genericCurField == null) return 0;
        try { return (int)_genericCurField.GetValue(health); }
        catch { return 0; }
    }

    private static void EnsureGenericFields(Component health)
    {
        if (health == null) return;

        var t = health.GetType();
        if (_genericCachedType == t && _genericMaxField != null && _genericCurField != null)
            return;

        _genericCachedType = t;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        _genericMaxField = t.GetField("maxHealth", flags) ?? t.GetField("MaxHealth", flags);
        _genericCurField = t.GetField("currentHealth", flags) ?? t.GetField("CurrentHealth", flags);
    }
}
