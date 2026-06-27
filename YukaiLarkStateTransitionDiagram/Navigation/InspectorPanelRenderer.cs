namespace YukaiLarkStateTransitionDiagram.Navigation;

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using YukaiLarkStateTransitionDiagram.MiniMap;

/// <summary>
/// 画面右上のインスペクターパネルの描画
/// </summary>
public sealed class InspectorPanelRenderer : IDisposable
{
    private const int MinimumVisibleWidth = 560;
    private const int PanelWidth = 290;
    private const int PanelHeight = 218;
    private const int PanelRightMargin = 12;
    private const int PanelMinimumTop = 70;
    private const int MiniMapLeftPadding = 12;
    private const int MiniMapTop = 86;
    private const int MiniMapHeight = 118;

    private readonly GraphicsDevice _graphicsDevice;
    private readonly SpriteBatch _spriteBatch;
    private readonly Texture2D _pixel;
    private readonly MiniMapRenderer _miniMapRenderer;
    private readonly Dictionary<string, Texture2D> _uiTextTextureCache = new();

    public InspectorPanelRenderer(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, Texture2D pixel, PrimitiveRenderer primitiveRenderer)
    {
        _graphicsDevice = graphicsDevice;
        _spriteBatch = spriteBatch;
        _pixel = pixel;
        _miniMapRenderer = new MiniMapRenderer(spriteBatch, pixel, primitiveRenderer);
    }

    public void Dispose()
    {
        foreach (var texture in _uiTextTextureCache.Values)
        {
            texture.Dispose();
        }

        _uiTextTextureCache.Clear();
    }

    public void DrawInspectorPanel(
        Viewport viewport,
        int nodeCount,
        int transitionCount,
        string selectionSummary,
        IReadOnlyList<DiagramNode> nodes,
        Vector2 cameraOffset,
        BoardTheme theme)
    {
        if (viewport.Width < MinimumVisibleWidth)
        {
            return;
        }

        var bounds = GetPanelBounds(viewport);
        var x = bounds.X;
        _spriteBatch.Draw(_pixel, bounds, theme.PanelBackgroundColor);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), theme.PanelTopEdgeColor);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), theme.PanelBottomEdgeColor);

        DrawUiText($"状態: {nodeCount}    遷移: {transitionCount}", new Vector2(x + 12, bounds.Y + 10), theme.PanelPrimaryTextColor, 16, true);
        DrawUiText(selectionSummary, new Vector2(x + 12, bounds.Y + 36), theme.PanelSecondaryTextColor, 15, false);
        DrawUiText("全体図", new Vector2(x + 12, bounds.Y + 64), theme.PanelMutedTextColor, 13, true);
        _miniMapRenderer.Draw(GetMiniMapBounds(viewport), nodes, viewport, cameraOffset, theme);
    }

    public static bool TryGetPanelBounds(Viewport viewport, out Rectangle bounds)
    {
        if (viewport.Width < MinimumVisibleWidth)
        {
            bounds = Rectangle.Empty;
            return false;
        }

        bounds = GetPanelBounds(viewport);
        return true;
    }

    public static bool TryGetMiniMapBounds(Viewport viewport, out Rectangle bounds)
    {
        if (viewport.Width < MinimumVisibleWidth)
        {
            bounds = Rectangle.Empty;
            return false;
        }

        bounds = GetMiniMapBounds(viewport);
        return true;
    }

    private static Rectangle GetPanelBounds(Viewport viewport)
    {
        var x = viewport.Width - PanelWidth - PanelRightMargin;
        var y = Math.Max(PanelMinimumTop, (viewport.Height - PanelHeight) / 2);
        return new Rectangle(x, y, PanelWidth, PanelHeight);
    }

    private static Rectangle GetMiniMapBounds(Viewport viewport)
    {
        var panel = GetPanelBounds(viewport);
        return new Rectangle(panel.X + MiniMapLeftPadding, panel.Y + MiniMapTop, PanelWidth - MiniMapLeftPadding * 2, MiniMapHeight);
    }

    private float DrawUiText(string text, Vector2 position, Color color, float size, bool bold)
    {
        var texture = GetUiTextTexture(text, size, bold);
        _spriteBatch.Draw(texture, position, color);
        return position.X + texture.Width;
    }

    private Texture2D GetUiTextTexture(string text, float size, bool bold)
    {
        var cacheKey = $"ui|{size}|{bold}|{text}";
        if (_uiTextTextureCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var texture = TextRenderer.CreateUiTextTexture(_graphicsDevice, text, size, bold);
        _uiTextTextureCache[cacheKey] = texture;
        return texture;
    }
}
