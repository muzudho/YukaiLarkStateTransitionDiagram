namespace YukaiLarkStateTransitionDiagram.Navigation;

using System;
using System.Collections.Generic;
using YukaiLarkStateTransitionDiagram.Theme;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

/// <summary>
/// 画面右上のインスペクターパネルの描画
/// </summary>
public sealed class InspectorPanelRenderer : IDisposable
{
    private const int MinimumVisibleWidth = 560;
    private const int PanelWidth = 290;
    private const int PanelMinimumHeight = 70;
    private const int PanelRightMargin = 12;
    private const int PanelMinimumTop = 86;
    private const int PanelBottomMargin = 194;
    private const int PanelHorizontalPadding = 12;
    private const int PanelTopPadding = 10;
    private const int PanelBottomPadding = 10;
    private const int LineAdvance = 26;
    private const int ApproximateLineHeight = 20;
    private const int DefaultLineCount = 2;

    private readonly GraphicsDevice _graphicsDevice;
    private readonly SpriteBatch _spriteBatch;
    private readonly Texture2D _pixel;
    private readonly Dictionary<string, Texture2D> _uiTextTextureCache = new();

    public InspectorPanelRenderer(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, Texture2D pixel, PrimitiveRenderer primitiveRenderer)
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
        InspectorPanelContent content,
        BoardTheme theme)
    {
        if (viewport.Width < MinimumVisibleWidth) return;
        if (content.Lines.Count == 0) return;

        var bounds = GetPanelBounds(viewport, content.Lines.Count);
        _spriteBatch.Draw(_pixel, bounds, theme.PanelBackgroundColor);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), theme.PanelTopEdgeColor);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), theme.PanelBottomEdgeColor);

        var linePosition = new Vector2(bounds.X + PanelHorizontalPadding, bounds.Y + PanelTopPadding);
        foreach (var line in content.Lines)
        {
            var style = GetTextStyle(line.Style, theme);
            DrawUiText(line.Text, linePosition, style.Color, style.Size, style.Bold);
            linePosition.Y += LineAdvance;
        }
    }

    public static bool TryGetPanelBounds(Viewport viewport, out Rectangle bounds)
        => TryGetPanelBounds(viewport, DefaultLineCount, out bounds);

    public static bool TryGetPanelBounds(Viewport viewport, int lineCount, out Rectangle bounds)
    {
        if (viewport.Width < MinimumVisibleWidth)
        {
            bounds = Rectangle.Empty;
            return false;
        }

        bounds = GetPanelBounds(viewport, lineCount);
        return true;
    }

    private static Rectangle GetPanelBounds(Viewport viewport, int lineCount)
    {
        var panelHeight = GetPanelHeight(lineCount);
        var x = viewport.Width - PanelWidth - PanelRightMargin;
        var y = Math.Max(PanelMinimumTop, viewport.Height - panelHeight - PanelBottomMargin);
        return new Rectangle(x, y, PanelWidth, panelHeight);
    }

    private static int GetPanelHeight(int lineCount)
    {
        if (lineCount <= 0) return PanelMinimumHeight;

        var contentHeight = PanelTopPadding
            + ((lineCount - 1) * LineAdvance)
            + ApproximateLineHeight
            + PanelBottomPadding;
        return Math.Max(PanelMinimumHeight, contentHeight);
    }

    private static InspectorPanelTextAppearance GetTextStyle(InspectorPanelTextStyle style, BoardTheme theme)
        => style switch
        {
            InspectorPanelTextStyle.Primary => new InspectorPanelTextAppearance(theme.PanelPrimaryTextColor, 16, true),
            InspectorPanelTextStyle.SectionTitle => new InspectorPanelTextAppearance(theme.PanelPrimaryTextColor, 14, true),
            _ => new InspectorPanelTextAppearance(theme.PanelSecondaryTextColor, 15, false)
        };

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

    private readonly record struct InspectorPanelTextAppearance(Color Color, float Size, bool Bold);
}
