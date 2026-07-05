namespace YukaiLarkStateTransitionDiagram;

using System;
using System.Linq;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingBrushes = System.Drawing.Brushes;
using DrawingColor = System.Drawing.Color;
using DrawingFont = System.Drawing.Font;
using DrawingFontFamily = System.Drawing.FontFamily;
using DrawingFontStyle = System.Drawing.FontStyle;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingGraphicsUnit = System.Drawing.GraphicsUnit;
using DrawingRectangleF = System.Drawing.RectangleF;
using DrawingStringFormat = System.Drawing.StringFormat;
using DrawingStringFormatFlags = System.Drawing.StringFormatFlags;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

/// <summary>
/// 文字列のテクスチャ描画
/// </summary>
public static class TextRenderer
{
    private const int MaxTextMeasureWidth = 4096;
    private const int MaxUiTextTextureWidth = 4096;
    private const int MaxLabelTextTextureWidth = 2400;

    /// <summary>
    /// 文字列を描画してテクスチャを作成する。
    /// </summary>
    private static readonly DrawingStringFormat CenteredStringFormat = new()
    {
        Alignment = System.Drawing.StringAlignment.Center,
        LineAlignment = System.Drawing.StringAlignment.Center,
        FormatFlags = DrawingStringFormatFlags.NoWrap
    };

    /// <summary>
    /// 文字列を描画してテクスチャを作成する。
    /// </summary>
    private static readonly DrawingStringFormat LeftAlignedStringFormat = new()
    {
        Alignment = System.Drawing.StringAlignment.Near,
        LineAlignment = System.Drawing.StringAlignment.Center,
        FormatFlags = DrawingStringFormatFlags.NoWrap
    };

    /// <summary>
    /// 文字列を描画してテクスチャを作成する。
    /// </summary>
    private static readonly DrawingStringFormat StringFormatNoWrap = new()
    {
        FormatFlags = DrawingStringFormatFlags.NoWrap
    };

    /// <summary>
    /// UI用の文字テクスチャを生成する。
    /// </summary>
    /// <param name="graphicsDevice">描画先のグラフィックスデバイス</param>
    /// <param name="text">描画文字列</param>
    /// <param name="size">フォントサイズ</param>
    /// <param name="bold">太字かどうか</param>
    /// <returns>文字テクスチャ</returns>
    public static Texture2D CreateUiTextTexture(GraphicsDevice graphicsDevice, string text, float size, bool bold)
    {
        var renderedText = string.IsNullOrEmpty(text) ? " " : text;
        using var font = CreateJapaneseFont(size, bold);
        using var measureBitmap = new DrawingBitmap(1, 1);
        using var measureGraphics = DrawingGraphics.FromImage(measureBitmap);
        var measured = measureGraphics.MeasureString(renderedText, font, MaxTextMeasureWidth, StringFormatNoWrap);
        var width = Math.Clamp((int)Math.Ceiling(measured.Width) + 4, 8, MaxUiTextTextureWidth);
        var height = Math.Clamp((int)Math.Ceiling(measured.Height) + 4, 8, 64);
        using var bitmap = new DrawingBitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var graphics = DrawingGraphics.FromImage(bitmap);
        graphics.Clear(DrawingColor.Transparent);
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        graphics.DrawString(renderedText, font, DrawingBrushes.White, new DrawingRectangleF(0, 0, width, height), LeftAlignedStringFormat);

        return CreateTexture(graphicsDevice, bitmap, width, height);
    }

    /// <summary>
    /// ラベル用の文字テクスチャを生成する。
    /// </summary>
    /// <param name="graphicsDevice">描画先のグラフィックスデバイス</param>
    /// <param name="label">描画するラベル</param>
    /// <param name="editing">編集中かどうか</param>
    /// <returns>文字テクスチャ</returns>
    public static Texture2D CreateLabelTexture(GraphicsDevice graphicsDevice, string label, bool editing)
    {
        var text = string.IsNullOrEmpty(label) ? " " : label;
        using var font = CreateJapaneseFont(22, true);
        using var measureBitmap = new DrawingBitmap(1, 1);
        using var measureGraphics = DrawingGraphics.FromImage(measureBitmap);
        var measured = measureGraphics.MeasureString(text, font, MaxTextMeasureWidth, StringFormatNoWrap);
        var width = Math.Clamp((int)Math.Ceiling(measured.Width) + 18, 48, MaxLabelTextTextureWidth);
        var height = Math.Clamp((int)Math.Ceiling(measured.Height) + 10, 30, 72);
        using var bitmap = new DrawingBitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var graphics = DrawingGraphics.FromImage(bitmap);
        graphics.Clear(DrawingColor.Transparent);
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        graphics.DrawString(text, font, DrawingBrushes.White, new DrawingRectangleF(0, 0, width, height), CenteredStringFormat);
        return CreateTexture(graphicsDevice, bitmap, width, height);
    }
    public static float MeasureLabelTextWidth(string label)
    {
        var text = string.IsNullOrEmpty(label) ? " " : label;
        using var font = CreateJapaneseFont(22, true);
        using var measureBitmap = new DrawingBitmap(1, 1);
        using var measureGraphics = DrawingGraphics.FromImage(measureBitmap);
        return measureGraphics.MeasureString(text, font, MaxTextMeasureWidth, StringFormatNoWrap).Width;
    }

    public static float MeasureUiTextWidth(string text, float size, bool bold)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0f;
        }

        using var font = CreateJapaneseFont(size, bold);
        using var measureBitmap = new DrawingBitmap(1, 1);
        using var measureGraphics = DrawingGraphics.FromImage(measureBitmap);
        return measureGraphics.MeasureString(text, font, MaxTextMeasureWidth, StringFormatNoWrap).Width;
    }

    public static float MeasureUiTextCaretOffset(string text, int caretIndex, float size, bool bold)
    {
        if (string.IsNullOrEmpty(text) || caretIndex <= 0)
        {
            return 0f;
        }

        var clampedCaretIndex = Math.Clamp(caretIndex, 0, text.Length);
        using var font = CreateJapaneseFont(size, bold);
        using var measureBitmap = new DrawingBitmap(1, 1);
        using var measureGraphics = DrawingGraphics.FromImage(measureBitmap);
        measureGraphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        using var format = (DrawingStringFormat)LeftAlignedStringFormat.Clone();
        format.FormatFlags |= DrawingStringFormatFlags.MeasureTrailingSpaces;
        format.SetMeasurableCharacterRanges(new[] { new System.Drawing.CharacterRange(0, clampedCaretIndex) });
        using var region = measureGraphics.MeasureCharacterRanges(text, font, new DrawingRectangleF(0, 0, MaxTextMeasureWidth, 64), format)[0];
        return region.GetBounds(measureGraphics).Right;
    }

    /// <summary>
    /// 日本語を描画しやすいフォントを作成する。
    /// </summary>
    /// <param name="size">フォントサイズ</param>
    /// <param name="bold">太字かどうか</param>
    /// <returns>フォント</returns>
    public static DrawingFont CreateJapaneseFont(float size, bool bold)
    {
        var candidates = new[] { "Yu Gothic UI", "Meiryo", "MS Gothic", "Noto Sans CJK JP", "Arial Unicode MS" };
        foreach (var candidate in candidates)
        {
            if (DrawingFontFamily.Families.Any(f => string.Equals(f.Name, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return new DrawingFont(candidate, size, bold ? DrawingFontStyle.Bold : DrawingFontStyle.Regular, DrawingGraphicsUnit.Pixel);
            }
        }

        return new DrawingFont(DrawingFontFamily.GenericSansSerif, size, bold ? DrawingFontStyle.Bold : DrawingFontStyle.Regular, DrawingGraphicsUnit.Pixel);
    }

    /// <summary>
    /// ビットマップからテクスチャを作成する。
    /// </summary>
    /// <param name="graphicsDevice">描画先のグラフィックスデバイス</param>
    /// <param name="bitmap">ビットマップ</param>
    /// <param name="width">幅</param>
    /// <param name="height">高さ</param>
    /// <returns>テクスチャ</returns>
    private static Texture2D CreateTexture(GraphicsDevice graphicsDevice, DrawingBitmap bitmap, int width, int height)
    {
        var colors = new Color[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                var alpha = pixel.A;
                colors[y * width + x] = new Color(
                    Premultiply(pixel.R, alpha),
                    Premultiply(pixel.G, alpha),
                    Premultiply(pixel.B, alpha),
                    alpha);
            }
        }

        var texture = new Texture2D(graphicsDevice, width, height);
        texture.SetData(colors);
        return texture;
    }

    /// <summary>
    /// 色成分をアルファ値でプリマルチプライする。
    /// </summary>
    /// <param name="color">色成分</param>
    /// <param name="alpha">アルファ値</param>
    /// <returns>プリマルチプライ後の色成分</returns>
    private static byte Premultiply(byte color, byte alpha)
        => (byte)((color * alpha + 127) / 255);
}
