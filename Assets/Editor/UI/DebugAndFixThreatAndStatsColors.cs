#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abyssbound.Editor.UI
{
    public static class DebugAndFixThreatAndStatsColors
    {
        private static readonly string[] NameTokens =
        {
            "DMG", "HP", "DR", "Skull", "Threat", "Distance", "Farthest", "Stats"
        };

        private static readonly Color32 ForcedDistanceColor = new Color32(220, 220, 220, 255);
        private static readonly Color32 ForcedStatsGold = new Color32(242, 201, 76, 255);
        private static readonly Color32 FixTextColor = new Color32(30, 30, 30, 255);

        [MenuItem("Tools/UI/Debug UI Text (Threat + Stats)")]
        public static void DebugUiText()
        {
            var tmps = FindAllSceneText(includeDontDestroyOnLoad: true);

            var sb = new StringBuilder(16 * 1024);
            int matchCount = 0;
            int dmgHpTextMatches = 0;

            sb.AppendLine($"[UI Debug] TextMeshProUGUI in scenes (incl DontDestroyOnLoad): {tmps.Count}");

            for (int i = 0; i < tmps.Count; i++)
            {
                var tmp = tmps[i];
                if (tmp == null || tmp.gameObject == null)
                    continue;

                string name;
                try { name = tmp.gameObject.name; }
                catch { continue; }

                if (!NameContainsAnyToken(name))
                    continue;

                matchCount++;

                Color c;
                try { c = tmp.color; }
                catch { c = Color.magenta; }

                bool active;
                try { active = tmp.gameObject.activeInHierarchy; }
                catch { active = false; }

                sb.Append("- ");
                sb.Append(GetFullPath(tmp.transform));
                sb.Append(" | Color RGBA(");
                sb.Append(Mathf.RoundToInt(c.r * 255f)); sb.Append(',');
                sb.Append(Mathf.RoundToInt(c.g * 255f)); sb.Append(',');
                sb.Append(Mathf.RoundToInt(c.b * 255f)); sb.Append(',');
                sb.Append(Mathf.RoundToInt(c.a * 255f)); sb.Append(")");
                sb.Append(" | activeInHierarchy=");
                sb.Append(active ? "true" : "false");
                sb.AppendLine();
            }

            // Also print any TMP whose TEXT contains DMG: or HP:
            for (int i = 0; i < tmps.Count; i++)
            {
                var tmp = tmps[i];
                if (tmp == null || tmp.gameObject == null)
                    continue;

                string text;
                try { text = tmp.text ?? string.Empty; } catch { text = string.Empty; }
                if (text.IndexOf("DMG:", StringComparison.OrdinalIgnoreCase) < 0 && text.IndexOf("HP:", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                dmgHpTextMatches++;
                sb.Append("- [DMG/HP TEXT] ");
                sb.Append(GetFullPath(tmp.transform));
                sb.Append(" | text='");
                sb.Append(text.Replace("\n", "\\n"));
                sb.AppendLine("'");
            }

            sb.AppendLine($"[UI Debug] Matches: {matchCount}");
            sb.AppendLine($"[UI Debug] DMG:/HP: text matches: {dmgHpTextMatches}");
            Debug.Log(sb.ToString());
        }

        [MenuItem("Tools/UI/Fix Stats & Threat Text Colors")]
        public static void FixStatsAndThreatTextColors()
        {
            var tmps = FindAllSceneText(includeDontDestroyOnLoad: true);

            int totalFound = tmps.Count;
            int changedThreatDistance = 0;
            int changedStats = 0;

            var changedPaths = new List<string>(128);
            var scenesMadeDirty = new HashSet<Scene>();

            for (int i = 0; i < tmps.Count; i++)
            {
                var tmp = tmps[i];
                if (tmp == null || tmp.gameObject == null)
                    continue;

                string name;
                try { name = tmp.gameObject.name; } catch { continue; }

                // ThreatHUD distance texts (by exact name).
                if (string.Equals(name, "DistanceText", StringComparison.Ordinal) || string.Equals(name, "FarthestDistanceText", StringComparison.Ordinal))
                {
                    if (SetColor32(tmp, FixTextColor))
                    {
                        changedThreatDistance++;
                        changedPaths.Add(GetFullPath(tmp.transform));
                        scenesMadeDirty.Add(tmp.gameObject.scene);
                    }
                    continue;
                }

                // Stats panel texts: only those that actually display DMG:/HP:/DR: lines.
                string text;
                try { text = tmp.text ?? string.Empty; } catch { text = string.Empty; }

                bool looksLikeStatsLine =
                    text.IndexOf("DMG:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.IndexOf("HP:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.IndexOf("DR:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    string.Equals(name, "Stats_DMG", StringComparison.Ordinal) ||
                    string.Equals(name, "Stats_HP", StringComparison.Ordinal) ||
                    string.Equals(name, "Stats_DR", StringComparison.Ordinal);

                if (!looksLikeStatsLine)
                    continue;

                if (SetColor32(tmp, FixTextColor))
                {
                    changedStats++;
                    changedPaths.Add(GetFullPath(tmp.transform));
                    scenesMadeDirty.Add(tmp.gameObject.scene);
                }
            }

            foreach (var scene in scenesMadeDirty)
            {
                try { EditorSceneManager.MarkSceneDirty(scene); } catch { }
            }

            var sb = new StringBuilder(4 * 1024);
            sb.Append("[UI Fix] ");
            sb.Append("TMP found=").Append(totalFound);
            sb.Append(" | changed threat distance=").Append(changedThreatDistance);
            sb.Append(" | changed stats=").Append(changedStats);
            sb.Append(" | changed paths=").Append(changedPaths.Count);

            if (changedPaths.Count > 0)
            {
                sb.AppendLine();
                for (int i = 0; i < changedPaths.Count; i++)
                {
                    sb.Append("- ");
                    sb.AppendLine(changedPaths[i]);
                }
            }

            Debug.Log(sb.ToString());
        }

        [MenuItem("Tools/UI/Fix Threat & Stats Colors (Forced)")]
        public static void FixThreatAndStatsColorsForced()
        {
            var tmps = FindAllSceneText(includeDontDestroyOnLoad: true);

            int totalFound = tmps.Count;
            int changedDistance = 0;
            int changedStats = 0;

            var changedPaths = new List<string>(128);
            var scenesMadeDirty = new HashSet<Scene>();

            // First pass: apply direct rules.
            for (int i = 0; i < tmps.Count; i++)
            {
                var tmp = tmps[i];
                if (tmp == null || tmp.gameObject == null)
                    continue;

                string name;
                try { name = tmp.gameObject.name; }
                catch { continue; }

                string text;
                try { text = tmp.text ?? string.Empty; }
                catch { text = string.Empty; }

                bool changed = false;

                if (ContainsIgnoreCase(name, "Distance"))
                {
                    changed |= SetColor32(tmp, ForcedDistanceColor);
                    if (changed)
                    {
                        changedDistance++;
                        changedPaths.Add(GetFullPath(tmp.transform));
                        scenesMadeDirty.Add(tmp.gameObject.scene);
                    }

                    // Also force alpha.
                    ForceAlpha255(tmp);
                    continue;
                }

                bool isDmg = ContainsIgnoreCase(name, "DMG") || ContainsIgnoreCase(text, "DMG");
                bool isHp = ContainsIgnoreCase(name, "HP") || ContainsIgnoreCase(text, "HP");
                bool isDr = ContainsIgnoreCase(name, "DR") || ContainsIgnoreCase(text, "DR");

                if (isDmg || isHp || isDr)
                {
                    changed |= SetColor32(tmp, ForcedStatsGold);
                    ForceAlpha255(tmp);

                    if (changed)
                    {
                        changedStats++;
                        changedPaths.Add(GetFullPath(tmp.transform));
                        scenesMadeDirty.Add(tmp.gameObject.scene);
                    }

                    // Apply to any stats list items under the same parent.
                    try
                    {
                        var parent = tmp.transform.parent;
                        if (parent != null)
                        {
                            var siblings = parent.GetComponentsInChildren<TextMeshProUGUI>(true);
                            for (int s = 0; s < siblings.Length; s++)
                            {
                                var sib = siblings[s];
                                if (sib == null || sib.gameObject == null)
                                    continue;

                                // Avoid double-counting the original.
                                if (sib == tmp)
                                    continue;

                                bool sibChanged = SetColor32(sib, ForcedStatsGold);
                                ForceAlpha255(sib);

                                if (sibChanged)
                                {
                                    changedStats++;
                                    changedPaths.Add(GetFullPath(sib.transform));
                                    scenesMadeDirty.Add(sib.gameObject.scene);
                                }
                            }
                        }
                    }
                    catch { }
                }
            }

            foreach (var scene in scenesMadeDirty)
            {
                try { EditorSceneManager.MarkSceneDirty(scene); } catch { }
            }

            // Log summary.
            var sb = new StringBuilder(8 * 1024);
            sb.Append("[UI Fix Forced] ");
            sb.Append("TMP found="); sb.Append(totalFound);
            sb.Append(" | changed distance="); sb.Append(changedDistance);
            sb.Append(" | changed stats="); sb.Append(changedStats);
            sb.Append(" | changed paths="); sb.Append(changedPaths.Count);

            if (changedPaths.Count > 0)
            {
                sb.AppendLine();
                for (int i = 0; i < changedPaths.Count; i++)
                {
                    sb.Append("- ");
                    sb.AppendLine(changedPaths[i]);
                }
            }

            Debug.Log(sb.ToString());
        }

        private static List<TextMeshProUGUI> FindAllSceneText(bool includeDontDestroyOnLoad)
        {
            var results = new List<TextMeshProUGUI>(512);

            TextMeshProUGUI[] all;
            try { all = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>(); }
            catch { all = Array.Empty<TextMeshProUGUI>(); }

            for (int i = 0; i < all.Length; i++)
            {
                var tmp = all[i];
                if (tmp == null || tmp.gameObject == null)
                    continue;

                var scene = tmp.gameObject.scene;
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                if (!includeDontDestroyOnLoad)
                {
                    // Basic guard: in practice this still includes the active scene(s).
                    // We keep this switch in case callers want to exclude the DontDestroy scene.
                    try
                    {
                        if (string.Equals(scene.name, "DontDestroyOnLoad", StringComparison.Ordinal))
                            continue;
                    }
                    catch { }
                }

                // Skip prefab assets, etc.
                try
                {
                    if (!tmp.gameObject.scene.IsValid())
                        continue;
                }
                catch { continue; }

                results.Add(tmp);
            }

            return results;
        }

        private static bool NameContainsAnyToken(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            for (int i = 0; i < NameTokens.Length; i++)
            {
                if (ContainsIgnoreCase(name, NameTokens[i]))
                    return true;
            }

            return false;
        }

        private static bool ContainsIgnoreCase(string haystack, string needle)
        {
            if (haystack == null || needle == null)
                return false;
            return haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool SetColor32(TextMeshProUGUI tmp, Color32 c)
        {
            if (tmp == null)
                return false;

            Color prev;
            try { prev = tmp.color; }
            catch { prev = Color.magenta; }

            tmp.color = c;

            var prev32 = (Color32)prev;
            return prev32.r != c.r || prev32.g != c.g || prev32.b != c.b || prev32.a != c.a;
        }

        private static void ForceAlpha255(TextMeshProUGUI tmp)
        {
            if (tmp == null)
                return;

            try
            {
                var c = (Color32)tmp.color;
                if (c.a != 255)
                {
                    c.a = 255;
                    tmp.color = c;
                }
            }
            catch { }
        }

        private static string GetFullPath(Transform t)
        {
            if (t == null)
                return "(null)";

            try
            {
                var parts = new List<string>(16);
                var cur = t;
                while (cur != null)
                {
                    parts.Add(cur.name);
                    cur = cur.parent;
                }
                parts.Reverse();
                return string.Join("/", parts);
            }
            catch
            {
                return t.name;
            }
        }
    }
}
#endif
