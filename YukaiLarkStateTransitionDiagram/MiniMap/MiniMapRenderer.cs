namespace YukaiLarkStateTransitionDiagram.MiniMap;

using System;
using System.Collections.Generic;
using YukaiLarkStateTransitionDiagram.Theme;
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
        float cameraZoom,
        BoardTheme theme,
        bool dimmed = false,
        MiniMapLayout? layout = null)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        layout ??= MiniMapLayout.Create(bounds, nodes, viewport, cameraOffset, cameraZoom);
        var panelBackground = dimmed
            ? WithAlpha(Blend(theme.PanelBackgroundColor, Color.Gray, 0.62f), 184)
            : WithAlpha(Blend(theme.PanelBackgroundColor, theme.BackgroundColor, 0.48f), 238);
        var edgeColor = dimmed
            ? WithAlpha(Blend(theme.PanelTopEdgeColor, Color.Gray, 0.68f), 150)
            : WithAlpha(theme.PanelTopEdgeColor, 205);
        _spriteBatch.Draw(_pixel, bounds, panelBackground);
        DrawRectangleOutline(bounds, edgeColor, 1);

        foreach (var node in nodes)
        {
            DrawNode(layout, node, theme, dimmed);
        }

        var view = layout.GetViewportRectangle(viewport, cameraOffset, cameraZoom);
        DrawClampedOutline(bounds, view, dimmed ? new Color(74, 74, 74, 170) : new Color(24, 28, 32, 190), 3);
        DrawClampedOutline(bounds, view, dimmed ? new Color(172, 172, 172, 150) : WithAlpha(theme.SelectedTransitionLineColor, 232), 1);
    }

    private void DrawNode(MiniMapLayout layout, DiagramNode node, BoardTheme theme, bool dimmed)
    {
        var position = layout.WorldToMap(node.Position);
        var radius = Math.Clamp(node.Radius / Math.Max(1f, layout.WorldBounds.Width) * layout.MapBounds.Width, 3.5f, 8f);
        var fill = node.Kind switch
        {
            NodeKind.StartMarker => theme.PinColor,
            NodeKind.EndMarker => theme.SelectedTransitionLineColor,
            _ => theme.TransitionHandleColor
        };
        if (dimmed)
        {
            fill = Blend(fill, Color.Gray, 0.68f);
        }

        _primitiveRenderer.DrawCircle(position, radius, WithAlpha(fill, dimmed ? (byte)150 : (byte)225));
        _primitiveRenderer.DrawCircleOutline(position, radius + 1f, dimmed ? new Color(112, 112, 112, 128) : WithAlpha(theme.PanelBottomEdgeColor, 205), 1f);
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
