#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Abyssbound.Threat.Editor
{
    public static class ThreatSetupMenu
    {
        [MenuItem("Tools/Threat/Setup Threat HUD (One-Click)")]
        public static void Setup()
        {
            EnsureFolders();

            // Ensure deterministic skull sprite exists.
            var skullSprite = ThreatSkullSpriteGenerator.EnsureSkullSpriteAsset(forceRegenerate: false);

            var service = EnsureThreatService();
            var canvas = FindHudCanvas();
            var hud = EnsureThreatHud(canvas, skullSprite);

            // Temporary: move the main player health bar to bottom-center for readability.
            TryMovePlayerHealthBarToBottom(canvas);

            try
            {
                if (hud != null)
                    hud.gameObject.name = "ThreatHUD";
            }
            catch { }

            MarkDirty();

            Debug.Log($"[Threat] Setup complete. Service={(service != null ? "OK" : "MISSING")} Canvas={(canvas != null ? canvas.name : "NONE")} HUD={(hud != null ? "OK" : "MISSING")} SkullSprite={(skullSprite != null ? "OK" : "MISSING")}");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "Scripts");
            EnsureFolder("Assets/Scripts", "Threat");
            EnsureFolder("Assets/Scripts/Threat", "Editor");
            EnsureFolder("Assets", "Art");
            EnsureFolder("Assets/Art", "UI");
            EnsureFolder("Assets/Art/UI", "Threat");
        }

        private static void EnsureFolder(string parent, string child)
        {
            try
            {
                string path = parent.EndsWith("/") ? parent + child : parent + "/" + child;
                if (AssetDatabase.IsValidFolder(path))
                    return;

                if (!AssetDatabase.IsValidFolder(parent))
                    return;

                AssetDatabase.CreateFolder(parent, child);
            }
            catch { }
        }

        private static ThreatService EnsureThreatService()
        {
            ThreatService existing = null;
            try
            {
                var all = UnityEngine.Object.FindObjectsByType<ThreatService>(FindObjectsSortMode.None);
                if (all != null && all.Length > 0)
                    existing = all[0];
            }
            catch { existing = null; }

            if (existing != null)
                return existing;

            var go = new GameObject("[ThreatService]");
            Undo.RegisterCreatedObjectUndo(go, "Create ThreatService");
            return go.AddComponent<ThreatService>();
        }

        private static Canvas FindHudCanvas()
        {
            // Preferred: named HUD canvas.
            try
            {
                var go = GameObject.Find("Abyss_HUDCanvas");
                if (go != null)
                {
                    var c = go.GetComponentInChildren<Canvas>(true);
                    if (c != null) return c;
                }
            }
            catch { }

            // Heuristic: any canvas with HUD in name.
            try
            {
                var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
                if (canvases != null)
                {
                    for (int i = 0; i < canvases.Length; i++)
                    {
                        var c = canvases[i];
                        if (c == null) continue;
                        var n = c.gameObject.name;
                        if (n.IndexOf("HUD", StringComparison.OrdinalIgnoreCase) >= 0)
                            return c;
                    }

                    if (canvases.Length > 0)
                        return canvases[0];
                }
            }
            catch { }

            return null;
        }

        private static ThreatHUD EnsureThreatHud(Canvas canvas, Sprite skullSprite)
        {
            if (canvas == null)
            {
                Debug.LogWarning("[Threat] No Canvas found; cannot create ThreatHUD.");
                return null;
            }

            // Find existing.
            try
            {
                var existing = canvas.GetComponentInChildren<ThreatHUD>(true);
                if (existing != null)
                {
                    TryDisableLegacySkullText(existing.transform);
                    ApplyLayoutAndSprite(existing, skullSprite);
                    return existing;
                }
            }
            catch { }

            var root = canvas.transform;

            var panelGo = new GameObject(
                "ThreatHUD",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(ThreatHUD));

            Undo.RegisterCreatedObjectUndo(panelGo, "Create ThreatHUD");
            panelGo.transform.SetParent(root, worldPositionStays: false);

            var panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 1f);
            panelRt.anchorMax = new Vector2(0.5f, 1f);
            panelRt.pivot = new Vector2(0.5f, 1f);
            panelRt.anchoredPosition = new Vector2(0f, -10f);
            panelRt.sizeDelta = new Vector2(320f, 74f);

            var panelImg = panelGo.GetComponent<Image>();
            // ThreatHUD will ensure it has a 1x1 sprite at runtime.
            panelImg.color = new Color(0f, 0f, 0f, 0.60f);
            panelImg.raycastTarget = false;

            // Skull row root.
            var rowGo = new GameObject("SkullRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            Undo.RegisterCreatedObjectUndo(rowGo, "Create SkullRow");
            rowGo.transform.SetParent(panelGo.transform, worldPositionStays: false);

            var rowRt = rowGo.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0.5f, 0.5f);
            rowRt.anchorMax = new Vector2(0.5f, 0.5f);
            rowRt.pivot = new Vector2(0.5f, 0.5f);
            rowRt.anchoredPosition = new Vector2(0f, 0f);
            rowRt.sizeDelta = new Vector2(300f, 56f);

            var hlg = rowGo.GetComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 10f;
            hlg.childControlHeight = false;
            hlg.childControlWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.padding = new RectOffset(14, 14, 10, 10);

            var bgs = new List<Image>(5);
            var fills = new List<Image>(5);

            for (int i = 0; i < 5; i++)
            {
                var slotGo = new GameObject($"Skull{i + 1}", typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(slotGo, "Create Skull Slot");
                slotGo.transform.SetParent(rowGo.transform, worldPositionStays: false);

                var slotRt = slotGo.GetComponent<RectTransform>();
                slotRt.sizeDelta = new Vector2(34f, 34f);

                var bgGo = new GameObject("BG", typeof(RectTransform), typeof(Image));
                Undo.RegisterCreatedObjectUndo(bgGo, "Create Skull BG");
                bgGo.transform.SetParent(slotGo.transform, worldPositionStays: false);
                var bgRt = bgGo.GetComponent<RectTransform>();
                bgRt.anchorMin = Vector2.zero;
                bgRt.anchorMax = Vector2.one;
                bgRt.offsetMin = Vector2.zero;
                bgRt.offsetMax = Vector2.zero;

                var bg = bgGo.GetComponent<Image>();
                bg.sprite = skullSprite;
                bg.color = new Color(1f, 1f, 1f, 0.30f);
                bg.raycastTarget = false;
                bg.preserveAspect = true;

                var fgGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
                Undo.RegisterCreatedObjectUndo(fgGo, "Create Skull Fill");
                fgGo.transform.SetParent(slotGo.transform, worldPositionStays: false);
                var fgRt = fgGo.GetComponent<RectTransform>();
                fgRt.anchorMin = Vector2.zero;
                fgRt.anchorMax = Vector2.one;
                fgRt.offsetMin = Vector2.zero;
                fgRt.offsetMax = Vector2.zero;

                var fg = fgGo.GetComponent<Image>();
                fg.sprite = skullSprite;
                fg.type = Image.Type.Filled;
                fg.fillMethod = Image.FillMethod.Horizontal;
                fg.fillOrigin = 0;
                fg.fillAmount = 0f;
                fg.raycastTarget = false;
                fg.preserveAspect = true;

                bgs.Add(bg);
                fills.Add(fg);
            }

            // Numeric threat value (optional, off by default).
            var valueGo = new GameObject("ThreatValue", typeof(RectTransform), typeof(TextMeshProUGUI));
            Undo.RegisterCreatedObjectUndo(valueGo, "Create Threat Value");
            valueGo.transform.SetParent(panelGo.transform, worldPositionStays: false);
            var valueRt = valueGo.GetComponent<RectTransform>();
            valueRt.anchorMin = new Vector2(0f, 0.5f);
            valueRt.anchorMax = new Vector2(0f, 0.5f);
            valueRt.pivot = new Vector2(0f, 0.5f);
            valueRt.anchoredPosition = new Vector2(10f, 0f);
            valueRt.sizeDelta = new Vector2(54f, 32f);

            var valueText = valueGo.GetComponent<TextMeshProUGUI>();
            valueText.raycastTarget = false;
            valueText.fontSize = 18f;
            valueText.alignment = TextAlignmentOptions.MidlineLeft;
            valueText.text = "0.0";
            valueGo.SetActive(false);

            var hud = panelGo.GetComponent<ThreatHUD>();
            try { hud.skullSprite = skullSprite; } catch { }
            hud.Wire(bgs.ToArray(), fills.ToArray(), valueText);

            // Draw above other HUD elements.
            try { hud.transform.SetAsLastSibling(); } catch { }

            return hud;
        }

        private static void ApplyLayoutAndSprite(ThreatHUD hud, Sprite skullSprite)
        {
            if (hud == null)
                return;

            try
            {
                var rt = hud.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.5f, 1f);
                    rt.anchorMax = new Vector2(0.5f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    rt.anchoredPosition = new Vector2(0f, -10f);
                    rt.sizeDelta = new Vector2(320f, 74f);
                }
            }
            catch { }

            try
            {
                var img = hud.GetComponent<Image>();
                if (img != null)
                {
                    img.raycastTarget = false;
                    img.color = new Color(0f, 0f, 0f, 0.60f);
                }
            }
            catch { }

            try
            {
                var row = hud.transform.Find("SkullRow");
                var hlg = row != null ? row.GetComponent<HorizontalLayoutGroup>() : null;
                if (hlg != null)
                {
                    hlg.childAlignment = TextAnchor.MiddleCenter;
                    hlg.spacing = 10f;
                    hlg.childControlHeight = false;
                    hlg.childControlWidth = false;
                    hlg.childForceExpandHeight = false;
                    hlg.childForceExpandWidth = false;
                    hlg.padding = new RectOffset(14, 14, 10, 10);
                }

                if (row != null)
                {
                    var rowRt = row.GetComponent<RectTransform>();
                    if (rowRt != null)
                    {
                        rowRt.anchorMin = new Vector2(0.5f, 0.5f);
                        rowRt.anchorMax = new Vector2(0.5f, 0.5f);
                        rowRt.pivot = new Vector2(0.5f, 0.5f);
                        rowRt.anchoredPosition = new Vector2(0f, 0f);
                        rowRt.sizeDelta = new Vector2(300f, 56f);
                    }

                    for (int i = 0; i < 5; i++)
                    {
                        var slot = row.Find($"Skull{i + 1}");
                        var slotRt = slot != null ? slot.GetComponent<RectTransform>() : null;
                        if (slotRt != null)
                            slotRt.sizeDelta = new Vector2(34f, 34f);
                    }
                }
            }
            catch { }

            try
            {
                if (skullSprite != null)
                    hud.skullSprite = skullSprite;
            }
            catch { }

            try { hud.transform.SetAsLastSibling(); } catch { }
        }

        private static void TryMovePlayerHealthBarToBottom(Canvas canvas)
        {
            if (canvas == null)
                return;

            // HudFactory creates the player bar as "Abyss_PlayerHealthBar".
            RectTransform target = null;
            try
            {
                var t = canvas.transform.Find("Abyss_PlayerHealthBar");
                if (t != null)
                    target = t as RectTransform;
            }
            catch { target = null; }

            if (target == null)
            {
                try
                {
                    var rts = canvas.GetComponentsInChildren<RectTransform>(true);
                    if (rts != null)
                    {
                        for (int i = 0; i < rts.Length; i++)
                        {
                            var rt = rts[i];
                            if (rt == null) continue;
                            if (rt == canvas.transform) continue;

                            var n = rt.gameObject.name;
                            if (n.IndexOf("Abyss_PlayerHealthBar", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                target = rt;
                                break;
                            }
                        }
                    }
                }
                catch { target = null; }
            }

            if (target == null)
                return;

            try
            {
                target.anchorMin = new Vector2(0.5f, 0f);
                target.anchorMax = new Vector2(0.5f, 0f);
                target.pivot = new Vector2(0.5f, 0f);
                target.anchoredPosition = new Vector2(0f, 10f);
            }
            catch { }
        }

        private static void TryDisableLegacySkullText(Transform hudRoot)
        {
            if (hudRoot == null)
                return;

            try
            {
                var t = hudRoot.Find("SkullText");
                if (t == null) return;

                var tmp = t.GetComponent<TMP_Text>();
                if (tmp != null) tmp.text = string.Empty;

                t.gameObject.SetActive(false);
            }
            catch { }
        }

        private static void MarkDirty()
        {
            try
            {
                var scene = SceneManager.GetActiveScene();
                if (scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(scene);
            }
            catch { }
        }
    }
}
#endif
