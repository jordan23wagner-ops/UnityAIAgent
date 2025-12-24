using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class InventoryUiDoctor
{
    private const string DiagnoseMenuPath = "Tools/Abyssbound/QA/Inventory UI/Diagnose (Play Mode)";
    private const string AutoFixMenuPath = "Tools/Abyssbound/QA/Inventory UI/Auto-Fix (Play Mode)";

    [MenuItem(DiagnoseMenuPath)]
    private static void DiagnoseMenu()
    {
        Run(applyFix: false);
    }

    [MenuItem(AutoFixMenuPath)]
    private static void AutoFixMenu()
    {
        Run(applyFix: true);
    }

    private static void Run(bool applyFix)
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[InventoryUiDoctor] Not in Play Mode; exiting (no asset modifications)." );
            return;
        }

        var ui = FindPlayerInventoryUi();
        if (ui == null)
        {
            Debug.LogWarning("[InventoryUiDoctor] Could not find a PlayerInventoryUI in the active scene.");
            return;
        }

        var report = new List<string>();
        report.Add($"[InventoryUiDoctor] Run via Editor menu: '{DiagnoseMenuPath}' or '{AutoFixMenuPath}'");
        report.Add($"[InventoryUiDoctor] Mode={(applyFix ? "AUTO-FIX" : "DIAGNOSE")} target='{GetHierarchyPath(ui.transform)}'");

        var rootGo = GetPrivateField<GameObject>(ui, "root");
        var scrollRect = GetPrivateField<ScrollRect>(ui, "scrollRect");
        var contentRoot = GetPrivateField<RectTransform>(ui, "contentRoot");
        var rowTemplate = GetPrivateField<Component>(ui, "rowTemplate");
        Transform searchRoot = rootGo != null ? rootGo.transform : ui.transform;

        // Fallback discovery by name.
        if (scrollRect == null)
            scrollRect = FindByNameContains<ScrollRect>(searchRoot, "ItemsScrollView") ?? FindByNameContains<ScrollRect>(searchRoot, "Scroll");

        if (contentRoot == null)
            contentRoot = FindByNameContains<RectTransform>(searchRoot, "Content");

        if (scrollRect != null)
        {
            if (scrollRect.viewport == null)
                scrollRect.viewport = FindByNameContains<RectTransform>(scrollRect.transform, "Viewport") ?? FindByNameContains<RectTransform>(searchRoot, "Viewport");

            if (scrollRect.content == null && contentRoot != null)
                scrollRect.content = contentRoot;

            if (contentRoot == null)
                contentRoot = scrollRect.content;
        }

        RectTransform viewport = scrollRect != null ? scrollRect.viewport : null;

        // Basic active state.
        report.Add("--- Active State ---");
        report.Add($"root activeSelf={(rootGo != null ? rootGo.activeSelf : false)} activeInHierarchy={(rootGo != null ? rootGo.activeInHierarchy : false)}");
        report.Add($"ui activeInHierarchy={ui.gameObject.activeInHierarchy} enabled={ui.enabled}");
        report.Add($"scrollRect={(scrollRect != null ? scrollRect.name : "(null)")} activeInHierarchy={(scrollRect != null ? scrollRect.gameObject.activeInHierarchy : false)}");
        report.Add($"viewport={(viewport != null ? viewport.name : "(null)")} activeInHierarchy={(viewport != null ? viewport.gameObject.activeInHierarchy : false)}");
        report.Add($"content={(contentRoot != null ? contentRoot.name : "(null)")} activeInHierarchy={(contentRoot != null ? contentRoot.gameObject.activeInHierarchy : false)}");

        // Mask.
        report.Add("--- Viewport Mask ---");
        bool hasRectMask = viewport != null && viewport.GetComponent<RectMask2D>() != null;
        bool hasMask = viewport != null && viewport.GetComponent<Mask>() != null;
        report.Add($"Viewport has RectMask2D={hasRectMask} Mask={hasMask}");

        // Rows.
        report.Add("--- Rows Under Content ---");
        Transform rowTemplateTf = rowTemplate != null ? rowTemplate.transform : null;
        if (contentRoot == null)
        {
            report.Add("contentRoot missing; cannot enumerate rows.");
        }
        else
        {
            var rowNames = new List<string>();
            var rowTransforms = new List<RectTransform>();

            for (int i = 0; i < contentRoot.childCount; i++)
            {
                var ch = contentRoot.GetChild(i);
                if (ch == null) continue;
                if (rowTemplateTf != null && ch == rowTemplateTf) continue;
                rowNames.Add(ch.name);
                var rt = ch as RectTransform;
                if (rt != null) rowTransforms.Add(rt);
            }

            report.Add($"childCount(excluding RowTemplate)={rowNames.Count}");
            if (rowNames.Count > 0)
                report.Add("rows: " + string.Join(", ", rowNames));

            foreach (var rt in rowTransforms)
            {
                var size = rt.rect.size;
                report.Add($"row '{rt.name}': size=({size.x:0.##},{size.y:0.##}) anchoredPos={rt.anchoredPosition} localScale={rt.localScale}");

                // Quick alpha checks.
                var tmp = rt.GetComponentInChildren<TMP_Text>(true);
                if (tmp != null)
                {
                    float a = tmp.color.a;
                    report.Add($"  TMP '{tmp.name}': text='{Trim(tmp.text, 40)}' colorA={a:0.##} alphaProp={tmp.alpha:0.##}");
                }

                var g = rt.GetComponentInChildren<Graphic>(true);
                if (g != null)
                    report.Add($"  Graphic '{g.name}' ({g.GetType().Name}): colorA={g.color.a:0.##} raycastTarget={g.raycastTarget} enabled={g.enabled}");
            }
        }

        // Canvas & overlay detection.
        report.Add("--- Canvas / Overlay Detection ---");
        var listRt = scrollRect != null ? scrollRect.GetComponent<RectTransform>() : null;
        var listCanvas = scrollRect != null ? scrollRect.GetComponentInParent<Canvas>() : null;
        var listAreaRt = viewport != null ? viewport : listRt;

        if (scrollRect == null || listCanvas == null || listAreaRt == null)
        {
            report.Add($"Missing scrollRect/canvas/listRect. scrollRect={(scrollRect != null)} listCanvas={(listCanvas != null)} listArea={(listAreaRt != null)}");
            Flush(report);
            return;
        }

        var listCam = GetCanvasCamera(listCanvas);
        var listScreenRect = GetScreenRect(listAreaRt, listCam);

        int listEffectiveOrder = GetEffectiveSortingOrder(listCanvas);
        report.Add($"List Canvas='{listCanvas.name}' renderMode={listCanvas.renderMode} overrideSorting={listCanvas.overrideSorting} sortingLayer='{SortingLayer.IDToName(listCanvas.sortingLayerID)}' sortingOrder={listCanvas.sortingOrder} effectiveOrder={listEffectiveOrder}");
        report.Add($"List screenRect={RectToString(listScreenRect)} areaRt='{listAreaRt.name}'");

        // Build hierarchy order index for draw-order comparisons.
        var orderedTransforms = listCanvas.GetComponentsInChildren<Transform>(true);
        var orderIndex = new Dictionary<Transform, int>(orderedTransforms.Length);
        for (int i = 0; i < orderedTransforms.Length; i++)
            if (orderedTransforms[i] != null)
                orderIndex[orderedTransforms[i]] = i;

        int scrollIndex = orderIndex.TryGetValue(scrollRect.transform, out var si) ? si : -1;
        report.Add($"ScrollRect hierarchyIndex={scrollIndex} siblingIndex={scrollRect.transform.GetSiblingIndex()} parent='{scrollRect.transform.parent?.name}'");

        var suspectedOverlays = new List<Graphic>();
        var offenders = new List<UnityEngine.Object>();

        var allGraphics = listCanvas.GetComponentsInChildren<Graphic>(true);
        foreach (var graphic in allGraphics)
        {
            if (graphic == null) continue;
            if (!graphic.gameObject.activeInHierarchy) continue;

            // Must be on the same Canvas (skip nested canvases).
            if (graphic.canvas != listCanvas) continue;

            // Exclude list subtree itself.
            if (graphic.transform.IsChildOf(scrollRect.transform)) continue;

            if (graphic.color.a <= 0.05f) continue;
            if (graphic.raycastTarget) continue; // clicks passing through is a strong signal

            // Must render above in hierarchy draw order.
            int gi = orderIndex.TryGetValue(graphic.transform, out var gidx) ? gidx : -1;
            if (scrollIndex >= 0 && gi >= 0 && gi <= scrollIndex) continue;

            // Must overlap list area.
            var grt = graphic.transform as RectTransform;
            if (grt == null) continue;

            var gRect = GetScreenRect(grt, listCam);
            if (!gRect.Overlaps(listScreenRect, allowInverse: true)) continue;

            suspectedOverlays.Add(graphic);
            offenders.Add(graphic.gameObject);
        }

        if (suspectedOverlays.Count == 0)
        {
            report.Add("No suspected covering overlays found that match the clickable-but-invisible signature.");
        }
        else
        {
            report.Add($"Suspected overlays found: {suspectedOverlays.Count}");
            foreach (var ov in suspectedOverlays)
            {
                int gi = orderIndex.TryGetValue(ov.transform, out var gidx) ? gidx : -1;
                report.Add($"- '{GetHierarchyPath(ov.transform)}' type={ov.GetType().Name} hierarchyIndex={gi} siblingIndex={ov.transform.GetSiblingIndex()} colorA={ov.color.a:0.##} raycastTarget={ov.raycastTarget}");
            }
        }

        // Check other canvases with higher order that overlap.
        int highestOverlappingCanvasOrder = listEffectiveOrder;
        var canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        foreach (var c in canvases)
        {
            if (c == null || c == listCanvas) continue;
            if (!c.gameObject.activeInHierarchy) continue;

            int otherOrder = GetEffectiveSortingOrder(c);
            if (otherOrder <= listEffectiveOrder) continue;

            var cam = GetCanvasCamera(c);
            bool overlaps = CanvasHasAnyOverlappingGraphic(c, cam, listScreenRect);
            if (!overlaps) continue;

            highestOverlappingCanvasOrder = Math.Max(highestOverlappingCanvasOrder, otherOrder);
            report.Add($"Overlapping higher-order Canvas='{c.name}' renderMode={c.renderMode} overrideSorting={c.overrideSorting} sortingLayer='{SortingLayer.IDToName(c.sortingLayerID)}' sortingOrder={c.sortingOrder} effectiveOrder={otherOrder}");
        }

        // Apply safe fixes.
        if (applyFix)
        {
            report.Add("--- Auto-Fix Actions ---");

            // Ensure viewport mask.
            if (viewport != null)
            {
                if (viewport.GetComponent<RectMask2D>() == null && viewport.GetComponent<Mask>() == null)
                {
                    viewport.gameObject.AddComponent<RectMask2D>();
                    report.Add("Added RectMask2D to Viewport.");
                }
            }

            // Bring list to front.
            scrollRect.transform.SetAsLastSibling();
            report.Add("ScrollRect.transform.SetAsLastSibling() applied.");

            // Try to move background-like overlays behind list, else alpha to 0.
            int moved = 0;
            int alphaZeroed = 0;

            foreach (var ov in suspectedOverlays)
            {
                if (ov == null) continue;

                if (!LooksLikeBackground(ov.name))
                    continue;

                if (ov.raycastTarget) // should already be false
                    continue;

                // Prefer sibling change if same parent as scroll rect.
                if (ov.transform.parent == scrollRect.transform.parent)
                {
                    int desired = Math.Max(0, scrollRect.transform.GetSiblingIndex() - 1);
                    ov.transform.SetSiblingIndex(desired);
                    moved++;
                    report.Add($"Moved behind list: '{GetHierarchyPath(ov.transform)}' -> siblingIndex={desired}");
                }
                else
                {
                    // Fall back to making it invisible.
                    var c = ov.color;
                    if (c.a > 0f)
                    {
                        ov.color = new Color(c.r, c.g, c.b, 0f);
                        alphaZeroed++;
                        report.Add($"Zeroed alpha (fallback): '{GetHierarchyPath(ov.transform)}'");
                    }
                }
            }

            report.Add($"Auto-fix summary: moved={moved} alphaZeroed={alphaZeroed}");

            // If another overlapping canvas exists with higher order, bump list canvas.
            if (highestOverlappingCanvasOrder > listEffectiveOrder)
            {
                listCanvas.overrideSorting = true;
                listCanvas.sortingOrder = Math.Max(200, highestOverlappingCanvasOrder + 1);
                report.Add($"Bumped list canvas sortingOrder to {listCanvas.sortingOrder} (overrideSorting=true) due to higher overlapping canvas order={highestOverlappingCanvasOrder}.");
            }

            // Select offenders.
            if (offenders.Count > 0)
            {
                Selection.objects = offenders.Distinct().ToArray();
                report.Add($"Selected {Selection.objects.Length} offender objects in Editor.");
            }
        }

        Flush(report);
    }

    private static MonoBehaviour FindPlayerInventoryUi()
    {
        // Type-name match is fine per requirements.
        var all = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
        MonoBehaviour best = null;

        foreach (var mb in all)
        {
            if (mb == null) continue;
            var t = mb.GetType();
            if (!string.Equals(t.Name, "PlayerInventoryUI", StringComparison.Ordinal))
                continue;

            // Prefer active-in-hierarchy + enabled.
            if (!mb.gameObject.scene.IsValid())
                continue;

            bool active = mb.gameObject.activeInHierarchy;
            if (!active) continue;

            best = mb;
            if (mb.enabled) break;
        }

        return best;
    }

    private static T GetPrivateField<T>(object obj, string fieldName) where T : class
    {
        try
        {
            if (obj == null) return null;
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var f = obj.GetType().GetField(fieldName, flags);
            if (f == null) return null;
            return f.GetValue(obj) as T;
        }
        catch
        {
            return null;
        }
    }

    private static T FindByNameContains<T>(Transform root, string contains) where T : Component
    {
        if (root == null) return null;
        try
        {
            var all = root.GetComponentsInChildren<T>(true);
            foreach (var c in all)
            {
                if (c == null) continue;
                if (c.name.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0)
                    return c;
            }
        }
        catch { }
        return null;
    }

    private static string GetHierarchyPath(Transform t)
    {
        if (t == null) return "(null)";
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

    private static Camera GetCanvasCamera(Canvas canvas)
    {
        if (canvas == null) return null;
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;
        return canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
    }

    private static Rect GetScreenRect(RectTransform rt, Camera cam)
    {
        var corners = new Vector3[4];
        rt.GetWorldCorners(corners);

        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

        for (int i = 0; i < 4; i++)
        {
            var sp = RectTransformUtility.WorldToScreenPoint(cam, corners[i]);
            min = Vector2.Min(min, sp);
            max = Vector2.Max(max, sp);
        }

        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static string RectToString(Rect r)
    {
        return $"x={r.xMin:0.##}..{r.xMax:0.##} y={r.yMin:0.##}..{r.yMax:0.##}";
    }

    private static int GetEffectiveSortingOrder(Canvas c)
    {
        if (c == null) return 0;
        if (c.overrideSorting) return c.sortingOrder;

        // If not overriding, approximate by nearest parent that does.
        var p = c.transform.parent;
        while (p != null)
        {
            var pc = p.GetComponent<Canvas>();
            if (pc != null && pc.overrideSorting)
                return pc.sortingOrder;
            p = p.parent;
        }

        return c.sortingOrder;
    }

    private static bool CanvasHasAnyOverlappingGraphic(Canvas canvas, Camera cam, Rect targetScreenRect)
    {
        try
        {
            var graphics = canvas.GetComponentsInChildren<Graphic>(true);
            int checkedCount = 0;

            foreach (var g in graphics)
            {
                if (g == null) continue;
                if (!g.gameObject.activeInHierarchy) continue;
                if (g.color.a <= 0.05f) continue;

                var rt = g.transform as RectTransform;
                if (rt == null) continue;

                var r = GetScreenRect(rt, cam);
                if (r.Overlaps(targetScreenRect, allowInverse: true))
                    return true;

                checkedCount++;
                if (checkedCount > 2000)
                    break;
            }
        }
        catch { }

        return false;
    }

    private static bool LooksLikeBackground(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        return name.IndexOf("bg", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("background", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("panel", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("shadow", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string Trim(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return s;
        s = s.Replace("\n", " ").Replace("\r", " ");
        return s.Length <= max ? s : s.Substring(0, max) + "…";
    }

    private static void Flush(List<string> lines)
    {
        if (lines == null || lines.Count == 0) return;
        Debug.Log(string.Join("\n", lines));
    }
}
