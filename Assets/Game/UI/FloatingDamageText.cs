using System;
using System.Reflection;
using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
public class FloatingDamageText : MonoBehaviour
{
    [SerializeField] private float lifetimeSeconds = 0.9f;
    [SerializeField] private float riseSpeed = 1.0f;

    private float _time;
    private bool _finished;

    // TMP (optional, reflection-based)
    private TMP_Text _tmpDirect;
    private Color _tmpDirectBaseColor;

    private Component _tmpText;
    private PropertyInfo _tmpTextProp;
    private PropertyInfo _tmpColorProp;
    private PropertyInfo _tmpFontSizeProp;
    private PropertyInfo _tmpEnableAutoSizingProp;
    private PropertyInfo _tmpAlignmentProp;
    private Color _tmpBaseColor;

    // TextMesh fallback
    private TextMesh _textMesh;
    private Color _meshBaseColor;

    private static bool _loggedAwake;

    internal bool DebugForceVisible;

    internal Action<FloatingDamageText> Finished;

    public void SetDefaults(float lifetime, float rise)
    {
        lifetimeSeconds = Mathf.Max(0.05f, lifetime);
        riseSpeed = rise;
    }

    private void Awake()
    {
        if (!_loggedAwake)
        {
            _loggedAwake = true;
            Debug.Log("[DMG] FloatingDamageText Awake", this);
        }

        // Prefer direct TMP reference (supports TMP on children).
        TryResolveTmpDirect();

        // Legacy path: reflection TMP on the same GO.
        if (_tmpDirect == null)
            TryResolveTmp();

        if (_tmpDirect != null)
        {
            // TMP: keep a sane default (may be overridden by debugForceVisible on enable).
            TrySetTmpDirectVisuals();
            _tmpDirectBaseColor = new Color(1f, 0.2f, 0.2f, 1f);
        }
        else if (_tmpText != null)
        {
            // TMP: keep a sane default (may be overridden by debugForceVisible on enable).
            TrySetTmpVisuals();
            _tmpBaseColor = Color.red;
        }
        else
        {
            _textMesh = GetComponent<TextMesh>();
            if (_textMesh == null)
                _textMesh = gameObject.AddComponent<TextMesh>();

            // TextMesh: keep a sane default (may be overridden by debugForceVisible on enable).
            _textMesh.anchor = TextAnchor.MiddleCenter;
            _textMesh.alignment = TextAlignment.Center;
            _textMesh.fontSize = 32;
            _textMesh.characterSize = 0.07f;
            _textMesh.color = Color.red;

            _meshBaseColor = _textMesh.color;
        }

        // Common: neutral rotation to avoid inherited weirdness.
        transform.rotation = Quaternion.identity;
        transform.localEulerAngles = Vector3.zero;

        // Ensure no mirroring.
        var ls = transform.localScale;
        transform.localScale = new Vector3(Mathf.Abs(ls.x), Mathf.Abs(ls.y), Mathf.Abs(ls.z));
    }

    private void OnEnable()
    {
        _time = 0f;
        _finished = false;

        // Common: neutral rotation and no mirrored scale.
        transform.rotation = Quaternion.identity;
        transform.localEulerAngles = Vector3.zero;

        var ls = transform.localScale;
        transform.localScale = new Vector3(Mathf.Abs(ls.x), Mathf.Abs(ls.y), Mathf.Abs(ls.z));

        if (DebugForceVisible)
        {
            ApplyDebugVisibilityDefaults();
            NudgeTowardCameraOnce();
        }

        // Reset alpha on reuse.
        SetAlpha(1f);
    }

    public void Init(int amount)
    {
        string s = IntStringCache.Get(amount);

        if (_tmpDirect != null)
        {
            _tmpDirect.text = s;
            return;
        }

        if (_tmpText != null && _tmpTextProp != null)
        {
            _tmpTextProp.SetValue(_tmpText, s);
            return;
        }

        if (_textMesh != null)
            _textMesh.text = s;
    }

    private void Update()
    {
        if (_finished)
            return;

        float dt = Time.deltaTime;
        _time += dt;

        transform.position += Vector3.up * (riseSpeed * dt);

        float t01 = lifetimeSeconds <= 0.01f ? 1f : Mathf.Clamp01(_time / lifetimeSeconds);
        SetAlpha(1f - t01);

        if (_time >= lifetimeSeconds)
        {
            _finished = true;
            gameObject.SetActive(false);
            Finished?.Invoke(this);
        }
    }

    private void LateUpdate()
    {
        if (!gameObject.activeInHierarchy)
            return;

        var cam = WorldUiRoot.GetCamera();
        if (cam == null)
            return;

        if (!cam || !cam.transform)
            return;

        // Extra safety: negative local scale can mirror text even with correct rotation.
        var ls = transform.localScale;
        if (ls.x < 0f || ls.y < 0f || ls.z < 0f)
        {
            transform.localScale = new Vector3(Mathf.Abs(ls.x), Mathf.Abs(ls.y), Mathf.Abs(ls.z));
            if (DebugForceVisible)
                ApplyDebugVisibilityDefaults();
        }

        // Billboard (NOT mirrored for this project):
        // Use the reversed vector so TextMesh/TMP appear readable (front-facing in this project).
        var toCam = transform.position - cam.transform.position;
        if (toCam.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(toCam);
    }

    private void SetAlpha(float a)
    {
        a = Mathf.Clamp01(a);

        if (_tmpDirect != null)
        {
            var c = _tmpDirectBaseColor;
            c.a = a;
            _tmpDirect.color = c;
            return;
        }

        if (_tmpText != null && _tmpColorProp != null)
        {
            var c = _tmpBaseColor;
            c.a = a;
            _tmpColorProp.SetValue(_tmpText, c);
            return;
        }

        if (_textMesh != null)
        {
            var c = _meshBaseColor;
            c.a = a;
            _textMesh.color = c;
        }
    }

    private void TryResolveTmp()
    {
        // We deliberately compile without a TMPro dependency.
        var tmpTextType = Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro");
        if (tmpTextType == null)
            return;

        _tmpText = GetComponent(tmpTextType);
        if (_tmpText == null)
            return;

        _tmpTextProp = tmpTextType.GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
        _tmpColorProp = tmpTextType.GetProperty("color", BindingFlags.Instance | BindingFlags.Public);
        _tmpFontSizeProp = tmpTextType.GetProperty("fontSize", BindingFlags.Instance | BindingFlags.Public);
        _tmpEnableAutoSizingProp = tmpTextType.GetProperty("enableAutoSizing", BindingFlags.Instance | BindingFlags.Public);
        _tmpAlignmentProp = tmpTextType.GetProperty("alignment", BindingFlags.Instance | BindingFlags.Public);

        if (_tmpColorProp != null)
        {
            var cObj = _tmpColorProp.GetValue(_tmpText);
            if (cObj is Color c)
                _tmpBaseColor = c;
            else
                _tmpBaseColor = new Color(1f, 0.2f, 0.2f, 1f);
        }
    }

    private void TryResolveTmpDirect()
    {
        try
        {
            _tmpDirect = GetComponentInChildren<TMP_Text>(true);
            if (_tmpDirect != null)
            {
                var c = _tmpDirect.color;
                if (c.a <= 0.01f) c = new Color(1f, 0.2f, 0.2f, 1f);
                c.a = 1f;
                _tmpDirect.color = c;
                _tmpDirectBaseColor = c;
            }
        }
        catch
        {
            _tmpDirect = null;
        }
    }

    private void TrySetTmpDirectVisuals()
    {
        if (_tmpDirect == null)
            return;

        try
        {
            _tmpDirect.textWrappingMode = TextWrappingModes.NoWrap;
            _tmpDirect.fontSize = Mathf.Max(_tmpDirect.fontSize, 24f);
            var c = _tmpDirect.color;
            if (c.a <= 0.01f) c = Color.red;
            c.a = 1f;
            _tmpDirect.color = c;
            _tmpDirectBaseColor = c;
        }
        catch { }
    }

    private void TrySetTmpVisuals()
    {
        if (_tmpText == null)
            return;

        try
        {
            if (_tmpColorProp != null)
                _tmpColorProp.SetValue(_tmpText, Color.red);

            if (_tmpFontSizeProp != null)
                _tmpFontSizeProp.SetValue(_tmpText, 24f);

            if (_tmpEnableAutoSizingProp != null)
                _tmpEnableAutoSizingProp.SetValue(_tmpText, false);

            if (_tmpAlignmentProp != null)
            {
                var enumType = _tmpAlignmentProp.PropertyType;
                if (enumType != null && enumType.IsEnum)
                {
                    try
                    {
                        var center = Enum.Parse(enumType, "Center", ignoreCase: true);
                        _tmpAlignmentProp.SetValue(_tmpText, center);
                    }
                    catch
                    {
                        // Ignore if enum doesn't have Center.
                    }
                }
            }
        }
        catch
        {
            // Ignore TMP reflection mismatches.
        }
    }

    private void ApplyDebugVisibilityDefaults()
    {
        const float minScale = 0.12f;

        // Ensure TMP linkage is resolved even if Awake didn't run yet (edge cases).
        if (_tmpDirect == null)
            TryResolveTmpDirect();
        if (_tmpDirect == null && _tmpText == null)
            TryResolveTmp();

        if (_tmpDirect != null)
        {
            try
            {
                _tmpDirect.fontSize = Mathf.Max(_tmpDirect.fontSize, 72f);
                _tmpDirect.textWrappingMode = TextWrappingModes.NoWrap;
                var c = _tmpDirect.color;
                if (c.a <= 0.01f) c = Color.red;
                c.a = 1f;
                _tmpDirect.color = c;
                _tmpDirectBaseColor = Color.red;
            }
            catch { }
        }
        else if (_tmpText != null)
        {
            // TMP: force readability.
            try
            {
                if (_tmpColorProp != null)
                    _tmpColorProp.SetValue(_tmpText, Color.red);

                if (_tmpFontSizeProp != null)
                    _tmpFontSizeProp.SetValue(_tmpText, 72f);

                if (_tmpEnableAutoSizingProp != null)
                    _tmpEnableAutoSizingProp.SetValue(_tmpText, false);
            }
            catch
            {
                // Ignore TMP reflection mismatches.
            }

            _tmpBaseColor = Color.red;
        }
        else
        {
            // TextMesh: force readability.
            if (_textMesh == null)
                _textMesh = GetComponent<TextMesh>();

            if (_textMesh != null)
            {
                _textMesh.color = Color.red;
                _textMesh.fontSize = 96;
                _textMesh.characterSize = 0.25f;
                _textMesh.anchor = TextAnchor.MiddleCenter;
                _textMesh.alignment = TextAlignment.Center;
                _meshBaseColor = _textMesh.color;
            }
        }

        // Critical: never let scale get too small.
        if (transform.localScale.x < minScale)
            transform.localScale = Vector3.one * minScale;

        if (transform.lossyScale.x < 0.005f)
            transform.localScale = Vector3.one * minScale;
    }

    private void NudgeTowardCameraOnce()
    {
        // TEMP: helps if the text spawns behind world geometry.
        // Only runs when DebugForceVisible is enabled.
        var cam = WorldUiRoot.GetCamera();
        if (cam == null)
            return;

        var toCam = cam.transform.position - transform.position;
        if (toCam.sqrMagnitude < 0.0001f)
            return;

        const float nudgeDistance = 0.15f;
        transform.position += toCam.normalized * nudgeDistance;
    }
}
