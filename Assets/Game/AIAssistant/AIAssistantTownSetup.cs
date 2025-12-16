using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.UI;

public class AIAssistantTownSetup : MonoBehaviour
{
#if UNITY_EDITOR
    [ContextMenu("AI Assistant: Setup Town Merchants & Workshop")]
            public void SetupTown()
            {
            float y = 5f;
            Vector3 basePos = new Vector3(0, y, 0);
            float spacing = 5f;
            var created = new System.Collections.Generic.List<GameObject>();

            created.Add(CreateMerchant("WeaponsMerchantNPC", typeof(WeaponsGearMerchant), basePos + new Vector3(0,0,0), Color.red));
            created.Add(CreateMerchant("ConsumablesMerchantNPC", typeof(ConsumablesMerchant), basePos + new Vector3(spacing,0,0), Color.green));
            created.Add(CreateMerchant("SkillingSuppliesMerchantNPC", typeof(SkillingSuppliesMerchant), basePos + new Vector3(2*spacing,0,0), Color.blue));
            created.Add(CreateMerchant("WorkshopMerchantNPC", typeof(WorkshopMerchant), basePos + new Vector3(3*spacing,0,0), Color.yellow));

            created.Add(CreateInteractable("Forge", typeof(ForgeInteractable), basePos + new Vector3(3*spacing,0,2.5f), Color.gray));
            created.Add(CreateInteractable("SmithingStand", typeof(SmithingStandInteractable), basePos + new Vector3(3*spacing+1.5f,0,0), Color.magenta));
            created.Add(CreateInteractable("Workshop", typeof(WorkshopInteractable), basePos + new Vector3(3*spacing,0,-2.5f), Color.cyan));
            created.Add(CreateInteractable("Bonfire", typeof(BonfireInteractable), basePos + new Vector3(3*spacing-1.5f,0,0), new Color(1f,0.5f,0f)));

            SetupSimpleInteractPopup();
            SetupPlayerInteraction();
            Debug.Log("[AI Assistant] Town merchants, workshop, and UI set up at (0,5,0) with huge visible markers.");

        #if UNITY_EDITOR
            UnityEditor.Selection.objects = created.ToArray();
        #endif
            }

    private GameObject CreateMerchant(string name, System.Type script, Vector3 pos, Color color)
    {
        if (GameObject.Find(name) != null) return null;
        var go = new GameObject(name);
        go.transform.position = pos;
        go.layer = 0; // Default
        go.SetActive(true);
        go.AddComponent<BoxCollider>().isTrigger = true;
        go.AddComponent(script);
        var vis = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        vis.transform.SetParent(go.transform, false);
        vis.transform.localPosition = Vector3.zero;
        vis.transform.localScale = new Vector3(2.5f,5f,2.5f);
        var mat = vis.GetComponent<Renderer>().material;
        mat.color = color;
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", color * 2f);
        Object.DestroyImmediate(vis.GetComponent<Collider>());
        // Add floating label
        var label = new GameObject("Label");
        label.transform.SetParent(go.transform, false);
        label.transform.localPosition = new Vector3(0,3.5f,0);
        var tm = label.AddComponent<TextMesh>();
        tm.text = $"!!! {name} !!!";
        tm.fontSize = 96;
        tm.characterSize = 0.25f;
        tm.color = Color.white;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        Debug.Log($"[AI Assistant] Created {name} at {pos}");
        if (!go.activeInHierarchy)
            Debug.LogWarning($"[AI Assistant] {name} is not active in hierarchy!");
        return go;
    }

    private GameObject CreateInteractable(string name, System.Type script, Vector3 pos, Color color)
    {
        if (GameObject.Find(name) != null) return null;
        var go = new GameObject(name);
        go.transform.position = pos;
        go.layer = 0; // Default
        go.SetActive(true);
        go.AddComponent<BoxCollider>().isTrigger = true;
        go.AddComponent(script);
        var vis = GameObject.CreatePrimitive(PrimitiveType.Cube);
        vis.transform.SetParent(go.transform, false);
        vis.transform.localPosition = Vector3.zero;
        vis.transform.localScale = new Vector3(3f,3f,3f);
        var mat = vis.GetComponent<Renderer>().material;
        mat.color = color;
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", color * 2f);
        Object.DestroyImmediate(vis.GetComponent<Collider>());
        // Add floating label
        var label = new GameObject("Label");
        label.transform.SetParent(go.transform, false);
        label.transform.localPosition = new Vector3(0,2.5f,0);
        var tm = label.AddComponent<TextMesh>();
        tm.text = $"!!! {name} !!!";
        tm.fontSize = 96;
        tm.characterSize = 0.25f;
        tm.color = Color.white;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        Debug.Log($"[AI Assistant] Created {name} at {pos}");
        if (!go.activeInHierarchy)
            Debug.LogWarning($"[AI Assistant] {name} is not active in hierarchy!");
        return go;
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
            canvasGo.AddComponent<CanvasScaler>();
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
