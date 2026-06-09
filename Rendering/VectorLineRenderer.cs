using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using VectorBreakout.Game;

namespace VectorBreakout.Rendering;

public sealed class VectorLineRenderer : IDisposable
{
    private const int ShimmerColorCount = 4;

    private readonly GraphicsDevice _graphicsDevice;
    private readonly BasicEffect _effect;
    private readonly List<LineVertex> _lineVertices = new();
    private readonly List<LineVertex> _alphaLineVertices = new();
    private readonly List<LineVertex> _solidFillBatch = new();
    private readonly List<LineVertex>[] _fillBatches;
    private Vector2 _screenOffset;

    public VectorLineRenderer(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
        _effect = new BasicEffect(_graphicsDevice)
        {
            VertexColorEnabled = true,
            TextureEnabled = false,
            LightingEnabled = false,
        };

        _fillBatches = new List<LineVertex>[ShimmerColorCount];
        for (int i = 0; i < ShimmerColorCount; i++)
        {
            _fillBatches[i] = new List<LineVertex>();
        }
    }

    public void ResizeViewport(int width, int height)
    {
        _effect.World = Matrix.Identity;
        _effect.View = Matrix.Identity;
        _effect.Projection = Matrix.CreateOrthographicOffCenter(0f, width, height, 0f, 0f, 1f);
    }

    public void BeginFrame()
    {
        _lineVertices.Clear();
        _alphaLineVertices.Clear();
        _solidFillBatch.Clear();
        for (int i = 0; i < ShimmerColorCount; i++)
        {
            _fillBatches[i].Clear();
        }
    }

    public void AddSolidPolygonFill(IReadOnlyList<Vector2> outline, Vector2 fanOrigin, Color fillColor)
    {
        if (outline.Count < 3)
        {
            return;
        }

        int count = outline.Count;
        if (outline[count - 1] == outline[0])
        {
            count--;
        }

        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;
            Vector2 p0 = outline[i];
            Vector2 p1 = outline[next];

            _solidFillBatch.Add(new LineVertex(OffsetPoint(fanOrigin), fillColor));
            _solidFillBatch.Add(new LineVertex(OffsetPoint(p0), fillColor));
            _solidFillBatch.Add(new LineVertex(OffsetPoint(p1), fillColor));
        }
    }

    public void SetScreenOffset(Vector2 offset)
    {
        _screenOffset = offset;
    }

    private Vector2 OffsetPoint(Vector2 point) => point + _screenOffset;

    public void AddPolygonFill(
        IReadOnlyList<Vector2> outline,
        Vector2 fanOrigin,
        Color fillColor,
        Color highlightColor,
        int shimmerColorIndex,
        float totalTime,
        Vector2 playfieldCenter)
    {
        if (outline.Count < 3)
        {
            return;
        }

        shimmerColorIndex = System.Math.Clamp(shimmerColorIndex, 0, ShimmerColorCount - 1);
        List<LineVertex> batch = _fillBatches[shimmerColorIndex];

        int count = outline.Count;
        if (outline[count - 1] == outline[0])
        {
            count--;
        }

        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;
            Vector2 p0 = outline[i];
            Vector2 p1 = outline[next];

            float shineOrigin = BrickColorShimmer.ComputeShine(fanOrigin, playfieldCenter, totalTime, shimmerColorIndex);
            float shine0 = BrickColorShimmer.ComputeShine(p0, playfieldCenter, totalTime, shimmerColorIndex);
            float shine1 = BrickColorShimmer.ComputeShine(p1, playfieldCenter, totalTime, shimmerColorIndex);

            Color cOrigin = BrickColorShimmer.ApplyFillShine(fillColor, highlightColor, shineOrigin);
            Color c0 = BrickColorShimmer.ApplyFillShine(fillColor, highlightColor, shine0);
            Color c1 = BrickColorShimmer.ApplyFillShine(fillColor, highlightColor, shine1);

            batch.Add(new LineVertex(OffsetPoint(fanOrigin), cOrigin));
            batch.Add(new LineVertex(OffsetPoint(p0), c0));
            batch.Add(new LineVertex(OffsetPoint(p1), c1));
        }
    }

    public void AddLine(Vector2 start, Vector2 end, Color color)
    {
        _lineVertices.Add(new LineVertex(OffsetPoint(start), color));
        _lineVertices.Add(new LineVertex(OffsetPoint(end), color));
    }

    public void AddAlphaLine(Vector2 start, Vector2 end, Color color)
    {
        _alphaLineVertices.Add(new LineVertex(OffsetPoint(start), color));
        _alphaLineVertices.Add(new LineVertex(OffsetPoint(end), color));
    }

    public void AddAlphaPolyline(IReadOnlyList<Vector2> points, Color color, bool closed = false)
    {
        if (points.Count < 2)
        {
            return;
        }

        for (int i = 0; i < points.Count - 1; i++)
        {
            AddAlphaLine(points[i], points[i + 1], color);
        }

        if (closed)
        {
            AddAlphaLine(points[^1], points[0], color);
        }
    }

    public void AddPolyline(IReadOnlyList<Vector2> points, Color color, bool closed = false)
    {
        if (points.Count < 2)
        {
            return;
        }

        for (int i = 0; i < points.Count - 1; i++)
        {
            AddLine(points[i], points[i + 1], color);
        }

        if (closed)
        {
            AddLine(points[^1], points[0], color);
        }
    }

    public void Draw(float glowScale = 1.8f, Effect? brickShimmerEffect = null, float totalTime = 0f, Vector2 playfieldCenter = default)
    {
        var previousBlendState = _graphicsDevice.BlendState;
        var previousDepthState = _graphicsDevice.DepthStencilState;
        var previousRasterizerState = _graphicsDevice.RasterizerState;

        DrawSolidFills();
        DrawAlphaLines();
        DrawFillBatches(brickShimmerEffect, totalTime, playfieldCenter);

        if (_lineVertices.Count < 2)
        {
            _graphicsDevice.BlendState = previousBlendState;
            _graphicsDevice.DepthStencilState = previousDepthState;
            _graphicsDevice.RasterizerState = previousRasterizerState;
            return;
        }

        _graphicsDevice.BlendState = BlendState.Additive;
        foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();

            DrawCurrentLinesScaled(0.28f * glowScale);
            DrawCurrentLines();
        }

        _graphicsDevice.BlendState = previousBlendState;
        _graphicsDevice.DepthStencilState = previousDepthState;
        _graphicsDevice.RasterizerState = previousRasterizerState;
    }

    private void DrawSolidFills()
    {
        if (_solidFillBatch.Count < 3)
        {
            return;
        }

        var previousBlendState = _graphicsDevice.BlendState;
        var previousDepthState = _graphicsDevice.DepthStencilState;
        var previousRasterizerState = _graphicsDevice.RasterizerState;

        _graphicsDevice.BlendState = BlendState.AlphaBlend;
        _graphicsDevice.DepthStencilState = DepthStencilState.None;
        _graphicsDevice.RasterizerState = RasterizerState.CullNone;

        foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            DrawTriangleBatch(_solidFillBatch);
        }

        _graphicsDevice.BlendState = previousBlendState;
        _graphicsDevice.DepthStencilState = previousDepthState;
        _graphicsDevice.RasterizerState = previousRasterizerState;
    }

    private void DrawAlphaLines()
    {
        if (_alphaLineVertices.Count < 2)
        {
            return;
        }

        var previousBlendState = _graphicsDevice.BlendState;
        var previousDepthState = _graphicsDevice.DepthStencilState;
        var previousRasterizerState = _graphicsDevice.RasterizerState;

        _graphicsDevice.BlendState = BlendState.AlphaBlend;
        _graphicsDevice.DepthStencilState = DepthStencilState.None;
        _graphicsDevice.RasterizerState = RasterizerState.CullNone;

        var vertices = new LineVertex[_alphaLineVertices.Count];
        _alphaLineVertices.CopyTo(vertices, 0);

        foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, vertices, 0, vertices.Length / 2);
        }

        _graphicsDevice.BlendState = previousBlendState;
        _graphicsDevice.DepthStencilState = previousDepthState;
        _graphicsDevice.RasterizerState = previousRasterizerState;
    }

    private void DrawFillBatches(Effect? brickShimmerEffect, float totalTime, Vector2 playfieldCenter)
    {
        Matrix worldViewProjection = _effect.World * _effect.View * _effect.Projection;
        _graphicsDevice.BlendState = BlendState.AlphaBlend;
        _graphicsDevice.DepthStencilState = DepthStencilState.None;
        _graphicsDevice.RasterizerState = RasterizerState.CullNone;

        for (int colorIndex = 0; colorIndex < ShimmerColorCount; colorIndex++)
        {
            List<LineVertex> batch = _fillBatches[colorIndex];
            if (batch.Count < 3)
            {
                continue;
            }

            if (brickShimmerEffect != null)
            {
                Color highlight = BrickColorShimmer.Colors[colorIndex];
                Vector3 highlightRgb = highlight.ToVector3();

                brickShimmerEffect.Parameters["WorldViewProjection"]?.SetValue(worldViewProjection);
                brickShimmerEffect.Parameters["Center"]?.SetValue(playfieldCenter);
                brickShimmerEffect.Parameters["HighlightColor"]?.SetValue(highlightRgb);
                brickShimmerEffect.Parameters["Time"]?.SetValue(totalTime);
                brickShimmerEffect.Parameters["Interval"]?.SetValue(BrickColorShimmer.ShimmerInterval);
                brickShimmerEffect.Parameters["PhaseOffset"]?.SetValue(BrickColorShimmer.PhaseOffsets[colorIndex]);

                foreach (EffectPass pass in brickShimmerEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    DrawTriangleBatch(batch);
                }
            }
            else
            {
                foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    DrawTriangleBatch(batch);
                }
            }
        }
    }

    private void DrawTriangleBatch(List<LineVertex> batch)
    {
        var vertices = new LineVertex[batch.Count];
        batch.CopyTo(vertices, 0);
        _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, vertices.Length / 3);
    }

    private void DrawCurrentLines()
    {
        var vertices = new LineVertex[_lineVertices.Count];
        for (int i = 0; i < _lineVertices.Count; i++)
        {
            LineVertex source = _lineVertices[i];
            vertices[i] = new LineVertex(new Vector2(source.Position.X, source.Position.Y), source.Color);
        }

        _graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, vertices, 0, vertices.Length / 2);
    }

    private void DrawCurrentLinesScaled(float brightness)
    {
        var vertices = new LineVertex[_lineVertices.Count];
        for (int i = 0; i < _lineVertices.Count; i++)
        {
            LineVertex source = _lineVertices[i];
            Color color = source.Color * brightness;
            vertices[i] = new LineVertex(new Vector2(source.Position.X, source.Position.Y), color);
        }

        _graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, vertices, 0, vertices.Length / 2);
    }

    public void Dispose()
    {
        _effect.Dispose();
    }
}
