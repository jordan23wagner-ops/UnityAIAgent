using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.UI;

using Abyss.Shop;
using Abyss.Town;

using Game.Town;

public class AIAssistantTownSetup : MonoBehaviour
{

    private static System.Collections.Generic.HashSet<string> _loggedParentWarnings = new System.Collections.Generic.HashSet<string>();

    public bool enableAutoSpawn = false;

    [Header("Debug")]
    [Tooltip("If enabled, spawns visible sphere markers for town objects.")]
    [SerializeField] private bool createDebugMarkers = false;

#if UNITY_EDITOR
    private void Start()
    {
        if (enableAutoSpawn)
        {
            SetupTown();
        }
        else
        {
            Debug.LogWarning("[AIAssistantTownSetup] Auto-spawn is disabled. Set enableAutoSpawn = true to allow spawning on play.", this);
        }
    }

    public void SetupTown()
    {
        var registry = TownRegistry.Instance;
        registry.EnsureSpawnRoot();
#if UNITY_EDITOR
        if (!Application.isPlaying)
            registry.RebuildIndexFromScene();
#endif
        float y = 5f;
        Vector3 basePos = new Vector3(0, y, 0);
        float spacing = 5f;
        var created = new System.Collections.Generic.List<GameObject>();

        created.Add(CreateMerchant("merchant_weaponsgear", typeof(WeaponsGearMerchant), basePos + new Vector3(0,0,0), Color.red));
        created.Add(CreateMerchant("merchant_consumables", typeof(ConsumablesMerchant), basePos + new Vector3(spacing,0,0), Color.green));
        created.Add(CreateMerchant("merchant_skilling", typeof(SkillingSuppliesMerchant), basePos + new Vector3(2*spacing,0,0), Color.blue));
        created.Add(CreateMerchant("merchant_workshop", typeof(WorkshopMerchant), basePos + new Vector3(3*spacing,0,0), Color.yellow));

        created.Add(CreateInteractable("interactable_forge", typeof(ForgeInteractable), basePos + new Vector3(3*spacing,0,2.5f), Color.gray));
        created.Add(CreateInteractable("interactable_smithingstand", typeof(SmithingStandInteractable), basePos + new Vector3(3*spacing+1.5f,0,0), Color.magenta));
        created.Add(CreateInteractable("interactable_workshop", typeof(WorkshopInteractable), basePos + new Vector3(3*spacing,0,-2.5f), Color.cyan));
        created.Add(CreateInteractable("interactable_bonfire", typeof(BonfireInteractable), basePos + new Vector3(3*spacing-1.5f,0,0), new Color(1f,0.5f,0f)));

        // --- Begin new logic for reliability and idempotency ---
        int shopsAdded = 0, collidersAdded = 0, repositioned = 0;
        var spawnRoot = registry.SpawnRoot;
        if (spawnRoot != null)
        {
            // Collect merchant candidates robustly (TownKeyTag with merchant_ OR any MerchantShop)
            var candidates = new System.Collections.Generic.HashSet<GameObject>();

            var tags = spawnRoot.GetComponentsInChildren<Game.Town.TownKeyTag>(true);
            foreach (var t in tags)
            {
                if (t == null) continue;
                if (string.IsNullOrEmpty(t.Key)) continue;
                if (t.Key.StartsWith("merchant_"))
                    candidates.Add(t.gameObject);
            }

            var shops = spawnRoot.GetComponentsInChildren<Abyss.Shop.MerchantShop>(true);
            foreach (var s in shops)
            {
                if (s == null) continue;
                candidates.Add(s.gameObject);
            }

            foreach (var go in candidates)
            {
                if (go == null) continue;
                var tag = go.GetComponent<Game.Town.TownKeyTag>();
                string key = tag != null ? tag.Key : string.Empty;

                // Ensure MerchantShop component
                if (go.GetComponent<Abyss.Shop.MerchantShop>() == null)
                {
                    go.AddComponent<Abyss.Shop.MerchantShop>();
                    shopsAdded++;
                }

                // Ensure collider
                var col = go.GetComponent<Collider>();
                if (col == null)
                {
                    var box = go.AddComponent<BoxCollider>();
                    box.isTrigger = false;
                    box.center = new Vector3(0f, 1f, 0f);
                    box.size = new Vector3(1.2f, 2.0f, 1.2f);
                    collidersAdded++;
                }
                else
                {
                    col.isTrigger = false;
                    if (col is BoxCollider b)
                    {
                        if (b.size == Vector3.zero)
                            b.size = new Vector3(1.2f, 2.0f, 1.2f);
                        if (b.center == Vector3.zero)
                            b.center = new Vector3(0f, 1f, 0f);
                    }
                }

                // Anchor lookup
                var anchor = FindAnchorFor(key, go.name);
                if (anchor != null && anchor != go.transform)
                {
                    go.transform.position = anchor.position;
                    repositioned++;
                }

                // Grounding: raycast down to find floor, fallback to 1.0
                var pos = go.transform.position;
                RaycastHit hit;
                float desiredY = pos.y;
                if (Physics.Raycast(new Vector3(pos.x, pos.y + 10f, pos.z), Vector3.down, out hit, 50f))
                {
                    desiredY = hit.point.y;
                }
                else
                {
                    desiredY = 1.0f;
                }

                // Apply world-space clamp
                var beforeY = go.transform.position.y;
                if (desiredY > 1.05f || desiredY < 0.95f)
                {
                    var p = go.transform.position;
                    p.y = desiredY;
                    go.transform.position = p;
                    repositioned++;
                }

                // Ensure a lightweight runtime clamp component is present for late fixes
                if (go.GetComponent<Abyss.Town.TownWorldYClamp>() == null)
                    go.AddComponent<Abyss.Town.TownWorldYClamp>();
            }
        }
        #if UNITY_EDITOR
            // Robust anchor lookup for TownKey objects
            Transform FindAnchorFor(string townKey, string spawnedName)
            {
                // A) Try exact GameObject name match in scene
                string[] tryNames = new string[] {
                    spawnedName + "NPC",
                    spawnedName.Replace(" [TownKey:" + townKey + "]", "") + "NPC"
                };
                foreach (var n in tryNames)
                {
                    var go = GameObject.Find(n);
                    if (go != null) return go.transform;
                }

                // B) If merchant_*, try common NPC names
                if (townKey.StartsWith("merchant_"))
                {
                    string tail = townKey.Substring("merchant_".Length);
                    string[] merchantNames = new string[] {
                        "WeaponsGearMerchantNPC",
                        "ConsumablesMerchantNPC",
                        "SkillingSuppliesMerchantNPC",
                        "WorkshopMerchantNPC"
                    };
                    foreach (var m in merchantNames)
                    {
                        if (m.ToLower().Contains(tail.ToLower()))
                        {
                            var go = GameObject.Find(m);
                            if (go != null) return go.transform;
                        }
                    }
                }

                // C) Fallback: search all Transforms for name containing key tail and "NPC"
                string keyTail = townKey.Contains("_") ? townKey.Substring(townKey.IndexOf('_') + 1) : townKey;
                #if UNITY_2023_2_OR_NEWER
                foreach (var t in GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                #else
                foreach (var t in GameObject.FindObjectsOfType<Transform>())
                #endif
                {
                    if (t == null) continue;
                    string n = t.name.ToLower();
                    if (n.Contains(keyTail.ToLower()) && n.Contains("npc"))
                        return t;
                }
                return null;
            }
        #endif
        // --- End new logic ---

        SetupSimpleInteractPopup();
        SetupPlayerInteraction();
        Debug.Log($"[AI Assistant] Town setup complete: shopsAdded={shopsAdded}, collidersAdded={collidersAdded}, repositioned={repositioned}.");

        // Schedule a second pass next frame to re-clamp any merchants affected by other startup scripts
        if (Application.isPlaying)
            StartCoroutine(ReclampSpawnRootNextFrame(spawnRoot));

#if UNITY_EDITOR
        UnityEditor.Selection.objects = created.ToArray();
#endif
    }

    private System.Collections.IEnumerator ReclampSpawnRootNextFrame(Transform spawnRoot)
    {
        if (spawnRoot == null) yield break;

        int totalReclamped = 0;
        // Do three passes to handle late-moving scripts
        for (int pass = 0; pass < 3; pass++)
        {
            yield return null; // wait a frame between passes

            // Rebuild candidate list each pass
            var candidates = new System.Collections.Generic.HashSet<GameObject>();
            var tags = spawnRoot.GetComponentsInChildren<Game.Town.TownKeyTag>(true);
            foreach (var t in tags)
            {
                if (t == null) continue;
                if (string.IsNullOrEmpty(t.Key)) continue;
                if (t.Key.StartsWith("merchant_")) candidates.Add(t.gameObject);
            }
            var shops = spawnRoot.GetComponentsInChildren<Abyss.Shop.MerchantShop>(true);
            foreach (var s in shops) if (s != null) candidates.Add(s.gameObject);

            int reclamped = 0;
            foreach (var go in candidates)
            {
                if (go == null) continue;
                var p = go.transform.position;
                if (p.y > 1.05f || p.y < 0.95f)
                {
                    p.y = 1.0f;
                    go.transform.position = p;
                    reclamped++;
                }
            }

            totalReclamped += reclamped;
            if (reclamped > 0)
                Debug.Log($"[AIAssistantTownSetup] Reclamped {reclamped} merchants on pass {pass+1}.");
        }

        if (totalReclamped > 0)
            Debug.Log($"[AIAssistantTownSetup] Total reclamped merchants after 3 passes: {totalReclamped}.");
    }

    [ContextMenu("Nuke Town Spawns")]
    public void NukeTownSpawns()
    {
        TownRegistry.Instance.DestroyAllRegistered();
        Debug.Log("[AIAssistantTownSetup] All registered town spawns destroyed via TownRegistry.", this);
    }



    // Overloads matching call sites
    private GameObject CreateMerchant(string key, System.Type componentType, Vector3 position, Color debugColor)
    {
        if (TownRegistry.Instance.TryGet(key, out var existing) && existing != null)
            return existing;

        var go = new GameObject(componentType.Name);
        go.transform.position = position;
        go.AddComponent(componentType);

        if (createDebugMarkers)
        {
            // Add debug marker (sphere, no collider)
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "DebugMarker";
            marker.transform.SetParent(go.transform, false);
            marker.transform.localPosition = Vector3.zero;
            marker.transform.localScale = Vector3.one * 1.5f;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = debugColor;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", debugColor * 2f);
        #if UNITY_EDITOR
            mat.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        #endif
            marker.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(marker.GetComponent<Collider>());
        }

        // Always use the registered object
        var registered = TownRegistry.Instance.RegisterOrKeep(key, go);
        RemoveDebugMarkerChildren(registered);
        return registered;
    }

    private GameObject CreateInteractable(string key, System.Type componentType, Vector3 position, Color debugColor)
    {
        if (TownRegistry.Instance.TryGet(key, out var existing) && existing != null)
            return existing;

        var go = new GameObject(componentType.Name);
        go.transform.position = position;
        go.AddComponent(componentType);

        if (createDebugMarkers)
        {
            // Add debug marker (sphere, no collider)
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "DebugMarker";
            marker.transform.SetParent(go.transform, false);
            marker.transform.localPosition = Vector3.zero;
            marker.transform.localScale = Vector3.one * 1.5f;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = debugColor;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", debugColor * 2f);
        #if UNITY_EDITOR
            mat.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        #endif
            marker.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(marker.GetComponent<Collider>());
        }

        // Always use the registered object
        var registered = TownRegistry.Instance.RegisterOrKeep(key, go);
        RemoveDebugMarkerChildren(registered);
        return registered;
    }

    private static void RemoveDebugMarkerChildren(GameObject root)
    {
        if (root == null) return;

        // The old debug marker was a child named "Sphere"; the new one is "DebugMarker".
        for (int i = root.transform.childCount - 1; i >= 0; i--)
        {
            var c = root.transform.GetChild(i);
            if (c == null) continue;
            if (!string.Equals(c.name, "Sphere", System.StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(c.name, "DebugMarker", System.StringComparison.OrdinalIgnoreCase))
                continue;

            // Only delete if it looks like a pure visual marker.
            if (c.GetComponent<MeshRenderer>() == null && c.GetComponent<Renderer>() == null) continue;

            #if UNITY_EDITOR
            if (!Application.isPlaying) Object.DestroyImmediate(c.gameObject);
            else Object.Destroy(c.gameObject);
            #else
            Object.Destroy(c.gameObject);
            #endif
        }
    }

    private void SetupSimpleInteractPopup()
    {
        var popup = Object.FindFirstObjectByType<SimpleInteractPopup>(FindObjectsInactive.Include);
        if (popup != null) return;

        var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null)
        {
            var canvasGo = new GameObject("SimpleInteractCanvas");
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
        }

        var popupRoot = new GameObject("PopupRoot");
        popupRoot.transform.SetParent(canvas.transform, false);
        var rect = popupRoot.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(400, 100);
        var img = popupRoot.AddComponent<Image>();
        img.color = new Color(0,0,0,0.7f);
        popupRoot.SetActive(false);

        var popupTextGo = new GameObject("PopupText");
        popupTextGo.transform.SetParent(popupRoot.transform, false);
        var text = popupTextGo.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 28;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        var textRect = popupTextGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var popupScript = canvas.gameObject.AddComponent<SimpleInteractPopup>();
        popupScript.popupRoot = popupRoot;
        popupScript.popupText = text;
    }

    private void SetupPlayerInteraction()
    {
        var player = GameObject.FindWithTag("Player");
        if (player == null) return;
        if (player.GetComponent<PlayerInteraction>() == null)
            player.AddComponent<PlayerInteraction>();
    }
#endif
}
