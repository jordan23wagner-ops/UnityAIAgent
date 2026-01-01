#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Abyssbound.Editor.UI
{
    public static class FixThreatAndStatsColors
    {
        private static readonly Color32 ThreatDistanceGray = new Color32(200, 200, 200, 255);
        private static readonly Color32 StatsLabelGold = new Color32(242, 201, 76, 255);
        private static readonly Color32 StatsValueWhite = new Color32(255, 255, 255, 255);

        private static readonly Color ShadowOrOutlineBlack70 = new Color(0f, 0f, 0f, 0.70f);

        [MenuItem("Tools/UI/Fix Threat & Stats Colors")]
        public static void Run()
        {
            var scenesMadeDirty = new HashSet<Scene>();

            var loadedScenes = new HashSet<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.IsValid() && s.isLoaded)
                    loadedScenes.Add(s);
            }

            // 1) Threat distance text readability (by object name, no RectTransform changes).
            var allTmp = FindAllSceneTmp(loadedScenes);
            for (int i = 0; i < allTmp.Count; i++)
            {
                var tmp = allTmp[i];
                if (tmp == null) continue;

                string n;
                try { n = tmp.gameObject.name; } catch { continue; }

                if (!string.Equals(n, "DistanceText", StringComparison.Ordinal) && !string.Equals(n, "FarthestDistanceText", StringComparison.Ordinal))
                    continue;

                tmp.color = ThreatDistanceGray;

                // If Outline or Shadow exists, tune it for readability (do not add new components).
                var outline = tmp.GetComponent<Outline>();
                if (outline != null)
                {
                    outline.effectColor = ShadowOrOutlineBlack70;
                    // Keep small thickness; do not make it glow.
                    if (outline.effectDistance.sqrMagnitude > (2f * 2f))
                        outline.effectDistance = new Vector2(1f, -1f);
                    outline.useGraphicAlpha = true;
                }

                var shadow = tmp.GetComponent<Shadow>();
                if (shadow != null)
                {
                    shadow.effectColor = ShadowOrOutlineBlack70;
                    if (shadow.effectDistance.sqrMagnitude > (2f * 2f))
                        shadow.effectDistance = new Vector2(1f, -1f);
                    shadow.useGraphicAlpha = true;
                }

                scenesMadeDirty.Add(tmp.gameObject.scene);
            }

            // 2) Restore Stats UI color (top-right panel).
            // Strategy: find one or more "stats containers" by spotting known label keywords in top-right anchored TMPs,
            // then recolor TMPs under those containers (labels -> gold, numeric-only -> white).
            var statsContainers = new HashSet<Transform>();

            for (int i = 0; i < allTmp.Count; i++)
            {
                var tmp = allTmp[i];
                if (tmp == null) continue;

                if (!IsTopRightAnchored(tmp.rectTransform))
                    continue;

                string text;
                try { text = tmp.text; } catch { continue; }
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                if (!LooksLikeStatsLabelKeyword(text))
                    continue;

                var container = FindReasonableStatsContainer(tmp.transform);
                if (container != null)
                    statsContainers.Add(container);
            }

            foreach (var container in statsContainers)
            {
                if (container == null) continue;

                TextMeshProUGUI[] tmps;
                try { tmps = container.GetComponentsInChildren<TextMeshProUGUI>(true); }
                catch { continue; }

                for (int i = 0; i < tmps.Length; i++)
                {
                    var tmp = tmps[i];
                    if (tmp == null || tmp.gameObject == null) continue;

                    // Only touch items that are plausibly part of the top-right stats UI.
                    if (!IsTopRightAnchored(tmp.rectTransform))
                        continue;

                    string text;
                    try { text = tmp.text; } catch { continue; }
                    if (string.IsNullOrWhiteSpace(text))
                        continue;

                    if (IsNumericOnly(text))
                        tmp.color = StatsValueWhite;
                    else
                        tmp.color = StatsLabelGold;

                    scenesMadeDirty.Add(tmp.gameObject.scene);
                }
            }

            foreach (var s in scenesMadeDirty)
            {
                try { EditorSceneManager.MarkSceneDirty(s); } catch { }
            }

            Debug.Log("Threat + Stats UI colors fixed.");
        }

        private static List<TextMeshProUGUI> FindAllSceneTmp(HashSet<Scene> loadedScenes)
        {
            var result = new List<TextMeshProUGUI>(512);

            TextMeshProUGUI[] all;
            try { all = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>(); }
            catch { all = Array.Empty<TextMeshProUGUI>(); }

            for (int i = 0; i < all.Length; i++)
            {
                var tmp = all[i];
                if (tmp == null || tmp.gameObject == null) continue;

                var scene = tmp.gameObject.scene;
                if (!scene.IsValid() || !scene.isLoaded) continue;
                if (!loadedScenes.Contains(scene)) continue;

                result.Add(tmp);
            }

            return result;
        }

        private static bool IsTopRightAnchored(RectTransform rt)
        {
            if (rt == null) return false;

            // Top-right-ish anchors.
            return rt.anchorMin.x >= 0.75f && rt.anchorMax.x >= 0.75f && rt.anchorMin.y >= 0.60f && rt.anchorMax.y >= 0.60f;
        }

        private static bool LooksLikeStatsLabelKeyword(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var t = text.Trim().ToUpperInvariant();

            // Common labels seen in the top-right stats panel.
            return t == "DMG" || t == "HP" || t == "DR" || t == "DEF" || t == "ATK" || t == "ARM" || t == "DPS" || t.Contains("CRIT") || t.Contains("SKILL");
        }

        private static Transform FindReasonableStatsContainer(Transform leaf)
        {
            // Walk up a few levels and pick the first ancestor with a RectTransform;
            // we’ll recolor only top-right anchored TMPs under it.
            var cur = leaf;
            for (int i = 0; i < 8 && cur != null; i++)
            {
                var rt = cur as RectTransform;
                if (rt != null)
                    return cur;

                cur = cur.parent;
            }

            return leaf != null ? leaf.parent : null;
        }

        private static bool IsNumericOnly(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            // Allow digits + punctuation typically used in numbers.
            // If it contains letters, it is not numeric-only.
            if (Regex.IsMatch(text, @"[A-Za-z]") )
                return false;

            return Regex.IsMatch(text.Trim(), @"^[0-9\s\+\-\.,:%/xX]+$");
        }
    }
}
#endif
