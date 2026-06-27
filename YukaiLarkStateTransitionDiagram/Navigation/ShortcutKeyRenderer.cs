namespace YukaiLarkStateTransitionDiagram.Navigation;

using System;
using YukaiLarkStateTransitionDiagram;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using YukaiLarkStateTransitionDiagram.Theme;

/// <summary>
/// ショートカットキーとその名前の描画
/// </summary>
public sealed class ShortcutKeyRenderer : IDisposable
{
    private static readonly TimeSpan HelpPageDuration = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan HelpPageFadeDuration = TimeSpan.FromMilliseconds(350);
    private readonly GraphicsDevice _graphicsDevice;
    private readonly SpriteBatch _spriteBatch;
    private readonly Texture2D _pixel;
    private readonly Dictionary<string, Texture2D> _uiTextTextureCache = new();
    private IKeyCapTheme _keyCapTheme;
    private BoardTheme _boardTheme;

    private readonly record struct HelpHint(string Key, string Description);
    private readonly record struct HelpPage(HelpHint[] Hints);

    public ShortcutKeyRenderer(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, Texture2D pixel, IKeyCapTheme keyCapTheme, BoardTheme boardTheme)
    {
        _graphicsDevice = graphicsDevice;
        _spriteBatch = spriteBatch;
        _pixel = pixel;
        _keyCapTheme = keyCapTheme;
        _boardTheme = boardTheme;
    }

    public IKeyCapTheme KeyCapTheme
    {
        get => _keyCapTheme;
        set => _keyCapTheme = value;
    }

    public BoardTheme BoardTheme
    {
        get => _boardTheme;
        set => _boardTheme = value;
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
    /// ショートカットキーの描画
    /// </summary>
    /// <param name="viewport">描画領域のビューポート</param>
    /// <param name="totalGameTime">ゲーム開始からの経過時間</param>
    /// <param name="isEditingLabel">ラベル編集中かどうか</param>
    /// <param name="isExportSelecting">エクスポート選択中かどうか</param>
    /// <param name="hasExportSelection">エクスポート範囲が作成済みかどうか</param>
    /// <param name="hasStartMarker">開始マークが存在するかどうか</param>
    /// <param name="selectedNode">選択されているノード</param>
    /// <param name="selectedTransition">選択されている遷移</param>
    public void DrawBottomHelp(
        Viewport viewport,
        TimeSpan totalGameTime,
        bool isEditingLabel,
        bool isExportSelecting,
        bool hasExportSelection,
        bool hasStartMarker,
        DiagramNode? selectedNode,
        DiagramTransition? selectedTransition)
    {
        if (viewport.Height < 360)
        {
            return;
        }

        var y = viewport.Height - 34;
        _spriteBatch.Draw(_pixel, new Rectangle(0, y, viewport.Width, 34), _boardTheme.BottomBarBackgroundColor);

        var pages = GetHelpPages(isEditingLabel, isExportSelecting, hasExportSelection, hasStartMarker, selectedNode, selectedTransition);
        if (pages.Length == 0)
        {
            return;
        }

        var pageDurationSeconds = HelpPageDuration.TotalSeconds;
        var fadeDurationSeconds = Math.Min(HelpPageFadeDuration.TotalSeconds, pageDurationSeconds / 2d);
        var pageProgress = totalGameTime.TotalSeconds % pageDurationSeconds;
        var pageIndex = (int)(totalGameTime.TotalSeconds / pageDurationSeconds) % pages.Length;
        var nextPageIndex = (pageIndex + 1) % pages.Length;
        var fadeProgress = 0d;
        var currentAlpha = 1f;
        var nextAlpha = 0f;

        if (pages.Length > 1 && pageProgress >= pageDurationSeconds - fadeDurationSeconds)
        {
            fadeProgress = (pageProgress - (pageDurationSeconds - fadeDurationSeconds)) / fadeDurationSeconds;
            currentAlpha = (float)(1d - fadeProgress);
            nextAlpha = (float)fadeProgress;
        }

        var position = new Vector2(12, y + 6);
        position = DrawHelpPage(page: pages[pageIndex], position, currentAlpha);

        if (nextAlpha > 0f)
        {
            DrawHelpPage(page: pages[nextPageIndex], position: new Vector2(12, y + 6), nextAlpha);
        }

        DrawHelpPageNumber(
            viewport,
            y,
            pageIndex,
            pages.Length,
            currentAlpha,
            nextAlpha,
            nextPageIndex);
    }

    private Vector2 DrawHelpPage(HelpPage page, Vector2 position, float alpha)
    {
        for (var i = 0; i < page.Hints.Length; i++)
        {
            var hint = page.Hints[i];
            position = DrawShortcutHint(position, hint.Key, hint.Description, alpha);
            if (i < page.Hints.Length - 1)
            {
                position = DrawHelpSeparator(position, alpha);
            }
        }

        return position;
    }

    private void DrawHelpPageNumber(Viewport viewport, int y, int pageIndex, int pageCount, float currentAlpha, float nextAlpha, int nextPageIndex)
    {
        var currentText = $"({pageIndex + 1}/{pageCount})";
        var currentTexture = GetUiTextTexture(currentText, 14, false);
        var currentX = viewport.Width - currentTexture.Width - 12;
        DrawUiText(currentText, new Vector2(currentX, y + 3), _keyCapTheme.SeparatorTextColor, 14, false, currentAlpha);

        if (nextAlpha > 0f)
        {
            var nextText = $"({nextPageIndex + 1}/{pageCount})";
            DrawUiText(nextText, new Vector2(currentX, y + 3), _keyCapTheme.SeparatorTextColor, 14, false, nextAlpha);
        }
    }

    private static HelpPage[] GetHelpPages(
        bool isEditingLabel,
        bool isExportSelecting,
        bool hasExportSelection,
        bool hasStartMarker,
        DiagramNode? selectedNode,
        DiagramTransition? selectedTransition)
    {
        var startMarkerHint = hasStartMarker
            ? new HelpHint("S", "開始へ移動")
            : new HelpHint("S", "開始マーク追加");

        if (isExportSelecting)
        {
            var exportHints = new List<HelpHint>
            {
                new("ドラッグ", "枠を移動・調整"),
                new("Enter", "撮影"),
                new("Alt+ドラッグ", "吸着なし"),
                new("右クリック/Esc", "キャンセル")
            };

            return
            [
                new HelpPage
                (
                    exportHints.ToArray()
                )
            ];
        }

        if (isEditingLabel)
        {
            return
            [
                new HelpPage
                (
                    [
                        new HelpHint("Enter", "確定"),
                        new HelpHint("Esc", "キャンセル"),
                        new HelpHint("Backspace", "1文字削除")
                    ]
                )
            ];
        }

        if (selectedTransition is not null)
        {
            return
            [
                new HelpPage
                (
                    [
                        new HelpHint("F2・Enter", "ラベル編集"),
                        new HelpHint("Tab", "ラベル左右切替"),
                        new HelpHint("Delete", "遷移削除")
                    ]
                ),
                new HelpPage
                (
                    [
                        new HelpHint("Shift+ドラッグ", "遷移作成"),
                        new HelpHint("Shift+同一状態", "自己ループ"),
                        startMarkerHint,
                        new HelpHint("Ctrl+S", "保存"),
                        new HelpHint("Ctrl+Z/Y", "元に戻す/やり直し")
                    ]
                ),
                new HelpPage
                (
                    [
                        new HelpHint("Ctrl+O", "読込"),
                        new HelpHint("Ctrl+P", "PNG出力"),
                        new HelpHint("T", "テーマ選択"),
                        new HelpHint("0-9", "テーマ")
                    ]
                )
            ];
        }

        if (selectedNode is not null)
        {
            var nodeHints = new List<HelpHint>
            {
                new("F2・Enter", "ラベル編集"),
                new("Delete", "状態削除")
            };

            if (selectedNode.Kind == NodeKind.Normal)
            {
                nodeHints.Add(new HelpHint("C", "状態色変更"));
            }

            return
            [
                new HelpPage
                (
                    nodeHints.ToArray()
                ),
                new HelpPage
                (
                    [
                        new HelpHint("N", "状態追加"),
                        startMarkerHint,
                        new HelpHint("E", "終了マーク追加"),
                        new HelpHint("Ctrl+N", "新規作成"),
                        new HelpHint("Ctrl+S", "保存"),
                        new HelpHint("Ctrl+Z/Y", "元に戻す/やり直し")
                    ]
                ),
                new HelpPage
                (
                    [
                        new HelpHint("Ctrl+O", "読込"),
                        new HelpHint("Ctrl+P", "PNG出力"),
                        new HelpHint("T", "テーマ選択"),
                        new HelpHint("0-9", "テーマ")
                    ]
                )
            ];
        }

        return
        [
            new HelpPage
            (
                [
                    new HelpHint("N", "状態追加"),
                    startMarkerHint,
                    new HelpHint("E", "終了マーク追加"),
                    new HelpHint("Ctrl+N", "新規作成"),
                    new HelpHint("Ctrl+S", "保存"),
                    new HelpHint("Ctrl+Z/Y", "元に戻す/やり直し")
                ]
            ),
            new HelpPage
            (
                [
                    new HelpHint("Ctrl+Shift+S", "名前を付けて保存"),
                    new HelpHint("Ctrl+O", "読込"),
                    new HelpHint("Ctrl+P", "PNG出力")
                ]
            ),
            new HelpPage
            (
                [
                    new HelpHint("Shift+ドラッグ", "遷移作成"),
                    new HelpHint("Shift+同一状態", "自己ループ"),
                    new HelpHint("空白ドラッグ", "表示移動")
                ]
            ),
            new HelpPage
            (
                [
                    new HelpHint("Ctrl+P", "PNG出力"),
                    new HelpHint("T", "テーマ選択"),
                    new HelpHint("0-9", "テーマ"),
                    new HelpHint("空白ドラッグ", "表示移動")
                ]
            )
        ];
    }

    /// <summary>
    /// ショートカットキーのヒントを描画する
    /// </summary>
    /// <param name="position"></param>
    /// <param name="key"></param>
    /// <param name="description"></param>
    /// <returns></returns>
    private Vector2 DrawShortcutHint(Vector2 position, string key, string description, float alpha)
    {
        var x = DrawKeyCap(key, position, alpha);
        x = DrawUiText(description, new Vector2(x + 6, position.Y + 3), _keyCapTheme.DescriptionTextColor, 14, false, alpha);
        return new Vector2(x + 12, position.Y);
    }

    /// <summary>
    /// ヘルプの区切り線を描画する
    /// </summary>
    /// <param name="position"></param>
    /// <returns></returns>
    private Vector2 DrawHelpSeparator(Vector2 position, float alpha)
    {
        var x = DrawUiText("/", new Vector2(position.X, position.Y + 3), _keyCapTheme.SeparatorTextColor, 14, false, alpha);
        return new Vector2(x + 12, position.Y);
    }

    /// <summary>
    /// キーキャップを描画する
    /// </summary>
    /// <param name="text">キーキャップに表示するテキスト</param>
    /// <param name="position">描画位置</param>
    /// <returns>描画後のX座標</returns>
    private float DrawKeyCap(string text, Vector2 position, float alpha)
    {
        var textTexture = GetUiTextTexture(text, _keyCapTheme.FontSize, true);
        var width = Math.Max(_keyCapTheme.MinWidth, textTexture.Width + (_keyCapTheme.HorizontalPadding * 2));
        var height = _keyCapTheme.Height;
        var bounds = new Rectangle((int)position.X, (int)position.Y, width, height);

        _spriteBatch.Draw(_pixel, bounds, WithAlpha(_keyCapTheme.FaceColor, alpha));
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), WithAlpha(_keyCapTheme.TopEdgeColor, alpha));
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), WithAlpha(_keyCapTheme.TopEdgeColor, alpha));
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - 2, bounds.Width, 2), WithAlpha(_keyCapTheme.BottomEdgeColor, alpha));
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), WithAlpha(_keyCapTheme.BottomEdgeColor, alpha));
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X + 2, bounds.Y + 2, bounds.Width - 4, 1), WithAlpha(_keyCapTheme.InnerHighlightColor, alpha));

        var textPosition = new Vector2(
            bounds.X + (bounds.Width - textTexture.Width) / 2f,
            bounds.Y + (bounds.Height - textTexture.Height) / 2f);
        _spriteBatch.Draw(textTexture, textPosition, WithAlpha(_keyCapTheme.LabelTextColor, alpha));
        return bounds.Right;
    }

    /// <summary>
    /// UIテキストを描画する
    /// </summary>
    /// <param name="text"></param>
    /// <param name="position"></param>
    /// <param name="color"></param>
    /// <param name="size"></param>
    /// <param name="bold"></param>
    /// <returns></returns>
    private float DrawUiText(string text, Vector2 position, Color color, float size, bool bold, float alpha = 1f)
    {
        var texture = GetUiTextTexture(text, size, bold);
        _spriteBatch.Draw(texture, position, WithAlpha(color, alpha));
        return position.X + texture.Width;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        return new Color(color.R, color.G, color.B, (byte)(Math.Clamp(alpha, 0f, 1f) * color.A));
    }

    /// <summary>
    /// UIテキストのテクスチャを取得する
    /// </summary>
    /// <param name="text">テキスト内容</param>
    /// <param name="size">フォントサイズ</param>
    /// <param name="bold">太字かどうか</param>
    /// <returns>テクスチャ</returns>
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
