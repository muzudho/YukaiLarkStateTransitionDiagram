namespace YukaiLarkStateTransitionDiagram.Navigation;

using System;
using System.Collections.Generic;
using YukaiLarkStateTransitionDiagram.Theme;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

/// <summary>
/// 画面上部のアプリ名と状態メッセージの描画
/// </summary>
public sealed class HeaderRenderer : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly SpriteBatch _spriteBatch;
    private readonly Texture2D _pixel;
    private readonly Dictionary<string, Texture2D> _uiTextTextureCache = new();

    public HeaderRenderer(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, Texture2D pixel)
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

    /// <summary>
    /// ヘッダーの描画
    /// </summary>
    /// <param name="viewport">描画領域のビューポート</param>
    /// <param name="title">表示タイトル</param>
    /// <param name="status">現在の状態メッセージ</param>
    public void DrawHeader(
        Viewport viewport,
        string title,
        string status,
        BoardTheme theme,
        bool isEditingTitle = false,
        string editingTitle = "",
        int editingCaretIndex = 0,
        bool showEditingCaret = false,
        string titleWarning = "")
    {
        var bounds = new Rectangle(0, 0, viewport.Width, 58);
        _spriteBatch.Draw(_pixel, bounds, theme.HeaderBackgroundColor);
        _spriteBatch.Draw(_pixel, new Rectangle(0, bounds.Height - 1, viewport.Width, 1), theme.HeaderBorderColor);

        if (isEditingTitle)
        {
            DrawTitleEditor(viewport, editingTitle, editingCaretIndex, showEditingCaret, theme);
        }
        else
        {
            // タイトルの描画
            DrawUiText(title, new Vector2(12, 8), theme.HeaderTitleTextColor, 18, true);
        }

        // 状態メッセージの描画
        DrawUiText(string.IsNullOrEmpty(titleWarning) ? status : titleWarning, new Vector2(12, 32), theme.HeaderStatusTextColor, 16, false);
    }

    public static Rectangle GetTitleHitBounds(Viewport viewport)
        => new(8, 4, Math.Min(520, Math.Max(120, viewport.Width - 24)), 28);

    private void DrawTitleEditor(Viewport viewport, string editingTitle, int editingCaretIndex, bool showEditingCaret, BoardTheme theme)
    {
        var editBounds = GetTitleHitBounds(viewport);
        _spriteBatch.Draw(_pixel, editBounds, Color.White * 0.16f);
        DrawRectangleOutline(editBounds, theme.HeaderBorderColor);

        var textPosition = new Vector2(editBounds.X + 5, editBounds.Y + 3);
        DrawUiText(editingTitle, textPosition, theme.HeaderTitleTextColor, 18, true);
        if (!showEditingCaret)
        {
            return;
        }

        var clampedCaretIndex = Math.Clamp(editingCaretIndex, 0, editingTitle.Length);
        var caretX = textPosition.X + TextRenderer.MeasureUiTextCaretOffset(editingTitle, clampedCaretIndex, 18, true);
        var caretBounds = new Rectangle((int)MathF.Round(caretX), editBounds.Y + 5, 2, editBounds.Height - 10);
        _spriteBatch.Draw(_pixel, caretBounds, theme.HeaderTitleTextColor);
    }

    private void DrawRectangleOutline(Rectangle rectangle, Color color)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, 1), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Bottom - 1, rectangle.Width, 1), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Y, 1, rectangle.Height), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.Right - 1, rectangle.Y, 1, rectangle.Height), color);
    }

    /// <summary>
    /// UIテキストの描画
    /// </summary>
    /// <param name="text"></param>
    /// <param name="position"></param>
    /// <param name="color"></param>
    /// <param name="size"></param>
    /// <param name="bold"></param>
    /// <returns></returns>
    private float DrawUiText(string text, Vector2 position, Color color, float size, bool bold)
    {
        var texture = GetUiTextTexture(text, size, bold);
        _spriteBatch.Draw(texture, position, color);
        return position.X + texture.Width;
    }

    /// <summary>
    /// UIテキストのテクスチャを取得
    /// </summary>
    /// <param name="text"></param>
    /// <param name="size"></param>
    /// <param name="bold"></param>
    /// <returns></returns>
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
