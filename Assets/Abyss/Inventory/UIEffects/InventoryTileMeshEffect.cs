using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Inventory.UIEffects
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Graphic))]
    public sealed class InventoryTileMeshEffect : BaseMeshEffect
    {
        [Header("Gradient")]
        [SerializeField] private Color topColor = new(0.30f, 0.30f, 0.30f, 1f);
        [SerializeField] private Color bottomColor = new(0.40f, 0.40f, 0.40f, 1f);

        [Header("Inner Shadow")]
        [SerializeField] private bool innerShadowEnabled = true;
        [SerializeField] private Color innerShadowColor = new(0f, 0f, 0f, 0.22f);
        [SerializeField] private float innerShadowSize = 4f;

        private static readonly List<UIVertex> _verts = new(256);

        public void SetGradient(Color top, Color bottom)
        {
            topColor = top;
            bottomColor = bottom;
            if (graphic != null)
                graphic.SetVerticesDirty();
        }

        public void SetInnerShadow(bool enabled, Color color, float size)
        {
            innerShadowEnabled = enabled;
            innerShadowColor = color;
            innerShadowSize = size;
            if (graphic != null)
                graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh == null)
                return;

            _verts.Clear();
            vh.GetUIVertexStream(_verts);
            if (_verts.Count == 0)
                return;

            var rect = graphic != null && graphic.rectTransform != null
                ? graphic.rectTransform.rect
                : default;

            float yMin = rect.yMin;
            float yMax = rect.yMax;
            float height = Mathf.Max(1f, yMax - yMin);

            // Apply vertical gradient by vertex Y.
            for (int i = 0; i < _verts.Count; i++)
            {
                var v = _verts[i];
                float t = Mathf.Clamp01((v.position.y - yMin) / height);
                var c = Color.Lerp(bottomColor, topColor, t);
                v.color = c;
                _verts[i] = v;
            }

            // Add simple inner shadow quads (single strip per edge).
            if (innerShadowEnabled && innerShadowColor.a > 0.001f)
            {
                float xMin = rect.xMin;
                float xMax = rect.xMax;
                float width = xMax - xMin;
                float s = Mathf.Clamp(innerShadowSize, 0f, Mathf.Min(width, height) * 0.5f);

                if (s > 0.01f)
                {
                    // Left
                    AddQuadTriangleStream(_verts, rect, xMin, yMin, xMin + s, yMax, innerShadowColor);
                    // Right
                    AddQuadTriangleStream(_verts, rect, xMax - s, yMin, xMax, yMax, innerShadowColor);
                    // Top
                    AddQuadTriangleStream(_verts, rect, xMin, yMax - s, xMax, yMax, innerShadowColor);
                    // Bottom
                    AddQuadTriangleStream(_verts, rect, xMin, yMin, xMax, yMin + s, innerShadowColor);
                }
            }

            vh.Clear();
            vh.AddUIVertexTriangleStream(_verts);
        }

        private static void AddQuadTriangleStream(List<UIVertex> stream, Rect rect, float x0, float y0, float x1, float y1, Color color)
        {
            float u0 = Mathf.InverseLerp(rect.xMin, rect.xMax, x0);
            float u1 = Mathf.InverseLerp(rect.xMin, rect.xMax, x1);
            float v0 = Mathf.InverseLerp(rect.yMin, rect.yMax, y0);
            float v1 = Mathf.InverseLerp(rect.yMin, rect.yMax, y1);

            var a = UIVertex.simpleVert;
            a.position = new Vector3(x0, y0, 0f);
            a.uv0 = new Vector2(u0, v0);
            a.color = color;

            var b = UIVertex.simpleVert;
            b.position = new Vector3(x0, y1, 0f);
            b.uv0 = new Vector2(u0, v1);
            b.color = color;

            var c = UIVertex.simpleVert;
            c.position = new Vector3(x1, y1, 0f);
            c.uv0 = new Vector2(u1, v1);
            c.color = color;

            var d = UIVertex.simpleVert;
            d.position = new Vector3(x1, y0, 0f);
            d.uv0 = new Vector2(u1, v0);
            d.color = color;

            // Two triangles: a-b-c and c-d-a
            stream.Add(a);
            stream.Add(b);
            stream.Add(c);

            stream.Add(c);
            stream.Add(d);
            stream.Add(a);
        }
    }
}
