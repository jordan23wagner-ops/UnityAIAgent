using UnityEngine;

[ExecuteAlways]
public class LegacyHudCanvasHider : MonoBehaviour
{
    private void OnEnable()
    {
        HideIfInEditMode();
    }

    private void Update()
    {
        HideIfInEditMode();
    }

    private void HideIfInEditMode()
    {
        if (Application.isPlaying)
            return;

        // Fast path (active objects only)
        var legacy = GameObject.Find("HUDCanvas");
        if (legacy != null && legacy.activeSelf)
        {
            legacy.SetActive(false);
            return;
        }

        // Robust fallback: include inactive scene objects too.
        var all = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < all.Length; i++)
        {
            var go = all[i];
            if (go == null)
                continue;

            if (go.name != "HUDCanvas")
                continue;

            if (!go.scene.IsValid())
                continue;

            if (go.activeSelf)
                go.SetActive(false);

            break;
        }
    }
}
