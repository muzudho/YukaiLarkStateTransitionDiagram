namespace YukaiLarkStateTransitionDiagram.Navigation;

using System;
using System.Collections.Generic;
using System.Linq;
using YukaiLarkStateTransitionDiagram.Theme;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public sealed record SubstateBreadcrumbItem(string Label, int DiagramId, bool IsCurrent);

/// <summary>
/// 現在のサブステート階層を表すパンくずリストの描画。
/// </summary>
public sealed class SubstateBreadcrumbRenderer : IDisposable
{
    public const int BreadcrumbTop = DiagramTabRenderer.TabBarTop + DiagramTabRenderer.TabBarHeight;
    public const int BreadcrumbHeight = 30;

    private const int MinimumVisibleWidth = 420;
    private const int HorizontalPadding = 12;
    private const int ButtonPaddingX = 7;
    private const int ButtonTop = BreadcrumbTop + 4;
    private const int ButtonHeight = 22;
    private const int ButtonGap = 6;
    private const int TextY = BreadcrumbTop + 7;
    private const float TextSize = 14f;
    private const string SeparatorText = ">";

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

    public void DrawBreadcrumb(Viewport viewport, IReadOnlyList<SubstateBreadcrumbItem> path, BoardTheme theme, Point mousePosition)
    {
        if (viewport.Width < MinimumVisibleWidth || path.Count == 0)
        {
            return;
        }

        _spriteBatch.Draw(_pixel, GetBreadcrumbBounds(viewport), theme.BackgroundColor * 0.68f);

        var entries = BuildVisibleEntries(path, viewport.Width - HorizontalPadding * 2);
        var x = (float)HorizontalPadding;
        for (var i = 0; i < entries.Count; i++)
        {
            if (i > 0)
            {
                x = DrawUiText(SeparatorText, new Vector2(x, TextY), theme.HeaderStatusTextColor, TextSize, false) + ButtonGap;
            }

            var entry = entries[i];
            var textBold = entry.IsCurrent;
            var textWidth = TextRenderer.MeasureUiTextWidth(entry.Label, TextSize, textBold);
            if (entry.SourceIndex.HasValue)
            {
                var buttonBounds = new Rectangle(
                    (int)MathF.Round(x),
                    ButtonTop,
                    (int)MathF.Ceiling(textWidth) + ButtonPaddingX * 2,
                    ButtonHeight);
                if (buttonBounds.Contains(mousePosition))
                {
                    _spriteBatch.Draw(_pixel, buttonBounds, theme.HeaderBorderColor * 0.20f);
                    DrawRectangleOutline(buttonBounds, theme.HeaderBorderColor * 0.70f);
                }

                DrawUiText(entry.Label, new Vector2(buttonBounds.X + ButtonPaddingX, TextY), theme.HeaderTitleTextColor, TextSize, textBold);
                x = buttonBounds.Right + ButtonGap;
                continue;
            }

            x = DrawUiText(entry.Label, new Vector2(x, TextY), theme.HeaderStatusTextColor, TextSize, false) + ButtonGap;
        }
    }

    public static Rectangle GetBreadcrumbBounds(Viewport viewport)
    {
        if (viewport.Width < MinimumVisibleWidth)
        {
            return Rectangle.Empty;
        }

        return new Rectangle(0, BreadcrumbTop, viewport.Width, BreadcrumbHeight);
    }

    public static int GetBreadcrumbItemIndexAt(Viewport viewport, IReadOnlyList<SubstateBreadcrumbItem> path, Point point)
    {
        if (!GetBreadcrumbBounds(viewport).Contains(point) || path.Count == 0)
        {
            return -1;
        }

        var entries = BuildVisibleEntries(path, viewport.Width - HorizontalPadding * 2);
        var x = (float)HorizontalPadding;
        for (var i = 0; i < entries.Count; i++)
        {
            if (i > 0)
            {
                x += TextRenderer.MeasureUiTextWidth(SeparatorText, TextSize, false) + ButtonGap;
            }

            var entry = entries[i];
            var textWidth = TextRenderer.MeasureUiTextWidth(entry.Label, TextSize, entry.IsCurrent);
            if (entry.SourceIndex.HasValue)
            {
                var buttonBounds = new Rectangle(
                    (int)MathF.Round(x),
                    ButtonTop,
                    (int)MathF.Ceiling(textWidth) + ButtonPaddingX * 2,
                    ButtonHeight);
                if (buttonBounds.Contains(point))
                {
                    return entry.SourceIndex.Value;
                }

                x = buttonBounds.Right + ButtonGap;
                continue;
            }

            x += textWidth + ButtonGap;
        }

        return -1;
    }

    private static List<BreadcrumbEntry> BuildVisibleEntries(IReadOnlyList<SubstateBreadcrumbItem> path, int maxWidth)
    {
        var normalized = path
            .Select((item, index) => new BreadcrumbEntry(NormalizeLabel(item.Label), index, item.IsCurrent))
            .ToList();

        if (MeasureEntriesWidth(normalized) <= maxWidth || normalized.Count <= 2)
        {
            return normalized;
        }

        for (var firstVisibleTail = 2; firstVisibleTail < normalized.Count; firstVisibleTail++)
        {
            var shortened = new List<BreadcrumbEntry> { normalized[0], new("...", null, false) };
            shortened.AddRange(normalized.Skip(firstVisibleTail));
            if (MeasureEntriesWidth(shortened) <= maxWidth)
            {
                return shortened;
            }
        }

        return new List<BreadcrumbEntry> { new("...", null, false), normalized[^1] };
    }

    private static float MeasureEntriesWidth(IReadOnlyList<BreadcrumbEntry> entries)
    {
        var width = 0f;
        for (var i = 0; i < entries.Count; i++)
        {
            if (i > 0)
            {
                width += TextRenderer.MeasureUiTextWidth(SeparatorText, TextSize, false) + ButtonGap;
            }

            var textWidth = TextRenderer.MeasureUiTextWidth(entries[i].Label, TextSize, entries[i].IsCurrent);
            width += entries[i].SourceIndex.HasValue
                ? textWidth + ButtonPaddingX * 2
                : textWidth;
            width += ButtonGap;
        }

        return width;
    }

    private static string NormalizeLabel(string label)
        => string.IsNullOrWhiteSpace(label) ? "無名" : label.Trim();

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

    private void DrawRectangleOutline(Rectangle rectangle, Color color)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, 1), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Bottom - 1, rectangle.Width, 1), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Y, 1, rectangle.Height), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.Right - 1, rectangle.Y, 1, rectangle.Height), color);
    }

    private sealed record BreadcrumbEntry(string Label, int? SourceIndex, bool IsCurrent);
}
