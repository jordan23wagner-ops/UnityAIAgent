using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

namespace Abyss.Shop
{
    /// <summary>
    /// Runtime hover highlight: only highlights door click targets (not whole buildings).
    /// </summary>
    public sealed class MerchantDoorHoverHighlighter : MonoBehaviour
    {
        private Camera _cam;
        private MerchantDoorClickTarget _current;

        private TextMeshPro _label;
        private static readonly Color LabelColor = Color.blue;
        [SerializeField] private float labelFontSize = 14f;

        private void Awake()
        {
            _cam = Camera.main;
            if (_cam == null) _cam = FindAnyObjectByType<Camera>();
        }

        private void Update()
        {
            // If the shop UI is open, don't highlight anything.
            if (MerchantShopUI.IsOpen)
            {
                Clear();
                return;
            }

            // If pointer is over UI, don't highlight world.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                Clear();
                return;
            }

            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            var ray = _cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, 500f))
            {
                Clear();
                return;
            }

            MerchantDoorClickTarget target = null;
            if (hit.collider != null)
            {
                // Prefer direct component on collider object.
                target = hit.collider.GetComponent<MerchantDoorClickTarget>();
                if (target == null)
                    target = hit.collider.GetComponentInParent<MerchantDoorClickTarget>();
            }
            if (target == _current) return;

            if (_current != null)
                _current.SetHighlighted(false);

            _current = target;

            if (_current != null)
            {
                _current.SetHighlighted(true);
                ShowLabelFor(_current);
            }
            else
            {
                HideLabel();
            }
        }

        private void Clear()
        {
            if (_current != null)
            {
                _current.SetHighlighted(false);
                _current = null;
            }

            HideLabel();
        }

        private void EnsureLabel()
        {
            if (_label != null) return;

            var go = new GameObject("MerchantHoverLabel");
            go.transform.SetParent(transform, false);
            _label = go.AddComponent<TextMeshPro>();
            _label.text = string.Empty;
            _label.fontSize = labelFontSize;
            _label.alignment = TextAlignmentOptions.Center;
            _label.color = LabelColor;
            _label.textWrappingMode = TextWrappingModes.NoWrap;
            _label.gameObject.SetActive(false);
        }

        private void ShowLabelFor(MerchantDoorClickTarget target)
        {
            EnsureLabel();

            string name = target != null ? target.GetDisplayName() : string.Empty;
            if (string.IsNullOrWhiteSpace(name))
                name = "Shop";

            _label.text = name;

            if (target != null && target.TryGetBounds(out var b))
            {
                var p = b.center;
                p.y = b.max.y + 0.6f;
                _label.transform.position = p;
            }

            _label.gameObject.SetActive(true);
        }

        private void HideLabel()
        {
            if (_label != null)
                _label.gameObject.SetActive(false);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var existing = FindAnyObjectByType<MerchantDoorHoverHighlighter>();
            if (existing != null) return;

            var go = new GameObject("MerchantDoorHoverHighlighter");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            go.AddComponent<MerchantDoorHoverHighlighter>();
        }
    }
}
