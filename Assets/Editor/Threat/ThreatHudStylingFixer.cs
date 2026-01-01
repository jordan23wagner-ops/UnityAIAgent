#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Abyssbound.Threat;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Abyssbound.Threat.Editor
{
    public static class ThreatHudStylingFixer
    {
        private static readonly Color32 DistanceTextColor = new Color32(230, 230, 230, 255); // #E6E6E6
        private static readonly Color32 StatsLabelGold = new Color32(242, 201, 76, 255); // #F2C94C
        private static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.60f);
        private static readonly Vector2 ShadowDistance = new Vector2(1f, -1f);

        [MenuItem("Tools/Threat/Fix Threat HUD Text Styling")]
        public static void FixThreatHudTextStyling()
        {
            var scenesMadeDirty = new HashSet<Scene>();

            int hudsFound = 0;
            int distanceTextsUpdated = 0;
            int farthestTextsUpdated = 0;
            int disabledDistanceContainerImages = 0;
            int statsLabelsUpdated = 0;

            var updatedObjectNames = new List<string>(64);

            var loadedScenes = new HashSet<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.IsValid() && s.isLoaded)
                    loadedScenes.Add(s);
            }

            // ThreatHUD fixes
            ThreatHUD[] huds;
            try
            {
                huds = Resources.FindObjectsOfTypeAll<ThreatHUD>();
            }
            catch
            {
                huds = Array.Empty<ThreatHUD>();
            }

            for (int i = 0; i < huds.Length; i++)
            {
                var hud = huds[i];
                if (hud == null || hud.gameObject == null)
                    continue;

                var scene = hud.gameObject.scene;
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                if (!loadedScenes.Contains(scene))
                    continue;

                hudsFound++;

                var dist = FindTmpUnderHud(hud.transform, "DistanceText");
                if (dist != null)
                {
                    ApplyDistanceTmpStyle(dist, anchoredPosition: new Vector2(0f, -42f));
                    distanceTextsUpdated++;
                    updatedObjectNames.Add(GetFullPath(dist.transform));
                    scenesMadeDirty.Add(scene);
                }

                var far = FindTmpUnderHud(hud.transform, "FarthestDistanceText");
                if (far != null)
                {
                    ApplyDistanceTmpStyle(far, anchoredPosition: new Vector2(0f, -62f));
                    farthestTextsUpdated++;
                    updatedObjectNames.Add(GetFullPath(far.transform));
                    scenesMadeDirty.Add(scene);
                }

                var distanceContainer = hud.transform.Find("DistanceContainer");
                if (distanceContainer != null)
                {
                    var img = distanceContainer.GetComponent<Image>();
                    if (img != null && img.enabled)
                    {
                        img.enabled = false;
                        img.raycastTarget = false;
                        disabledDistanceContainerImages++;
                        updatedObjectNames.Add(GetFullPath(distanceContainer));
                        scenesMadeDirty.Add(scene);
                    }
                }
            }

            // Stats panel color restore (top-right)
            TextMeshProUGUI[] allTmp;
            try
            {
                allTmp = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
            }
            catch
            {
                allTmp = Array.Empty<TextMeshProUGUI>();
            }

            for (int i = 0; i < allTmp.Length; i++)
            {
                var tmp = allTmp[i];
                if (tmp == null || tmp.gameObject == null)
                    continue;

                var scene = tmp.gameObject.scene;
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                if (!loadedScenes.Contains(scene))
                    continue;

                if (!LooksLikeTopRightStatsLabel(tmp))
                    continue;

                if (tmp.color.r == StatsLabelGold.r / 255f && tmp.color.g == StatsLabelGold.g / 255f && tmp.color.b == StatsLabelGold.b / 255f && tmp.color.a == 1f)
                    continue;

                tmp.color = StatsLabelGold;
                statsLabelsUpdated++;
                updatedObjectNames.Add(GetFullPath(tmp.transform));
                scenesMadeDirty.Add(scene);
            }

            foreach (var s in scenesMadeDirty)
            {
                try { EditorSceneManager.MarkSceneDirty(s); } catch { }
            }

            string updatedList = updatedObjectNames.Count == 0
                ? "(none)"
                : string.Join(", ", updatedObjectNames);

            Debug.Log($"[ThreatHudStylingFixer] HUDsFound={hudsFound} DistanceUpdated={distanceTextsUpdated} FarthestUpdated={farthestTextsUpdated} DisabledDistanceContainerImages={disabledDistanceContainerImages} StatsLabelsUpdated={statsLabelsUpdated} Updated={updatedList}");
        }

        private static TextMeshProUGUI FindTmpUnderHud(Transform hudRoot, string objectName)
        {
            if (hudRoot == null)
                return null;

            Transform t = null;
            try
            {
                t = hudRoot.Find(objectName) ?? hudRoot.Find("DistanceContainer/" + objectName);
            }
            catch { t = null; }

            if (t != null)
                return t.GetComponent<TextMeshProUGUI>();

            // Fallback: scan children by name.
            try
            {
                var all = hudRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    var tmp = all[i];
                    if (tmp == null || tmp.gameObject == null) continue;
                    if (string.Equals(tmp.gameObject.name, objectName, StringComparison.Ordinal))
                        return tmp;
                }
            }
            catch { }

            return null;
        }

        private static void ApplyDistanceTmpStyle(TextMeshProUGUI tmp, Vector2 anchoredPosition)
        {
            if (tmp == null)
                return;

            var rt = tmp.rectTransform;
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(260f, 18f);
                rt.anchoredPosition = anchoredPosition;
            }

            tmp.fontSize = 14f;
            tmp.color = DistanceTextColor;

            try { tmp.textWrappingMode = TextWrappingModes.NoWrap; } catch { }
            try { tmp.alignment = TextAlignmentOptions.Center; } catch { }
            try { tmp.raycastTarget = false; } catch { }

            // Prefer Outline if present; else Shadow.
            var outline = tmp.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = ShadowColor;
                outline.effectDistance = ShadowDistance;
                outline.useGraphicAlpha = true;
                outline.enabled = true;
                return;
            }

            var shadow = tmp.GetComponent<Shadow>();
            if (shadow == null)
                shadow = tmp.gameObject.AddComponent<Shadow>();

            shadow.effectColor = ShadowColor;
            shadow.effectDistance = ShadowDistance;
            shadow.useGraphicAlpha = true;
            shadow.enabled = true;
        }

        private static bool LooksLikeTopRightStatsLabel(TextMeshProUGUI tmp)
        {
            if (tmp == null)
                return false;

            // Restrict to top-right anchored UI elements.
            var rt = tmp.rectTransform;
            if (rt == null)
                return false;

            bool topRightAnchored = rt.anchorMin.x >= 0.90f && rt.anchorMax.x >= 0.90f && rt.anchorMin.y >= 0.80f && rt.anchorMax.y >= 0.80f;
            if (!topRightAnchored)
                return false;

            string text;
            try { text = tmp.text; }
            catch { return false; }

            if (string.IsNullOrWhiteSpace(text))
                return false;

            text = text.Trim();

            // Likely labels: DMG, HP, DEF, etc. Avoid touching numeric values.
            if (Regex.IsMatch(text, @"\d"))
                return false;

            string upper = text.ToUpperInvariant();
            return upper.Contains("DMG") || upper == "HP" || upper.Contains("DEF") || upper.Contains("ATK") || upper.Contains("ARM") || upper.Contains("SPD") || upper.Contains("CRIT") || upper.Contains("DPS");
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
