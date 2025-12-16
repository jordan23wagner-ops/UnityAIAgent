#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class LegacyHudCanvasAutoHide
{
    private static PlayModeStateChange _lastLoggedState = (PlayModeStateChange)(-1);

    static LegacyHudCanvasAutoHide()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode && state != PlayModeStateChange.ExitingPlayMode)
            return;

        int changed = 0;

        // 1) Exact-name legacy object "HUDCanvas" (including inactive)
        var allGos = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < allGos.Length; i++)
        {
            var go = allGos[i];
            if (go == null)
                continue;

            if (EditorUtility.IsPersistent(go))
                continue; // ignore assets/prefabs in project

            if (!go.scene.IsValid())
                continue;

            if (go.name == "Abyss_HUDCanvas" || go.name.StartsWith("Abyss_HUDCanvas", StringComparison.Ordinal))
                continue;

            if (go.name == "HUDCanvas" && go.activeSelf)
            {
                go.SetActive(false);
                changed++;
            }
        }

        // 2) Any Canvas whose name contains "HUDCanvas" (excluding Abyss_HUDCanvas)
        var allCanvases = Resources.FindObjectsOfTypeAll<Canvas>();
        for (int i = 0; i < allCanvases.Length; i++)
        {
            var c = allCanvases[i];
            if (c == null)
                continue;

            if (EditorUtility.IsPersistent(c))
                continue;

            var go = c.gameObject;
            if (!go.scene.IsValid())
                continue;

            if (go.name == "Abyss_HUDCanvas" || go.name.StartsWith("Abyss_HUDCanvas", StringComparison.Ordinal))
                continue;

            if (go.name.IndexOf("HUDCanvas", StringComparison.OrdinalIgnoreCase) >= 0 && go.activeSelf)
            {
                go.SetActive(false);
                changed++;
            }
        }

        if (changed > 0 && _lastLoggedState != state)
        {
            _lastLoggedState = state;
            Debug.Log("[UI_CLEANUP] Hid legacy HUDCanvas in edit mode.");
        }
    }
}
#endif
