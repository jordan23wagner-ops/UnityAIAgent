#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Abyssbound.Cooking;
using Abyss.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace Abyssbound.Cooking.Editor
{
    public static class CookingSetupMenu
    {
        private const string MenuPath = "Tools/Cooking/Setup Bonfire (Town)";

        private const string CookedShrimpItemId = "cooked_shrimp";
        private const string RawShrimpItemId = "fish_raw_shrimp";

        private const string CookedShrimpItemAssetPath = "Assets/GameData/Items/Item_CookedShrimp.asset";
        private const string RecipeAssetPath = "Assets/GameData/Cooking/Recipes/Recipe_RawShrimp_To_CookedShrimp.asset";
        private const string CookingUiPrefabPath = "Assets/Prefabs/UI/CookingUI.prefab";
        private const string BonfirePrefabPath = "Assets/Prefabs/World/Bonfire_CookingStation.prefab";

        private const string BonfireBaseMaterialPath = "Assets/GameData/Materials/Mat_Bonfire_Base.mat";
        private const string BonfireFlameMaterialPath = "Assets/GameData/Materials/Mat_Bonfire_Flame.mat";

        [MenuItem(MenuPath)]
        public static void SetupBonfire()
        {
            EnsureFolders();

            var (baseMat, flameMat) = EnsureBonfireMaterials();

            var cookedDef = EnsureCookedShrimpItem();
            var recipe = EnsureRecipeAsset();
            var uiPrefab = EnsureCookingUiPrefab(cookedDef);
            var bonfirePrefab = EnsureBonfirePrefab(uiPrefab, recipe, baseMat, flameMat);

            EnsureBonfireInActiveScene(bonfirePrefab, uiPrefab, recipe, baseMat, flameMat);

            Debug.Log("[Cooking] Bonfire setup complete.");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/GameData");
            EnsureFolder("Assets/GameData/Items");
            EnsureFolder("Assets/GameData/Cooking");
            EnsureFolder("Assets/GameData/Cooking/Recipes");
            EnsureFolder("Assets/GameData/Materials");
            EnsureFolder("Assets/Prefabs");
            EnsureFolder("Assets/Prefabs/UI");
            EnsureFolder("Assets/Prefabs/World");
            EnsureFolder("Assets/Editor");
            EnsureFolder("Assets/Editor/Cooking");
        }

        private static (Material baseMat, Material flameMat) EnsureBonfireMaterials()
        {
            var baseMat = AssetDatabase.LoadAssetAtPath<Material>(BonfireBaseMaterialPath);
            if (baseMat == null)
            {
                baseMat = CreateUnlitColorMaterial(new Color(0.8f, 0.1f, 0.1f, 1f));
                AssetDatabase.CreateAsset(baseMat, BonfireBaseMaterialPath);
            }

            var flameMat = AssetDatabase.LoadAssetAtPath<Material>(BonfireFlameMaterialPath);
            if (flameMat == null)
            {
                flameMat = CreateUnlitColorMaterial(new Color(1.0f, 0.45f, 0.1f, 1f));
                AssetDatabase.CreateAsset(flameMat, BonfireFlameMaterialPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return (baseMat, flameMat);
        }

        private static Material CreateUnlitColorMaterial(Color color)
        {
            // Prefer the exact shader name requested; fall back if project uses URP.
            var shader = Shader.Find("Unlit/Color");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Texture");
            if (shader == null)
                shader = Shader.Find("Standard");

            var mat = new Material(shader);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);

            return mat;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parts = path.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        private static ItemDefinition EnsureCookedShrimpItem()
        {
            var existing = FindItemDefinitionByItemId(CookedShrimpItemId);
            if (existing != null)
                return existing;

            var def = ScriptableObject.CreateInstance<ItemDefinition>();
            def.itemId = CookedShrimpItemId;
            def.displayName = "Cooked Shrimp";
            def.description = "A freshly cooked shrimp.";
            def.itemType = Abyss.Items.ItemType.Consumable;
            def.rarity = Abyss.Items.ItemRarity.Common;

            // Reuse raw shrimp icon if available.
            try
            {
                var raw = FindItemDefinitionByItemId(RawShrimpItemId);
                if (raw != null)
                    def.icon = raw.icon;
            }
            catch { }

            AssetDatabase.CreateAsset(def, CookedShrimpItemAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return def;
        }

        private static CookingRecipeSO EnsureRecipeAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<CookingRecipeSO>(RecipeAssetPath);
            if (existing != null)
                return existing;

            var recipe = ScriptableObject.CreateInstance<CookingRecipeSO>();
            recipe.recipeId = "raw_shrimp_to_cooked_shrimp";
            recipe.displayName = "Cook Shrimp";
            recipe.inputItemId = RawShrimpItemId;
            recipe.inputCount = 1;
            recipe.outputItemId = CookedShrimpItemId;
            recipe.outputCount = 1;

            AssetDatabase.CreateAsset(recipe, RecipeAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return recipe;
        }

        private static CookingUIController EnsureCookingUiPrefab(ItemDefinition cookedShrimpDef)
        {
            var existing = AssetDatabase.LoadAssetAtPath<CookingUIController>(CookingUiPrefabPath);
            if (existing != null)
                return existing;

            var root = new GameObject("CookingUI", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(CookingUIController));
            var rt = root.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(520, 420);

            var img = root.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.85f);

            // Title
            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(root.transform, false);
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -12f);
            titleRt.sizeDelta = new Vector2(0f, 40f);

            var titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
            titleTmp.text = "Cooking";
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.fontSize = 28f;
            titleTmp.color = Color.white;

            // Close button
            var closeGo = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(root.transform, false);
            var closeRt = closeGo.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(1f, 1f);
            closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-10f, -10f);
            closeRt.sizeDelta = new Vector2(32f, 32f);
            closeGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);

            var closeLabelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            closeLabelGo.transform.SetParent(closeGo.transform, false);
            var closeLabelRt = closeLabelGo.GetComponent<RectTransform>();
            closeLabelRt.anchorMin = Vector2.zero;
            closeLabelRt.anchorMax = Vector2.one;
            closeLabelRt.offsetMin = Vector2.zero;
            closeLabelRt.offsetMax = Vector2.zero;
            var closeTmp = closeLabelGo.GetComponent<TextMeshProUGUI>();
            closeTmp.text = "X";
            closeTmp.alignment = TextAlignmentOptions.Center;
            closeTmp.fontSize = 22f;
            closeTmp.color = Color.white;

            // List root
            var listGo = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            listGo.transform.SetParent(root.transform, false);
            var listRt = listGo.GetComponent<RectTransform>();
            listRt.anchorMin = new Vector2(0f, 0f);
            listRt.anchorMax = new Vector2(1f, 1f);
            listRt.pivot = new Vector2(0.5f, 0.5f);
            listRt.offsetMin = new Vector2(18f, 18f);
            listRt.offsetMax = new Vector2(-18f, -64f);

            var vlg = listGo.GetComponent<VerticalLayoutGroup>();
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.spacing = 10f;
            vlg.padding = new RectOffset(0, 0, 0, 0);

            var fitter = listGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Empty text
            var emptyGo = new GameObject("Empty", typeof(RectTransform), typeof(TextMeshProUGUI));
            emptyGo.transform.SetParent(root.transform, false);
            var emptyRt = emptyGo.GetComponent<RectTransform>();
            emptyRt.anchorMin = new Vector2(0f, 0f);
            emptyRt.anchorMax = new Vector2(1f, 0f);
            emptyRt.pivot = new Vector2(0.5f, 0f);
            emptyRt.anchoredPosition = new Vector2(0f, 20f);
            emptyRt.sizeDelta = new Vector2(0f, 34f);
            var emptyTmp = emptyGo.GetComponent<TextMeshProUGUI>();
            emptyTmp.text = "No available recipes.";
            emptyTmp.alignment = TextAlignmentOptions.Center;
            emptyTmp.fontSize = 18f;
            emptyTmp.color = new Color(1f, 1f, 1f, 0.8f);
            emptyGo.SetActive(false);

            // Recipe button template
            var btnGo = new GameObject("RecipeButtonTemplate", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(listGo.transform, false);
            var btnImg = btnGo.GetComponent<Image>();
            btnImg.color = new Color(1f, 1f, 1f, 0.12f);

            var btnRt = btnGo.GetComponent<RectTransform>();
            btnRt.sizeDelta = new Vector2(0f, 46f);

            var btnTextGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            btnTextGo.transform.SetParent(btnGo.transform, false);
            var btnTextRt = btnTextGo.GetComponent<RectTransform>();
            btnTextRt.anchorMin = Vector2.zero;
            btnTextRt.anchorMax = Vector2.one;
            btnTextRt.offsetMin = new Vector2(12f, 6f);
            btnTextRt.offsetMax = new Vector2(-12f, -6f);

            var btnTmp = btnTextGo.GetComponent<TextMeshProUGUI>();
            btnTmp.text = "Cook";
            btnTmp.alignment = TextAlignmentOptions.Left;
            btnTmp.fontSize = 18f;
            btnTmp.color = Color.white;

            btnGo.SetActive(false);

            // Wire controller fields
            var ctrl = root.GetComponent<CookingUIController>();
            var closeBtn = closeGo.GetComponent<Button>();
            var btnTemplate = btnGo.GetComponent<Button>();

            SetPrivateField(ctrl, "panel", rt);
            SetPrivateField(ctrl, "titleText", titleTmp);
            SetPrivateField(ctrl, "closeButton", closeBtn);
            SetPrivateField(ctrl, "listRoot", listRt);
            SetPrivateField(ctrl, "recipeButtonTemplate", btnTemplate);
            SetPrivateField(ctrl, "emptyText", emptyTmp);

            // Preload cooked shrimp def so inventory UI can show icon once created.
            if (cookedShrimpDef != null)
                SetPrivateField(ctrl, "preloadItemDefinitions", new ItemDefinition[] { cookedShrimpDef });

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, CookingUiPrefabPath);
            GameObject.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return prefab != null ? prefab.GetComponent<CookingUIController>() : AssetDatabase.LoadAssetAtPath<CookingUIController>(CookingUiPrefabPath);
        }

        private static GameObject EnsureBonfirePrefab(CookingUIController uiPrefab, CookingRecipeSO recipe, Material baseMat, Material flameMat)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(BonfirePrefabPath);

            // Always enforce visuals/materials on the prefab so it works with zero manual editing.
            if (existing != null)
            {
                var prefabRoot = PrefabUtility.LoadPrefabContents(BonfirePrefabPath);
                try
                {
                    EnsureBonfirePrefabContents(prefabRoot, uiPrefab, recipe, baseMat, flameMat);
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, BonfirePrefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return existing;
            }

            var root = new GameObject("Bonfire_CookingStation", typeof(CookingStation), typeof(BoxCollider));
            EnsureBonfirePrefabContents(root, uiPrefab, recipe, baseMat, flameMat);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, BonfirePrefabPath);
            GameObject.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return prefab;
        }

        private static void EnsureBonfirePrefabContents(GameObject root, CookingUIController uiPrefab, CookingRecipeSO recipe, Material baseMat, Material flameMat)
        {
            if (root == null)
                return;

            root.name = "Bonfire_CookingStation";

            // Required collider stays on the root; do not modify interaction/gameplay.
            var col = root.GetComponent<BoxCollider>();
            if (col == null)
                col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(1.5f, 2.0f, 1.5f);
            col.center = new Vector3(0f, 1f, 0f);
            col.isTrigger = false;

            var station = root.GetComponent<CookingStation>();
            if (station == null)
                station = root.AddComponent<CookingStation>();
            station.SetUiPrefab(uiPrefab);
            station.SetRecipes(new[] { recipe });

            if (root.GetComponent<BonfireVisuals>() == null)
                root.AddComponent<BonfireVisuals>();

            EnsureBonfireVisualChildren(root, baseMat, flameMat);
        }

        private static void EnsureBonfireVisualChildren(GameObject root, Material baseMat, Material flameMat)
        {
            if (root == null)
                return;

            var baseGo = FindOrCreateCylinderChild(root.transform, "Visual_Base");
            baseGo.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            baseGo.transform.localScale = new Vector3(1.6f, 0.2f, 1.6f);
            ForceDefaultLayerRecursive(baseGo);
            AssignMaterialAndEnable(baseGo, baseMat);

            var flameGo = FindOrCreateCylinderChild(root.transform, "Visual_Flame");
            flameGo.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            flameGo.transform.localScale = new Vector3(0.25f, 1.5f, 0.25f);
            ForceDefaultLayerRecursive(flameGo);
            AssignMaterialAndEnable(flameGo, flameMat);
        }

        private static GameObject FindOrCreateCylinderChild(Transform parent, string childName)
        {
            var existing = parent.Find(childName);
            if (existing != null)
                return existing.gameObject;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = childName;
            go.transform.SetParent(parent, false);

            // Strip collider from visual primitive.
            var primitiveCol = go.GetComponent<Collider>();
            if (primitiveCol != null)
                UnityEngine.Object.DestroyImmediate(primitiveCol);

            // Ensure components exist as explicitly requested.
            if (go.GetComponent<MeshFilter>() == null)
                go.AddComponent<MeshFilter>();
            if (go.GetComponent<MeshRenderer>() == null)
                go.AddComponent<MeshRenderer>();

            return go;
        }

        private static void AssignMaterialAndEnable(GameObject go, Material mat)
        {
            if (go == null)
                return;

            var mr = go.GetComponent<MeshRenderer>();
            if (mr == null)
                mr = go.AddComponent<MeshRenderer>();

            mr.enabled = true;
            if (mat != null)
                mr.sharedMaterial = mat;
        }

        private static void ForceDefaultLayerRecursive(GameObject go)
        {
            if (go == null)
                return;

            go.layer = 0;
            foreach (Transform child in go.transform)
                ForceDefaultLayerRecursive(child.gameObject);
        }

        private static void EnsureBonfireInActiveScene(GameObject bonfirePrefab, CookingUIController uiPrefab, CookingRecipeSO recipe, Material baseMat, Material flameMat)
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
                return;

            var parent = GameObject.Find("[Cooking]");
            if (parent == null)
                parent = new GameObject("[Cooking]");

            // Prefer upgrading an existing placeholder bonfire if present.
            var existingPlaceholder = UnityEngine.Object.FindFirstObjectByType<BonfireInteractable>(FindObjectsInactive.Include);
            GameObject stationGo = null;
            bool createdOrMovedByTool = false;

            if (existingPlaceholder != null)
            {
                stationGo = existingPlaceholder.gameObject;
                // Replace placeholder script to avoid Interact() ambiguity.
                UnityEngine.Object.DestroyImmediate(existingPlaceholder);
            }

            var existingStation = UnityEngine.Object.FindFirstObjectByType<CookingStation>(FindObjectsInactive.Include);
            if (existingStation != null)
                stationGo = existingStation.gameObject;

            if (stationGo == null)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(bonfirePrefab, scene);
                stationGo = inst;
                createdOrMovedByTool = true;
            }

            if (stationGo == null)
                return;

            stationGo.name = "Bonfire_CookingStation";

            // Safety: ensure it's not sunk below the ground.
            var p = stationGo.transform.position;
            if (p.y < 0.1f)
            {
                p.y = 0.1f;
                stationGo.transform.position = p;
            }

            // Only parent under [Cooking] if we created it, to avoid disturbing hand-built scenes.
            if (createdOrMovedByTool)
                stationGo.transform.SetParent(parent.transform, worldPositionStays: true);

            var station = stationGo.GetComponent<CookingStation>();
            if (station == null)
                station = stationGo.AddComponent<CookingStation>();

            station.SetUiPrefab(uiPrefab);
            station.SetRecipes(new[] { recipe });

            // Ensure a collider for raycast interaction.
            if (stationGo.GetComponent<Collider>() == null)
            {
                var c = stationGo.AddComponent<BoxCollider>();
                c.isTrigger = false;
                c.center = new Vector3(0f, 1f, 0f);
                c.size = new Vector3(1.5f, 2.0f, 1.5f);
            }

            if (stationGo.GetComponent<BonfireVisuals>() == null)
                stationGo.AddComponent<BonfireVisuals>();

            EnsureBonfireVisualChildren(stationGo, baseMat, flameMat);

            // Place near town anchor if found (only for newly created instances).
            if (createdOrMovedByTool)
            {
                if (TryFindTownAnchor(out var pos))
                    stationGo.transform.position = pos + new Vector3(1.5f, 0f, 0f);
                else
                    stationGo.transform.position = Vector3.zero;
            }

            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static bool TryFindTownAnchor(out Vector3 pos)
        {
            pos = Vector3.zero;

            var a = GameObject.Find("TownSpawn");
            if (a != null)
            {
                pos = a.transform.position;
                return true;
            }

            a = GameObject.Find("WP_Town");
            if (a != null)
            {
                pos = a.transform.position;
                return true;
            }

            // Fallback scan.
            var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            foreach (var t in all)
            {
                if (t == null) continue;
                var n = t.name;
                if (string.IsNullOrWhiteSpace(n)) continue;

                if (n.IndexOf("TownSpawn", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("WP_Town", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    pos = t.position;
                    return true;
                }
            }

            return false;
        }

        private static ItemDefinition FindItemDefinitionByItemId(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return null;

            var guids = AssetDatabase.FindAssets("t:ItemDefinition");
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var def = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                if (def == null) continue;

                try
                {
                    if (string.Equals(def.itemId, itemId, StringComparison.OrdinalIgnoreCase))
                        return def;
                }
                catch { }
            }

            return null;
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            if (target == null || string.IsNullOrWhiteSpace(fieldName))
                return;

            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
            var f = target.GetType().GetField(fieldName, flags);
            if (f == null)
                return;

            f.SetValue(target, value);

            EditorUtility.SetDirty((UnityEngine.Object)target);
        }
    }
}
#endif
