namespace YukaiLarkStateTransitionDiagram.Navigation;

using System;
using System.Collections.Generic;
using System.Linq;
using YukaiLarkStateTransitionDiagram.Theme;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

/// <summary>
/// 現在のサブステート階層を表すパンくずリストの描画。
/// </summary>
public sealed class SubstateBreadcrumbRenderer : IDisposable
{
    public const int BreadcrumbTop = DiagramTabRenderer.TabBarTop + DiagramTabRenderer.TabBarHeight;
    public const int BreadcrumbHeight = 30;

    private const int MinimumVisibleWidth = 420;
    private const int HorizontalPadding = 12;
    private const int TextY = BreadcrumbTop + 6;
    private const float TextSize = 14f;

    private readonly GraphicsDevice _graphicsDevice;
    private readonly SpriteBatch _spriteBatch;
    private readonly Texture2D _pixel;
    private readonly Dictionary<string, Texture2D> _uiTextTextureCache = new();

    public SubstateBreadcrumbRenderer(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, Texture2D pixel)
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

    public void DrawBreadcrumb(Viewport viewport, IReadOnlyList<string> path, BoardTheme theme)
    {
        if (viewport.Width < MinimumVisibleWidth || path.Count == 0)
        {
            return;
        }

        var bounds = GetBreadcrumbBounds(viewport);
        _spriteBatch.Draw(_pixel, bounds, theme.HeaderBackgroundColor * 0.88f);
        _spriteBatch.Draw(_pixel, new Rectangle(0, bounds.Bottom - 1, viewport.Width, 1), theme.HeaderBorderColor);

        var text = FitBreadcrumbText(path, viewport.Width - HorizontalPadding * 2);
        DrawUiText(text, new Vector2(HorizontalPadding, TextY), theme.HeaderStatusTextColor, TextSize, false);
    }

    public static Rectangle GetBreadcrumbBounds(Viewport viewport)
    {
        if (viewport.Width < MinimumVisibleWidth)
        {
            return Rectangle.Empty;
        }

        return new Rectangle(0, BreadcrumbTop, viewport.Width, BreadcrumbHeight);
    }

    private static string FitBreadcrumbText(IReadOnlyList<string> path, int maxWidth)
    {
        var normalized = path
            .Select(item => string.IsNullOrWhiteSpace(item) ? "無名" : item.Trim())
            .ToList();
        var text = $"現在位置: {string.Join(" > ", normalized)}";
        if (TextRenderer.MeasureUiTextWidth(text, TextSize, false) <= maxWidth || normalized.Count <= 2)
        {
            return text;
        }

        for (var hiddenCount = normalized.Count - 2; hiddenCount > 0; hiddenCount--)
        {
            var shortened = new List<string> { normalized[0], "..." };
            shortened.AddRange(normalized.Skip(hiddenCount + 1));
            text = $"現在位置: {string.Join(" > ", shortened)}";
            if (TextRenderer.MeasureUiTextWidth(text, TextSize, false) <= maxWidth)
            {
                return text;
            }
        }

        return $"現在位置: ... > {normalized[^1]}";
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
