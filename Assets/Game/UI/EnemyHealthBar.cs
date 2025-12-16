using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class EnemyHealthBar : MonoBehaviour
{
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2f, 0f);

    private EnemyHealth _target;
    private Image _fill;
    private Image _bg;
    private RectTransform _fillRt;

    private static Sprite _whiteSprite;

    private bool _subscribed;

    private void Awake()
    {
        EnsureUiBuilt();
    }

    public void Bind(EnemyHealth target)
    {
        if (_target != null)
            Unsubscribe();

        _target = target;
        EnsureUiBuilt();
        gameObject.SetActive(true);

        Subscribe();
        UpdateFill();
    }

    public void Unbind()
    {
        Unsubscribe();
        _target = null;
    }

    private void Subscribe()
    {
        if (_subscribed)
            return;

        if (_target == null)
            return;

        _target.OnDamaged += OnTargetDamaged;
        _target.OnDeath += OnTargetDeath;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
            return;

        if (_target != null)
        {
            _target.OnDamaged -= OnTargetDamaged;
            _target.OnDeath -= OnTargetDeath;
        }

        _subscribed = false;
    }

    private void OnTargetDamaged(EnemyHealth enemy, float amount)
    {
        if (enemy != _target)
            return;

        UpdateFill();
    }

    private void OnTargetDeath(EnemyHealth enemy)
    {
        if (enemy != _target)
            return;

        UpdateFill();
    }

    private void UpdateFill()
    {
        if (_target == null)
            return;

        int max = _target.MaxHealth;
        int cur = _target.CurrentHealth;
        float fill = (max <= 0) ? 0f : Mathf.Clamp01((float)cur / max);

        if (_fillRt != null)
        {
            var maxAnchor = _fillRt.anchorMax;
            maxAnchor.x = fill;
            _fillRt.anchorMax = maxAnchor;
        }
    }

    private static void EnsureWhiteSprite()
    {
        if (_whiteSprite != null)
            return;

        var tex = Texture2D.whiteTexture;
        if (tex == null)
            return;

        _whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
    }

    private void EnsureUiBuilt()
    {
        EnsureWhiteSprite();

        var rt = GetComponent<RectTransform>();
        if (rt == null)
            rt = gameObject.AddComponent<RectTransform>();

        rt.sizeDelta = new Vector2(1.2f, 0.15f);
        rt.localScale = Vector3.one;

        if (_bg == null || _fill == null)
        {
            // Build background
            var bgGo = transform.Find("BG")?.gameObject;
            if (bgGo == null)
            {
                bgGo = new GameObject("BG");
                bgGo.transform.SetParent(transform, false);
            }

            var bgRt = bgGo.GetComponent<RectTransform>();
            if (bgRt == null) bgRt = bgGo.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            _bg = bgGo.GetComponent<Image>();
            if (_bg == null) _bg = bgGo.AddComponent<Image>();
            _bg.raycastTarget = false;
            _bg.color = new Color(0f, 0f, 0f, 0.7f);
            if (_bg.sprite == null)
                _bg.sprite = _whiteSprite;
            _bg.type = Image.Type.Simple;

            // Build fill
            var fillGo = bgGo.transform.Find("Fill")?.gameObject;
            if (fillGo == null)
            {
                fillGo = new GameObject("Fill");
                fillGo.transform.SetParent(bgGo.transform, false);
            }

            var fillRt = fillGo.GetComponent<RectTransform>();
            if (fillRt == null) fillRt = fillGo.AddComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            fillRt.pivot = new Vector2(0f, 0.5f);

            _fillRt = fillRt;

            _fill = fillGo.GetComponent<Image>();
            if (_fill == null) _fill = fillGo.AddComponent<Image>();
            _fill.raycastTarget = false;
            _fill.color = new Color(0.2f, 1f, 0.2f, 0.95f);
            if (_fill.sprite == null)
                _fill.sprite = _whiteSprite;
            _fill.type = Image.Type.Simple;
        }
        else
        {
            if (_fillRt == null)
                _fillRt = _fill != null ? _fill.rectTransform : null;
        }
    }

    private void LateUpdate()
    {
        if (!_target)
            return;

        // Pooled enemies can be disabled; release immediately.
        if (!_target.isActiveAndEnabled || _target.IsDead)
        {
            EnemyHealthBarManager.ReleaseFor(_target);
            return;
        }

        transform.position = _target.transform.position + worldOffset;

        var cam = WorldUiRoot.GetCamera();
        if (cam != null)
        {
            // Camera or its transform can become invalid on scene reload.
            if (!cam || !cam.transform)
                return;

            var toCam = cam.transform.position - transform.position;
            if (toCam.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(toCam);
        }

        // Keep this as a failsafe (covers any future healing/reset pathways too).
        UpdateFill();
    }

    private void OnDisable()
    {
        // Safety: if disabled externally while still bound, release mapping.
        if (_target)
            EnemyHealthBarManager.ReleaseFor(_target);

        Unsubscribe();
    }
}
