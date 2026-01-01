using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AbyssboundTownLayoutMenu
{
    private const string MenuPath = "Abyssbound/Town/Apply Layout v1 (Linear Campsite)";
    private const string MenuPathPrint = "Abyssbound/Town/Print Layout Candidates";

    [MenuItem(MenuPath)]
    private static void ApplyLayoutLinearCampsite()
    {
        var townGo = GameObject.Find("Town");
        if (townGo == null)
        {
            Debug.LogWarning("[TownLayout] 'Town' root not found (aborted). Please open a scene that contains a Town root.");
            return;
        }

        var town = townGo.transform;

        var townOrigin = FindUnderRootByExactName(town, "TownOrigin");
        Vector3 basePos = townOrigin != null ? townOrigin.position : town.position;

        var edgevilleHubRoot = FindUnderRootByExactName(town, "EdgevilleHub_Root");

        // Spawn: PlayerSpawn_Town (typically under EdgevilleHub_Root).
        var spawn = edgevilleHubRoot != null
            ? FindUnderRootByExactName(edgevilleHubRoot, "PlayerSpawn_Town")
            : null;
        if (spawn == null)
            spawn = FindUnderRootByExactName(town, "PlayerSpawn_Town");

        // Merchants root: prefer Merchants under EdgevilleHub_Root.
        var merchantsRoot = edgevilleHubRoot != null
            ? FindUnderRootByExactName(edgevilleHubRoot, "Merchants")
            : null;
        if (merchantsRoot == null)
            merchantsRoot = FindUnderRootByExactName(town, "Merchants");

        // Bonfire: search under Town for Bonfire/Cooking; if missing, try [Cooking] root anywhere.
        var bonfire = FindUnderRootByNameContainsAny(town, "Bonfire", "Cooking");
        if (bonfire == null)
        {
            var cookingRoot = FindSceneTransformByExactNameIncludingInactive("[Cooking]");
            if (cookingRoot != null)
                bonfire = FindUnderRootByNameContainsAny(cookingRoot, "Bonfire", "CookingStation", "Cooking");
        }

        // Chest: search under Town by name fragments.
        var chest = FindUnderRootByNameContainsAny(town, "Chest", "Bank", "Storage");

        var exitMarker = FindUnderRootByExactName(town, "TownExit");
        if (exitMarker == null)
            exitMarker = FindUnderRootByExactName(town, "Exit");

        var missing = new List<string>(4);
        if (townOrigin == null) missing.Add("TownOrigin");
        if (spawn == null) missing.Add("PlayerSpawn_Town");
        if (bonfire == null) missing.Add("Bonfire/Cooking (under Town, or [Cooking] root)");
        if (chest == null) missing.Add("Chest/Bank/Storage (name contains, under Town)");

        if (missing.Count > 0)
            Debug.LogWarning($"[TownLayout] Missing: {string.Join(", ", missing)} (skipped those)");

        // Discovery diagnostics (before any moves).
        if (townOrigin != null) LogFound("TownOrigin", townOrigin);
        else LogFound("Town (fallback)", town);

        if (edgevilleHubRoot != null) LogFound("EdgevilleHub_Root", edgevilleHubRoot);
        if (spawn != null) LogFound("PlayerSpawn_Town", spawn);
        if (merchantsRoot != null) LogFound("Merchants", merchantsRoot);
        if (bonfire != null) LogFound("Bonfire", bonfire);
        if (chest != null) LogFound("Chest", chest);
        if (exitMarker != null) LogFound("Exit", exitMarker);

        bool movedAny = false;
        int movedCount = 0;

        // Hub container: align to TownOrigin so everything under it moves together.
        if (edgevilleHubRoot != null)
            movedAny |= MoveTo(edgevilleHubRoot, basePos, ref movedCount);

        if (spawn != null)
            movedAny |= MoveTo(spawn, basePos + new Vector3(0f, 0f, -10f), ref movedCount);

        if (merchantsRoot != null)
        {
            movedAny |= MoveTo(merchantsRoot, basePos + new Vector3(0f, 0f, -3f), ref movedCount);
            movedAny |= ArrangeMerchants(merchantsRoot, ref movedCount);
        }

        if (bonfire != null)
            movedAny |= MoveTo(bonfire, basePos + new Vector3(0f, 0f, 4f), ref movedCount);

        if (chest != null)
            movedAny |= MoveTo(chest, basePos + new Vector3(3f, 0f, 6f), ref movedCount);

        if (exitMarker != null)
            movedAny |= MoveTo(exitMarker, basePos + new Vector3(0f, 0f, 14f), ref movedCount);

        Debug.Log($"[TownLayout] Done. baseP={FormatVec3(basePos)} movedCount={movedCount}");

        if (movedAny)
            EditorSceneManager.MarkSceneDirty(town.gameObject.scene);
    }

    [MenuItem(MenuPathPrint)]
    private static void PrintLayoutCandidates()
    {
        var townGo = GameObject.Find("Town");
        if (townGo == null)
        {
            Debug.LogWarning("[TownLayout] 'Town' root not found (aborted). Please open a scene that contains a Town root.");
            return;
        }

        var town = townGo.transform;

        int directChildCount = 0;
        try { directChildCount = town.childCount; } catch { directChildCount = 0; }

        Debug.Log($"[TownLayout] TownRoot={GetTransformPath(town)} childCount={directChildCount}");

        // Print direct children (first 25) to help locate nested roots.
        int max = Mathf.Min(25, directChildCount);
        for (int i = 0; i < max; i++)
        {
            Transform child = null;
            try { child = town.GetChild(i); } catch { child = null; }
            if (child == null) continue;
            Debug.Log($"[TownLayout] TownChild {i + 1}/{max}: {GetTransformPath(child)} (name={child.name})");
        }

        Transform[] all;
        try { all = town.GetComponentsInChildren<Transform>(true); }
        catch { all = null; }

        if (all == null || all.Length == 0)
            return;

        var needles = new[]
        {
            "spawn",
            "bonfire",
            "cook",
            "chest",
            "bank",
            "storage",
            "merchant",
            "hub",
            "origin",
            "exit",
        };

        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == null) continue;

            string n = t.name ?? string.Empty;
            bool match = false;
            for (int k = 0; k < needles.Length; k++)
            {
                if (n.IndexOf(needles[k], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    match = true;
                    break;
                }
            }

            if (!match) continue;

            Debug.Log($"[TownLayout][Candidate] {GetTransformPath(t)} (name={t.name})");
        }
    }

    private static bool ArrangeMerchants(Transform merchantsRoot, ref int movedCount)
    {
        if (merchantsRoot == null) return false;

        var eligible = new List<Transform>(16);
        for (int i = 0; i < merchantsRoot.childCount; i++)
        {
            var child = merchantsRoot.GetChild(i);
            if (child == null) continue;

            string n = child.name ?? string.Empty;
            if (n.IndexOf("Merchant", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                eligible.Add(child);
            }
        }

        if (eligible.Count == 0) return false;

        // Deterministic order for idempotence.
        eligible.Sort((a, b) => string.Compare(a != null ? a.name : "", b != null ? b.name : "", StringComparison.OrdinalIgnoreCase));

        const float spacing = 2.5f;
        float centerX = merchantsRoot.position.x;
        float centerZ = merchantsRoot.position.z;

        float startX = centerX - (spacing * (eligible.Count - 1) * 0.5f);

        bool movedAny = false;
        for (int i = 0; i < eligible.Count; i++)
        {
            var t = eligible[i];
            if (t == null) continue;

            Vector3 target = new Vector3(startX + spacing * i, t.position.y, centerZ);
            movedAny |= MoveToExactY(t, target, ref movedCount);
        }

        return movedAny;
    }

    private static Transform FindUnderRootByExactName(Transform root, string exactName)
    {
        if (root == null || string.IsNullOrEmpty(exactName)) return null;

        Transform[] all;
        try { all = root.GetComponentsInChildren<Transform>(true); }
        catch { return null; }

        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == null) continue;
            if (string.Equals(t.name, exactName, StringComparison.Ordinal))
                return t;
        }

        return null;
    }

    private static Transform FindUnderRootByNameContains(Transform root, string needle)
    {
        if (root == null || string.IsNullOrEmpty(needle)) return null;

        Transform[] all;
        try { all = root.GetComponentsInChildren<Transform>(true); }
        catch { return null; }

        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == null) continue;
            if (t.name != null && t.name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                return t;
        }

        return null;
    }

    private static Transform FindUnderRootByNameContainsAny(Transform root, params string[] needles)
    {
        if (root == null || needles == null || needles.Length == 0) return null;

        Transform[] all;
        try { all = root.GetComponentsInChildren<Transform>(true); }
        catch { return null; }

        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == null) continue;
            if (t.name == null) continue;

            for (int k = 0; k < needles.Length; k++)
            {
                var needle = needles[k];
                if (string.IsNullOrEmpty(needle)) continue;
                if (t.name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                    return t;
            }
        }

        return null;
    }

    private static Transform FindSceneTransformByExactNameIncludingInactive(string exactName)
    {
        if (string.IsNullOrEmpty(exactName)) return null;

        Transform[] all;
        try { all = Resources.FindObjectsOfTypeAll<Transform>(); }
        catch { return null; }

        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == null) continue;

            // Avoid prefabs/assets.
            try { if (EditorUtility.IsPersistent(t)) continue; }
            catch { }

            if (string.Equals(t.name, exactName, StringComparison.Ordinal))
                return t;
        }

        return null;
    }

    private static bool IsSafeWorldTransform(Transform t)
    {
        if (t == null) return false;

        // Safety: never move UI/RectTransform hierarchies.
        try
        {
            if (t.GetComponent<RectTransform>() != null)
                return false;
        }
        catch { }

        return true;
    }

    private static bool MoveTo(Transform t, Vector3 targetWorldPos, ref int movedCount)
    {
        if (t == null) return false;

        // Preserve the object's current Y.
        targetWorldPos.y = t.position.y;
        return MoveToExactY(t, targetWorldPos, ref movedCount);
    }

    private static bool MoveToExactY(Transform t, Vector3 targetWorldPos, ref int movedCount)
    {
        if (t == null) return false;

        if (!IsSafeWorldTransform(t))
        {
            Debug.LogWarning($"[TownLayout] Skipped UI/RectTransform target: {GetTransformPath(t)}", t);
            return false;
        }

        if ((t.position - targetWorldPos).sqrMagnitude <= 0.000001f)
            return false;

        Undo.RecordObject(t, "Town Layout Move");
        t.position = targetWorldPos;
        movedCount++;

        Debug.Log($"[TownLayout] Moved {GetTransformPath(t)} to {FormatVec3(targetWorldPos)}");
        return true;
    }

    private static void LogFound(string label, Transform t)
    {
        if (t == null) return;
        Debug.Log($"[TownLayout] Found {label} at {FormatVec3(t.position)} path={GetTransformPath(t)}");
    }

    private static string GetTransformPath(Transform t)
    {
        if (t == null) return "<null>";

        var stack = new Stack<string>(16);
        var cur = t;
        while (cur != null)
        {
            stack.Push(cur.name);
            cur = cur.parent;
        }

        return string.Join("/", stack);
    }

    private static string FormatVec3(Vector3 v)
    {
        return $"({v.x:0.###}, {v.y:0.###}, {v.z:0.###})";
    }
}
