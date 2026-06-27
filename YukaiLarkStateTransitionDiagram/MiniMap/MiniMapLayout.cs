namespace YukaiLarkStateTransitionDiagram.MiniMap;

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public sealed class MiniMapLayout
{
    private const float ContentPadding = 14f;
    private const float EmptyWorldHalfSize = 420f;
    private const float MinimumWorldSize = 240f;

    private readonly Rectangle _mapBounds;
    private readonly MiniMapWorldBounds _worldBounds;
    private readonly float _scale;
    private readonly Vector2 _contentOffset;

    private MiniMapLayout(Rectangle mapBounds, MiniMapWorldBounds worldBounds, float scale, Vector2 contentOffset)
    {
        _mapBounds = mapBounds;
        _worldBounds = worldBounds;
        _scale = scale;
        _contentOffset = contentOffset;
    }

    public Rectangle MapBounds => _mapBounds;
    public MiniMapWorldBounds WorldBounds => _worldBounds;

    public static MiniMapLayout Create(Rectangle mapBounds, IReadOnlyList<DiagramNode> nodes, Viewport viewport, Vector2 cameraOffset)
    {
        var worldBounds = CreateWorldBounds(nodes, viewport, cameraOffset);
        var contentWidth = Math.Max(1f, mapBounds.Width - ContentPadding * 2f);
        var contentHeight = Math.Max(1f, mapBounds.Height - ContentPadding * 2f);
        var scale = Math.Min(contentWidth / worldBounds.Width, contentHeight / worldBounds.Height);
        var scaledSize = new Vector2(worldBounds.Width * scale, worldBounds.Height * scale);
        var contentOffset = new Vector2(
            mapBounds.X + (mapBounds.Width - scaledSize.X) / 2f,
            mapBounds.Y + (mapBounds.Height - scaledSize.Y) / 2f);

        return new MiniMapLayout(mapBounds, worldBounds, scale, contentOffset);
    }

    public Vector2 WorldToMap(Vector2 worldPosition)
        => _contentOffset + new Vector2(
            (worldPosition.X - _worldBounds.X) * _scale,
            (worldPosition.Y - _worldBounds.Y) * _scale);

    public Vector2 MapToWorld(Vector2 mapPosition)
        => new(
            _worldBounds.X + (mapPosition.X - _contentOffset.X) / _scale,
            _worldBounds.Y + (mapPosition.Y - _contentOffset.Y) / _scale);

    public Rectangle GetViewportRectangle(Viewport viewport, Vector2 cameraOffset)
    {
        var worldTopLeft = -cameraOffset;
        var worldBottomRight = new Vector2(viewport.Width, viewport.Height) - cameraOffset;
        var mapTopLeft = WorldToMap(worldTopLeft);
        var mapBottomRight = WorldToMap(worldBottomRight);
        return RectangleFromPoints(mapTopLeft, mapBottomRight);
    }

    private static MiniMapWorldBounds CreateWorldBounds(IReadOnlyList<DiagramNode> nodes, Viewport viewport, Vector2 cameraOffset)
    {
        var worldTopLeft = -cameraOffset;
        var worldBottomRight = new Vector2(viewport.Width, viewport.Height) - cameraOffset;
        var minX = worldTopLeft.X;
        var minY = worldTopLeft.Y;
        var maxX = worldBottomRight.X;
        var maxY = worldBottomRight.Y;

        foreach (var node in nodes)
        {
            var radius = Math.Max(node.Radius, DiagramNode.RadiusUnit);
            minX = Math.Min(minX, node.Position.X - radius);
            minY = Math.Min(minY, node.Position.Y - radius);
            maxX = Math.Max(maxX, node.Position.X + radius);
            maxY = Math.Max(maxY, node.Position.Y + radius);
        }

        if (nodes.Count == 0)
        {
            minX = Math.Min(minX, -EmptyWorldHalfSize);
            minY = Math.Min(minY, -EmptyWorldHalfSize);
            maxX = Math.Max(maxX, EmptyWorldHalfSize);
            maxY = Math.Max(maxY, EmptyWorldHalfSize);
        }

        var width = Math.Max(MinimumWorldSize, maxX - minX);
        var height = Math.Max(MinimumWorldSize, maxY - minY);
        var center = new Vector2((minX + maxX) / 2f, (minY + maxY) / 2f);
        return new MiniMapWorldBounds(center.X - width / 2f, center.Y - height / 2f, width, height);
    }

    private static Rectangle RectangleFromPoints(Vector2 first, Vector2 second)
    {
        var left = (int)MathF.Floor(MathF.Min(first.X, second.X));
        var top = (int)MathF.Floor(MathF.Min(first.Y, second.Y));
        var right = (int)MathF.Ceiling(MathF.Max(first.X, second.X));
        var bottom = (int)MathF.Ceiling(MathF.Max(first.Y, second.Y));
        return new Rectangle(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
    }
}
public readonly record struct MiniMapWorldBounds(float X, float Y, float Width, float Height);
