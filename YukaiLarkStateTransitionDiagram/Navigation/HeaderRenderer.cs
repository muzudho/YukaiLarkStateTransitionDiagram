namespace YukaiLarkStateTransitionDiagram.Navigation;

using System;
using System.Collections.Generic;
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
    public void DrawHeader(Viewport viewport, string title, string status)
    {
        var bounds = new Rectangle(0, 0, viewport.Width, 58);
        _spriteBatch.Draw(_pixel, bounds, new Color(17, 19, 23, 238));
        _spriteBatch.Draw(_pixel, new Rectangle(0, bounds.Height - 1, viewport.Width, 1), new Color(65, 72, 84));

        // タイトルの描画
        DrawUiText(title, new Vector2(12, 8), new Color(245, 247, 250), 18, true);

        // 状態メッセージの描画
        DrawUiText(status, new Vector2(12, 32), new Color(210, 220, 232), 16, false);
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