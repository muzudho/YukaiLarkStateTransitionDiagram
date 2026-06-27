namespace YukaiLarkStateTransitionDiagram.MiniMap;

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public sealed class MiniMapRenderer
{
    private readonly SpriteBatch _spriteBatch;
    private readonly Texture2D _pixel;
    private readonly PrimitiveRenderer _primitiveRenderer;

    public MiniMapRenderer(SpriteBatch spriteBatch, Texture2D pixel, PrimitiveRenderer primitiveRenderer)
    {
        _spriteBatch = spriteBatch;
        _pixel = pixel;
        _primitiveRenderer = primitiveRenderer;
    }

    public void Draw(
        Rectangle bounds,
        IReadOnlyList<DiagramNode> nodes,
        Viewport viewport,
        Vector2 cameraOffset,
        BoardTheme theme,
        MiniMapLayout? layout = null)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        layout ??= MiniMapLayout.Create(bounds, nodes, viewport, cameraOffset);
        _spriteBatch.Draw(_pixel, bounds, WithAlpha(Blend(theme.PanelBackgroundColor, theme.BackgroundColor, 0.48f), 238));
        DrawRectangleOutline(bounds, WithAlpha(theme.PanelTopEdgeColor, 205), 1);

        foreach (var node in nodes)
        {
            DrawNode(layout, node, theme);
        }

        var view = layout.GetViewportRectangle(viewport, cameraOffset);
        FillClamped(bounds, view, WithAlpha(theme.SelectedTransitionLineColor, 34));
        DrawClampedOutline(bounds, view, WithAlpha(theme.SelectedTransitionLineColor, 232), 2);
    }

    private void DrawNode(MiniMapLayout layout, DiagramNode node, BoardTheme theme)
    {
        var position = layout.WorldToMap(node.Position);
        var radius = Math.Clamp(node.Radius / Math.Max(1f, layout.WorldBounds.Width) * layout.MapBounds.Width, 3.5f, 8f);
        var fill = node.Kind switch
        {
            NodeKind.StartMarker => theme.PinColor,
            NodeKind.EndMarker => theme.SelectedTransitionLineColor,
            _ => theme.TransitionHandleColor
        };
        _primitiveRenderer.DrawCircle(position, radius, WithAlpha(fill, 225));
        _primitiveRenderer.DrawCircleOutline(position, radius + 1f, WithAlpha(theme.PanelBottomEdgeColor, 205), 1f);
    }

    private void FillClamped(Rectangle clipBounds, Rectangle rectangle, Color color)
    {
        var clipped = Rectangle.Intersect(clipBounds, rectangle);
        if (clipped.Width > 0 && clipped.Height > 0)
        {
            _spriteBatch.Draw(_pixel, clipped, color);
        }
    }

    private void DrawClampedOutline(Rectangle clipBounds, Rectangle rectangle, Color color, int thickness)
    {
        var clipped = Rectangle.Intersect(clipBounds, rectangle);
        if (clipped.Width > 0 && clipped.Height > 0)
        {
            DrawRectangleOutline(clipped, color, thickness);
        }
    }

    private void DrawRectangleOutline(Rectangle rectangle, Color color, int thickness)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, thickness), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Bottom - thickness, rectangle.Width, thickness), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Y, thickness, rectangle.Height), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.Right - thickness, rectangle.Y, thickness, rectangle.Height), color);
    }

    private static Color WithAlpha(Color color, byte alpha)
        => new(color.R, color.G, color.B, alpha);

    private static Color Blend(Color from, Color to, float amount)
    {
        amount = MathHelper.Clamp(amount, 0f, 1f);
        return new Color(
            (byte)MathHelper.Lerp(from.R, to.R, amount),
            (byte)MathHelper.Lerp(from.G, to.G, amount),
            (byte)MathHelper.Lerp(from.B, to.B, amount),
            (byte)MathHelper.Lerp(from.A, to.A, amount));
    }
}