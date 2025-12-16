using System;
using UnityEngine;
using UnityEngine.UI;

public static class WorldUiRoot
{
    private const string RootName = "Abyss_RuntimeWorldUI";
    private const string LegacyRootName = "WorldUI";
    private const string CanvasChildName = "Abyss_RuntimeWorldUI_Canvas";
    private const string WorldTextRootName = "Abyss_WorldTextRoot";

    private static Transform _root;
    private static Transform _canvasRoot;
    private static Canvas _canvas;
    private static Camera _cachedCamera;

    private static Transform _worldTextRoot;
    private static bool _loggedWorldTextRootCreated;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetBeforeSceneLoad()
    {
        // Domain reload may be disabled; clear static UnityEngine.Object refs
        // so we never touch stale ("Missing") objects during early OnEnable calls.
        ResetForPlaymode();
    }

    public static void ResetForPlaymode()
    {
        _root = null;
        _canvasRoot = null;
        _canvas = null;
        _cachedCamera = null;
        _worldTextRoot = null;
    }

    public static Transform GetOrCreateWorldTextRoot()
    {
        ClearStaleRefs();

        if (_worldTextRoot)
            return _worldTextRoot;

        GameObject existing = null;
        try
        {
            existing = GameObject.Find(WorldTextRootName);
        }
        catch { }

        if (existing == null)
        {
            existing = new GameObject(WorldTextRootName);
            if (Application.isPlaying)
                UnityEngine.Object.DontDestroyOnLoad(existing);

            if (!_loggedWorldTextRootCreated)
            {
                _loggedWorldTextRootCreated = true;
                Debug.Log("[WorldUiRoot] Created Abyss_WorldTextRoot.");
            }
        }

        _worldTextRoot = existing.transform;

        // Keep this root neutral so children keep their intended world-space transforms.
        _worldTextRoot.localPosition = Vector3.zero;
        _worldTextRoot.localRotation = Quaternion.identity;
        _worldTextRoot.localScale = Vector3.one;

        return _worldTextRoot;
    }

    public static Transform GetOrCreateRoot()
    {
        ClearStaleRefs();

        if (_root)
            return _root;

        var existing = GameObject.Find(RootName);
        // Back-compat: older scenes/code may have created "WorldUI".
        // Prefer our Abyss root; only fall back to legacy if it exists and our root does not.
        if (existing == null)
            existing = GameObject.Find(LegacyRootName);

        if (existing != null)
        {
            try
            {
                _root = existing.transform;
                EnsureCanvasExists();
                return _root;
            }
            catch (MissingReferenceException)
            {
                ResetAfterMissingOnce("GetOrCreateRoot(existing)");
                return null;
            }
            catch (Exception)
            {
                ResetAfterMissingOnce("GetOrCreateRoot(existing)");
                return null;
            }
        }

        try
        {
            var go = new GameObject(RootName);
            _root = go.transform;

            // Optional persistence for "one UIRuntimeRoot" across scene reloads.
            UnityEngine.Object.DontDestroyOnLoad(go);

            EnsureCanvasExists();
            return _root;
        }
        catch (MissingReferenceException)
        {
            ResetAfterMissingOnce("GetOrCreateRoot(create)");
            return null;
        }
        catch (Exception)
        {
            ResetAfterMissingOnce("GetOrCreateRoot(create)");
            return null;
        }
    }

    public static Transform GetOrCreateCanvasRoot()
    {
        try
        {
            GetOrCreateRoot();
            EnsureCanvasExists();
            return _canvasRoot;
        }
        catch (MissingReferenceException)
        {
            ResetAfterMissingOnce("GetOrCreateCanvasRoot");
            return null;
        }
        catch (Exception)
        {
            ResetAfterMissingOnce("GetOrCreateCanvasRoot");
            return null;
        }
    }

    public static Camera GetCamera()
    {
        ClearStaleRefs();

        if (_cachedCamera != null && _cachedCamera.enabled && _cachedCamera.gameObject.activeInHierarchy)
            return _cachedCamera;

        var cam = Camera.main;
        if (cam != null)
        {
            _cachedCamera = cam;
            return cam;
        }

        // Fallback (e.g., camera not tagged MainCamera)
        var cams = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        for (int i = 0; i < cams.Length; i++)
        {
            if (cams[i] != null && cams[i].enabled && cams[i].gameObject.activeInHierarchy)
            {
                _cachedCamera = cams[i];
                return cams[i];
            }
        }

        return null;
    }

    private static void ConfigureCanvas(Canvas canvas)
    {
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = GetCamera();
        canvas.sortingOrder = 500;

        var rt = canvas.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200f, 200f);
        rt.localScale = Vector3.one;
    }

    private static void EnsureAuxComponents(GameObject root)
    {
        if (root.GetComponent<GraphicRaycaster>() == null)
            root.AddComponent<GraphicRaycaster>();

        if (root.GetComponent<CanvasScaler>() == null)
        {
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        }
    }

    private static void EnsureCanvasExists()
    {
        ClearStaleRefs();

        if (_root == null)
            return;

        if (!_root)
            return;

        try
        {
            // Transform-touching parts only.

            if (_canvasRoot == null)
            {
                // Re-validate root before touching transform operations.
                if (_root == null || !_root)
                    return;

                var child = _root.Find(CanvasChildName);
                if (child == null)
                {
                    var go = new GameObject(CanvasChildName);
                    go.transform.SetParent(_root, false);
                    child = go.transform;
                }

                _canvasRoot = child;
            }

            if (_canvasRoot == null || !_canvasRoot)
            {
                _canvasRoot = null;
                _canvas = null;
                return;
            }

            if (_canvas == null)
            {
                _canvas = _canvasRoot.GetComponent<Canvas>();
                if (_canvas == null)
                    _canvas = _canvasRoot.gameObject.AddComponent<Canvas>();

                ConfigureCanvas(_canvas);
                EnsureAuxComponents(_canvasRoot.gameObject);
            }
            else
            {
                // Keep camera binding fresh across scene loads.
                _canvas.worldCamera = GetCamera();
            }
        }
        catch (MissingReferenceException)
        {
            ResetAfterMissingOnce("EnsureCanvasExists(MissingReferenceException)");
            return;
        }
        catch (Exception)
        {
            ResetAfterMissingOnce("EnsureCanvasExists(Exception)");
            return;
        }
    }

    private static void ResetAfterMissingOnce(string context)
    {
        ResetForPlaymode();
    }

    private static void ClearStaleRefs()
    {
        // When Enter Play Mode Options disables domain reload, static UnityEngine.Object refs
        // can persist across play sessions as "Missing" and throw MissingReferenceException
        // when accessed. Treat invalid refs as null and recreate on demand.
        if (_root != null && !_root) _root = null;
        if (_canvasRoot != null && !_canvasRoot) _canvasRoot = null;
        if (_canvas != null && !_canvas) _canvas = null;
        if (_cachedCamera != null && !_cachedCamera) _cachedCamera = null;
        if (_worldTextRoot != null && !_worldTextRoot) _worldTextRoot = null;
    }
}
