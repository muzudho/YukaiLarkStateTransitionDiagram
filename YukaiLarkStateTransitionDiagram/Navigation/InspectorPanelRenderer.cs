namespace YukaiLarkStateTransitionDiagram.Navigation;

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

/// <summary>
/// 画面右上のインスペクターパネルの描画
/// </summary>
public sealed class InspectorPanelRenderer : IDisposable
{
    private const int MinimumVisibleWidth = 560;
    private const int PanelWidth = 290;
    private const int PanelHeight = 96;
    private const int PanelRightMargin = 12;
    private const int PanelTop = 70;

    private readonly GraphicsDevice _graphicsDevice;
    private readonly SpriteBatch _spriteBatch;
    private readonly Texture2D _pixel;
    private readonly Dictionary<string, Texture2D> _uiTextTextureCache = new();

    public InspectorPanelRenderer(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, Texture2D pixel)
    {
        _graphicsDevice = graphicsDevice;
        _spriteBatch = spriteBatch;
        _pixel = pixel;
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
        string fileSummary,
        BoardTheme theme)
    {
        if (viewport.Width < MinimumVisibleWidth)
        {
            return;
        }

        var x = viewport.Width - PanelWidth - PanelRightMargin;
        var bounds = new Rectangle(x, PanelTop, PanelWidth, PanelHeight);
        _spriteBatch.Draw(_pixel, bounds, theme.PanelBackgroundColor);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), theme.PanelTopEdgeColor);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), theme.PanelBottomEdgeColor);

        DrawUiText($"状態: {nodeCount}    遷移: {transitionCount}", new Vector2(x + 12, bounds.Y + 10), theme.PanelPrimaryTextColor, 16, true);
        DrawUiText(selectionSummary, new Vector2(x + 12, bounds.Y + 36), theme.PanelSecondaryTextColor, 15, false);
        DrawUiText(fileSummary, new Vector2(x + 12, bounds.Y + 62), theme.PanelMutedTextColor, 14, false);
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

