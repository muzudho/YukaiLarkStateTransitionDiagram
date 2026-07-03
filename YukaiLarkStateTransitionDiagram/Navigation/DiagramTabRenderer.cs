namespace YukaiLarkStateTransitionDiagram.Navigation;

using System;
using System.Collections.Generic;
using YukaiLarkStateTransitionDiagram.Theme;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

/// <summary>
/// ダイアグラムインスタンス切り替え用タブの描画。
/// </summary>
public sealed class DiagramTabRenderer : IDisposable
{
    public const int TabBarTop = 58;
    public const int TabBarHeight = 34;

    private const int LeftMargin = 10;
    private const int TabHeight = 26;
    private const int TabGap = 4;
    private const int MaxTabWidth = 180;
    private const int MinTabWidth = 86;

    private readonly GraphicsDevice _graphicsDevice;
    private readonly SpriteBatch _spriteBatch;
    private readonly Texture2D _pixel;
    private readonly Dictionary<string, Texture2D> _uiTextTextureCache = new();

    public DiagramTabRenderer(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, Texture2D pixel)
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

    public void DrawTabs(Viewport viewport, IReadOnlyList<DiagramInstance> diagrams, int activeIndex, BoardTheme theme)
    {
        var barBounds = GetTabBarBounds(viewport);
        _spriteBatch.Draw(_pixel, barBounds, theme.HeaderBackgroundColor * 0.94f);
        _spriteBatch.Draw(_pixel, new Rectangle(0, barBounds.Bottom - 1, viewport.Width, 1), theme.HeaderBorderColor);

        for (var i = 0; i < diagrams.Count; i++)
        {
            var bounds = GetTabBounds(viewport, diagrams, i);
            if (bounds == Rectangle.Empty || bounds.X >= viewport.Width)
            {
                continue;
            }

            var active = i == activeIndex;
            var fill = active ? theme.BackgroundColor : theme.HeaderBackgroundColor;
            var textColor = active ? theme.HeaderTitleTextColor : theme.HeaderStatusTextColor;
            _spriteBatch.Draw(_pixel, bounds, fill);
            DrawRectangleOutline(bounds, active ? theme.SelectedTransitionLineColor : theme.HeaderBorderColor);

            var label = diagrams[i].Name;
            var texture = GetUiTextTexture(label, 14, active);
            var textX = bounds.X + Math.Max(8, (bounds.Width - texture.Width) / 2);
            _spriteBatch.Draw(texture, new Vector2(textX, bounds.Y + 5), textColor);
        }

        var addBounds = GetAddTabBounds(viewport, diagrams);
        if (addBounds != Rectangle.Empty)
        {
            _spriteBatch.Draw(_pixel, addBounds, theme.HeaderBackgroundColor);
            DrawRectangleOutline(addBounds, theme.HeaderBorderColor);
            DrawUiText("+", new Vector2(addBounds.X + 10, addBounds.Y + 3), theme.HeaderTitleTextColor, 16, true);
        }
    }

    public static Rectangle GetTabBarBounds(Viewport viewport)
        => new(0, TabBarTop, viewport.Width, TabBarHeight);

    public static Rectangle GetTabBounds(Viewport viewport, IReadOnlyList<DiagramInstance> diagrams, int index)
    {
        if (index < 0 || index >= diagrams.Count || viewport.Width < 260)
        {
            return Rectangle.Empty;
        }

        var usableWidth = Math.Max(MinTabWidth, viewport.Width - LeftMargin - 54);
        var tabWidth = Math.Clamp((usableWidth - ((diagrams.Count - 1) * TabGap)) / Math.Max(1, diagrams.Count), MinTabWidth, MaxTabWidth);
        return new Rectangle(LeftMargin + (index * (tabWidth + TabGap)), TabBarTop + 5, tabWidth, TabHeight);
    }

    public static Rectangle GetAddTabBounds(Viewport viewport, IReadOnlyList<DiagramInstance> diagrams)
    {
        var lastTab = diagrams.Count == 0 ? Rectangle.Empty : GetTabBounds(viewport, diagrams, diagrams.Count - 1);
        var x = lastTab == Rectangle.Empty ? LeftMargin : lastTab.Right + TabGap;
        if (x + 34 > viewport.Width - 10)
        {
            return Rectangle.Empty;
        }

        return new Rectangle(x, TabBarTop + 5, 34, TabHeight);
    }

    private void DrawRectangleOutline(Rectangle rectangle, Color color)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, 1), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Bottom - 1, rectangle.Width, 1), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Y, 1, rectangle.Height), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.Right - 1, rectangle.Y, 1, rectangle.Height), color);
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