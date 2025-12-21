#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Abyss.Equipment;
using Abyss.Inventory;
using Abyss.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ValidateUiIconsEditor
{
    [MenuItem("Tools/UI/Validate Item & UI Icons")]
    public static void ValidateItemAndUiIcons()
    {
        var guids = AssetDatabase.FindAssets("t:ItemDefinition");
        int checkedCount = 0;
        int iconMissing = 0;
        int itemIdMissing = 0;
        int displayNameMissing = 0;

        var failures = new List<string>(256);

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrWhiteSpace(path))
                continue;

            var def = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (def == null)
                continue;

            checkedCount++;

            bool fail = false;
            string itemId = null;
            string displayName = null;
            Sprite icon = null;

            try { itemId = def.itemId; } catch { }
            try { displayName = def.displayName; } catch { }
            try { icon = def.icon; } catch { }

            if (icon == null)
            {
                iconMissing++;
                fail = true;
            }

            if (string.IsNullOrWhiteSpace(itemId))
            {
                itemIdMissing++;
                fail = true;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayNameMissing++;
                fail = true;
            }

            if (fail)
            {
                failures.Add($"FAIL: {path} | itemId='{itemId ?? ""}' | displayName='{displayName ?? ""}' | icon={(icon != null ? icon.name : "NULL")}");
            }
        }

        int failedAssets = failures.Count;
        var summary =
            $"[Validate Item & UI Icons] Checked={checkedCount} Failed={failedAssets} " +
            $"(IconNull={iconMissing}, ItemIdEmpty={itemIdMissing}, DisplayNameEmpty={displayNameMissing})";

        if (failedAssets == 0)
        {
            Debug.Log(summary);
            return;
        }

        Debug.LogWarning(summary + "\n" + string.Join("\n", failures));
    }

    [MenuItem("Tools/UI/Validate Inventory Row Prefab")]
    public static void ValidateInventoryRowTemplate()
    {
        // This project typically uses a scene RowTemplate (built/managed by BuildPlayerInventoryUIEditor),
        // not necessarily a standalone prefab. So validate both:
        // 1) Active scene PlayerInventoryUI instances
        // 2) Prefab assets containing PlayerInventoryUI

        var results = new List<string>(128);
        int pass = 0;
        int fail = 0;

        void ValidateRowTemplate(PlayerInventoryUI ui, string context)
        {
            if (ui == null)
            {
                fail++;
                results.Add($"FAIL: {context} | PlayerInventoryUI NULL");
                return;
            }

            PlayerInventoryRowUI rowTemplate = null;
            try
            {
                var so = new SerializedObject(ui);
                var p = so.FindProperty("rowTemplate");
                rowTemplate = p != null ? p.objectReferenceValue as PlayerInventoryRowUI : null;
            }
            catch { }

            if (rowTemplate == null)
            {
                fail++;
                results.Add($"FAIL: {context} | rowTemplate is NULL");
                return;
            }

            // Validate background field exists and is wired (required by row rendering).
            Image bg = null;
            try
            {
                var soRow = new SerializedObject(rowTemplate);
                var pBg = soRow.FindProperty("background");
                bg = pBg != null ? pBg.objectReferenceValue as Image : null;
            }
            catch { }

            if (bg == null)
            {
                fail++;
                results.Add($"FAIL: {context} | rowTemplate={rowTemplate.name} | Background reference is NULL");
                return;
            }

            // Validate iconImage field points to a child with Image component.
            Image iconImage = null;
            try
            {
                var soRow = new SerializedObject(rowTemplate);
                var pIcon = soRow.FindProperty("iconImage");
                iconImage = pIcon != null ? pIcon.objectReferenceValue as Image : null;
            }
            catch { }

            if (iconImage == null)
            {
                fail++;
                results.Add($"FAIL: {context} | rowTemplate={rowTemplate.name} | IconImage reference is NULL");
                return;
            }

            bool isChild = false;
            try
            {
                isChild = iconImage.transform != null && iconImage.transform.IsChildOf(rowTemplate.transform);
            }
            catch { }

            if (!isChild)
            {
                fail++;
                results.Add($"FAIL: {context} | rowTemplate={rowTemplate.name} | IconImage '{iconImage.name}' is not a child of rowTemplate");
                return;
            }

            // Optional: ensure there is a child named "Icon" and it matches.
            try
            {
                var iconTf = rowTemplate.transform.Find("Icon");
                if (iconTf == null)
                {
                    fail++;
                    results.Add($"FAIL: {context} | rowTemplate={rowTemplate.name} | child 'Icon' not found");
                    return;
                }

                var iconImgChild = iconTf.GetComponent<Image>();
                if (iconImgChild == null)
                {
                    fail++;
                    results.Add($"FAIL: {context} | rowTemplate={rowTemplate.name} | child 'Icon' missing Image component");
                    return;
                }

                if (iconImgChild != iconImage)
                {
                    fail++;
                    results.Add($"FAIL: {context} | rowTemplate={rowTemplate.name} | iconImage field points to '{iconImage.name}', but child 'Icon' is '{iconImgChild.name}'");
                    return;
                }
            }
            catch
            {
                fail++;
                results.Add($"FAIL: {context} | rowTemplate={rowTemplate.name} | exception while validating Icon child");
                return;
            }

            pass++;
            results.Add($"PASS: {context} | rowTemplate={rowTemplate.name} | IconImage={iconImage.name} | Background={bg.name}");
        }

        // 1) Scene instances
        try
        {
            var all = Resources.FindObjectsOfTypeAll<PlayerInventoryUI>();
            var sceneUis = all
                .Where(ui => ui != null && ui.gameObject != null)
                .Where(ui => ui.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(ui.gameObject))
                .ToArray();

            foreach (var ui in sceneUis)
                ValidateRowTemplate(ui, $"Scene:{ui.gameObject.scene.name}/{ui.gameObject.name}");
        }
        catch { }

        // 2) Prefab assets containing PlayerInventoryUI
        try
        {
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            foreach (var guid in prefabGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                var ui = prefab.GetComponentInChildren<PlayerInventoryUI>(true);
                if (ui == null)
                    continue;

                ValidateRowTemplate(ui, $"Prefab:{path}");
            }
        }
        catch { }

        if (pass > 0 && fail == 0)
        {
            Debug.Log($"[Validate Inventory Row Prefab] PASS ({pass} checked)\n" + string.Join("\n", results));
            return;
        }

        Debug.LogWarning($"[Validate Inventory Row Prefab] FAIL (Pass={pass} Fail={fail})\n" + string.Join("\n", results));
    }

    [MenuItem("Tools/UI/Validate Equipment UI Wiring")]
    public static void ValidateEquipmentUiWiring()
    {
        var results = new List<string>(128);
        int pass = 0;
        int fail = 0;

        static string GetHierarchyPath(Transform t)
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

        static Image FindChildImageByNameCaseInsensitive(Transform root, string name)
        {
            if (root == null || string.IsNullOrWhiteSpace(name))
                return null;

            // Breadth-first search so we prefer shallow matches.
            var q = new Queue<Transform>();
            q.Enqueue(root);

            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                if (cur == null) continue;

                if (string.Equals(cur.name, name, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var img = cur.GetComponent<Image>();
                        if (img != null)
                            return img;
                    }
                    catch { }
                }

                for (int i = 0; i < cur.childCount; i++)
                    q.Enqueue(cur.GetChild(i));
            }

            return null;
        }

        void ValidateOne(PlayerEquipmentUI ui, string context)
        {
            if (ui == null)
            {
                fail++;
                results.Add($"FAIL: {context} | PlayerEquipmentUI NULL");
                return;
            }

            SerializedObject so;
            try { so = new SerializedObject(ui); }
            catch
            {
                fail++;
                results.Add($"FAIL: {context} | exception creating SerializedObject");
                return;
            }

            var slotsProp = so.FindProperty("slots");
            if (slotsProp == null || !slotsProp.isArray)
            {
                fail++;
                results.Add($"FAIL: {context} | slots array missing");
                return;
            }

            if (slotsProp.arraySize <= 0)
            {
                fail++;
                results.Add($"FAIL: {context} | slots array is empty");
                return;
            }

            bool didWire = false;

            for (int i = 0; i < slotsProp.arraySize; i++)
            {
                var el = slotsProp.GetArrayElementAtIndex(i);
                var slotInt = el.FindPropertyRelative("slot")?.intValue ?? -1;
                var button = el.FindPropertyRelative("button")?.objectReferenceValue as Button;
                var icon = el.FindPropertyRelative("iconImage")?.objectReferenceValue as Image;
                var strip = el.FindPropertyRelative("rarityStrip")?.objectReferenceValue as Image;

                if (button == null)
                {
                    fail++;
                    results.Add($"FAIL: {context} | slots[{i}] slot={(EquipmentSlot)slotInt} | button NULL");
                    continue;
                }

                if (icon == null)
                {
                    fail++;
                    results.Add($"FAIL: {context} | {button.name} slot={(EquipmentSlot)slotInt} | iconImage NULL");
                    continue;
                }

                if (strip == null)
                {
                    // TASK A: attempt to auto-wire by finding a child named "RarityStrip" (case-insensitive).
                    try
                    {
                        var found = FindChildImageByNameCaseInsensitive(button.transform, "RarityStrip");
                        if (found != null)
                        {
                            var p = el.FindPropertyRelative("rarityStrip");
                            if (p != null)
                            {
                                p.objectReferenceValue = found;
                                strip = found;
                                didWire = true;
                            }
                        }
                        else
                        {
                            var rootPath = GetHierarchyPath(button.transform);
                            results.Add($"WARN: {context} | {button.name} slot={(EquipmentSlot)slotInt} | rarityStrip NULL and child 'RarityStrip' not found under {rootPath}");
                        }
                    }
                    catch { }

                    if (strip == null)
                    {
                        fail++;
                        results.Add($"FAIL: {context} | {button.name} slot={(EquipmentSlot)slotInt} | rarityStrip NULL");
                        continue;
                    }
                }

                bool iconChildOk = false;
                bool stripChildOk = false;
                try
                {
                    var iconTf = button.transform.Find("Icon");
                    iconChildOk = iconTf != null && iconTf.GetComponent<Image>() == icon;

                    var stripTf = button.transform.Find("RarityStrip");
                    stripChildOk = stripTf != null && stripTf.GetComponent<Image>() == strip;
                }
                catch { }

                if (!iconChildOk)
                {
                    fail++;
                    results.Add($"FAIL: {context} | {button.name} | iconImage is not child 'Icon'");
                    continue;
                }

                if (!stripChildOk)
                {
                    fail++;
                    results.Add($"FAIL: {context} | {button.name} | rarityStrip is not child 'RarityStrip'");
                    continue;
                }
            }

            if (didWire)
            {
                try
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(ui);
                    if (ui.gameObject != null && ui.gameObject.scene.IsValid())
                        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
                }
                catch { }
            }

            pass++;
            results.Add($"PASS: {context} | slots={slotsProp.arraySize}");
        }

        // 1) Scene instances
        try
        {
            var all = Resources.FindObjectsOfTypeAll<PlayerEquipmentUI>();
            var sceneUis = all
                .Where(ui => ui != null && ui.gameObject != null)
                .Where(ui => ui.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(ui.gameObject))
                .ToArray();

            foreach (var ui in sceneUis)
                ValidateOne(ui, $"Scene:{ui.gameObject.scene.name}/{ui.gameObject.name}");
        }
        catch { }

        // 2) Prefab assets containing PlayerEquipmentUI
        try
        {
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            foreach (var guid in prefabGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                var ui = prefab.GetComponentInChildren<PlayerEquipmentUI>(true);
                if (ui == null)
                    continue;

                ValidateOne(ui, $"Prefab:{path}");
            }
        }
        catch { }

        if (pass > 0 && fail == 0)
        {
            Debug.Log($"[Validate Equipment UI Wiring] PASS ({pass} checked)\n" + string.Join("\n", results));
            return;
        }

        Debug.LogWarning($"[Validate Equipment UI Wiring] FAIL (Pass={pass} Fail={fail})\n" + string.Join("\n", results));
    }
}
#endif
