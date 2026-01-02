using System;
using UnityEngine;

namespace Abyssbound.WorldInteraction
{
    public sealed class WorldHoverHighlighter : MonoBehaviour
    {
        [Header("Stability")]
        [SerializeField] private float switchDistanceEpsilon = 0.75f;
        [SerializeField] private float lostTargetGraceSeconds = 0.20f;

        [Header("Visuals")]
        [SerializeField] private Color highlightColor = new Color(1f, 0.85f, 0.25f, 1f);
        [SerializeField] private Vector3 labelOffset = new Vector3(0f, 0.6f, 0f);

        private WorldInteractable current;
        private float currentDistance;
        private float lastSeenTime;
        private Vector3 lastHitPoint;

        private MaterialPropertyBlock _mpb;

        private GameObject labelGO;
        private Component tmpComponent;
        private TextMesh textMesh;

        public WorldInteractable Current => current;

        private void OnEnable()
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
        }

        private void Awake()
        {
            EnsureLabelCreated();
            SetLabelVisible(false);
        }

        public void UpdateHoverCandidate(WorldInteractable candidate, Vector3 hitPoint, float hitDistance, Camera cam)
        {
            bool sawCandidate = candidate != null;
            if (sawCandidate)
            {
                lastSeenTime = Time.unscaledTime;
                lastHitPoint = hitPoint;
            }

            if (current == null)
            {
                if (sawCandidate)
                    SetCurrent(candidate, hitDistance, cam);

                UpdateLabel(cam);
                return;
            }

            if (sawCandidate)
            {
                if (candidate == current)
                {
                    currentDistance = hitDistance;
                }
                else
                {
                    // Switch only if the new target is meaningfully closer.
                    if (hitDistance + switchDistanceEpsilon < currentDistance)
                    {
                        SetCurrent(candidate, hitDistance, cam);
                    }
                }
            }
            else
            {
                // Lost target grace.
                if (Time.unscaledTime - lastSeenTime > lostTargetGraceSeconds)
                {
                    ClearCurrent();
                }
            }

            UpdateLabel(cam);
        }

        private void SetCurrent(WorldInteractable next, float distance, Camera cam)
        {
            if (next == current)
                return;

            ClearCurrent();

            current = next;
            currentDistance = distance;

            ApplyHighlight(current, enabled: true);
            EnsureLabelCreated();
            SetLabelText(current != null ? current.DisplayName : string.Empty);
            SetLabelVisible(true);

            UpdateLabel(cam);
        }

        private void ClearCurrent()
        {
            if (current != null)
            {
                ApplyHighlight(current, enabled: false);
            }

            current = null;
            currentDistance = float.PositiveInfinity;
            SetLabelVisible(false);
        }

        private void ApplyHighlight(WorldInteractable target, bool enabled)
        {
            if (target == null)
                return;

            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            var renderers = target.HighlightRenderers;
            if (renderers == null)
                return;

            _mpb.Clear();
            if (enabled)
            {
                _mpb.SetColor("_Color", highlightColor);
                _mpb.SetColor("_BaseColor", highlightColor);
                _mpb.SetColor("_EmissionColor", highlightColor);
            }

            var block = _mpb;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                r.SetPropertyBlock(block);
            }
        }

        private void EnsureLabelCreated()
        {
            if (labelGO != null)
                return;

            labelGO = new GameObject("WorldHoverLabel");
            labelGO.transform.SetParent(null);

            // Try TextMeshPro (world) via reflection; fallback to TextMesh.
            var tmpType = Type.GetType("TMPro.TextMeshPro, Unity.TextMeshPro");
            if (tmpType != null)
            {
                tmpComponent = labelGO.AddComponent(tmpType);
                TrySetProperty(tmpComponent, "text", string.Empty);
                TrySetProperty(tmpComponent, "fontSize", 2.2f);
                TrySetProperty(tmpComponent, "color", Color.white);
            }
            else
            {
                textMesh = labelGO.AddComponent<TextMesh>();
                textMesh.text = string.Empty;
                textMesh.fontSize = 48;
                textMesh.characterSize = 0.05f;
                textMesh.anchor = TextAnchor.MiddleCenter;
                textMesh.alignment = TextAlignment.Center;
                textMesh.color = Color.white;
            }
        }

        private void SetLabelText(string text)
        {
            if (tmpComponent != null)
            {
                TrySetProperty(tmpComponent, "text", text);
                return;
            }

            if (textMesh != null)
                textMesh.text = text;
        }

        private void SetLabelVisible(bool visible)
        {
            if (labelGO != null)
                labelGO.SetActive(visible);
        }

        private void UpdateLabel(Camera cam)
        {
            if (labelGO == null || !labelGO.activeSelf)
                return;

            if (current == null)
                return;

            var bounds = ComputeBounds(current);
            var pos = bounds.center;
            pos.y = bounds.max.y;
            pos += labelOffset;

            labelGO.transform.position = pos;

            if (cam != null)
            {
                var forward = cam.transform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude > 0.0001f)
                    labelGO.transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            }
        }

        private static Bounds ComputeBounds(WorldInteractable target)
        {
            var renderers = target.HighlightRenderers;
            bool hasBounds = false;
            Bounds b = default;

            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    if (r == null) continue;
                    if (!hasBounds)
                    {
                        b = r.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        b.Encapsulate(r.bounds);
                    }
                }
            }

            if (!hasBounds)
            {
                var any = target.GetComponentInChildren<Renderer>(true);
                if (any != null)
                    return any.bounds;

                return new Bounds(target.transform.position, Vector3.one);
            }

            return b;
        }

        private static void TrySetProperty(object obj, string propName, object value)
        {
            if (obj == null) return;
            try
            {
                var p = obj.GetType().GetProperty(propName);
                if (p != null && p.CanWrite)
                    p.SetValue(obj, value);
            }
            catch
            {
                // ignored
            }
        }
    }
}
