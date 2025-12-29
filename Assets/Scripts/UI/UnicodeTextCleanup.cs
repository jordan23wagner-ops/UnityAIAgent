using TMPro;
using UnityEngine;

namespace Abyssbound.UI
{
    /// <summary>
    /// One-time cleanup for legacy TMP glyph texts (e.g. U+2620) that can spam font warnings.
    /// Intended to be added to a persistent bootstrap/root object.
    /// </summary>
    public sealed class UnicodeTextCleanup : MonoBehaviour
    {
        private static bool s_ran;

        private void Awake()
        {
            if (s_ran)
                return;

            s_ran = true;
            RunOnce();
        }

        private static void RunOnce()
        {
            const char Skull = '\u2620';

            int changedCount = 0;

            TextMeshProUGUI[] all;
            try
            {
                all = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
            }
            catch
            {
                return;
            }

            if (all == null || all.Length == 0)
                return;

            for (int i = 0; i < all.Length; i++)
            {
                var tmp = all[i];
                if (tmp == null)
                    continue;

                try
                {
                    if (tmp.gameObject == null)
                        continue;

                    // Filter out assets/prefabs not in a scene.
                    if (!tmp.gameObject.scene.IsValid())
                        continue;
                }
                catch
                {
                    continue;
                }

                string text;
                try { text = tmp.text; }
                catch { continue; }

                if (string.IsNullOrEmpty(text) || text.IndexOf(Skull) < 0)
                    continue;

                changedCount++;

                try { tmp.text = string.Empty; } catch { }
                try { tmp.enabled = false; } catch { }

                try
                {
                    var name = tmp.gameObject.name;
                    if (!string.IsNullOrEmpty(name) && name.ToLowerInvariant().Contains("skulltext"))
                        tmp.gameObject.SetActive(false);
                }
                catch { }
            }

            if (changedCount > 0)
            {
                try
                {
                    Debug.Log($"[UnicodeTextCleanup] Disabled legacy TMP skull glyph texts to prevent font warnings. Count={changedCount}");
                }
                catch { }
            }
        }
    }
}
