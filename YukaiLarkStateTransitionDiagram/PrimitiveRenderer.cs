namespace YukaiLarkStateTransitionDiagram;

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

/// <summary>
/// 低レベルな図形描画
/// </summary>
public sealed class PrimitiveRenderer
{
    private readonly SpriteBatch _spriteBatch;
    private readonly Texture2D _pixel;

    public PrimitiveRenderer(SpriteBatch spriteBatch, Texture2D pixel)
    {
        _spriteBatch = spriteBatch;
        _pixel = pixel;
    }

    /// <summary>
    /// 取っ手を描く。
    /// </summary>
    /// <param name="center">取っ手の中心点</param>
    /// <param name="color">取っ手の色</param>
    public void DrawHandle(Vector2 center, Color color)
    {
        DrawCircle(center, 8f, color);
        DrawCircleOutline(center, 8f, new Color(20, 24, 30), 2f);
    }

    /// <summary>
    /// 線を描く。
    /// </summary>
    /// <param name="start">線の開始点</param>
    /// <param name="end">線の終了点</param>
    /// <param name="color">線の色</param>
    /// <param name="thickness">線の太さ</param>
    public void DrawLine(Vector2 start, Vector2 end, Color color, float thickness)
    {
        var delta = end - start;
        var length = delta.Length();
        if (length <= 0.01f)
        {
            return;
        }

        _spriteBatch.Draw(_pixel, start, null, color, (float)Math.Atan2(delta.Y, delta.X), Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0f);
    }

    /// <summary>
    /// 円を描く。
    /// </summary>
    /// <param name="center">円の中心点</param>
    /// <param name="radius">円の半径</param>
    /// <param name="color">円の色</param>
    public void DrawCircle(Vector2 center, float radius, Color color)
    {
        for (var y = -radius; y <= radius; y++)
        {
            var halfWidth = (float)Math.Sqrt(radius * radius - y * y);
            _spriteBatch.Draw(_pixel, new Rectangle((int)(center.X - halfWidth), (int)(center.Y + y), (int)(halfWidth * 2), 1), color);
        }
    }

    /// <summary>
    /// 円の輪郭を描く。
    /// </summary>
    /// <param name="center">円の中心点</param>
    /// <param name="radius">円の半径</param>
    /// <param name="color">円の色</param>
    /// <param name="thickness">円の輪郭の太さ</param>
    public void DrawCircleOutline(Vector2 center, float radius, Color color, float thickness)
    {
        const int segments = 64;
        var previous = center + new Vector2(radius, 0);
        for (var i = 1; i <= segments; i++)
        {
            var angle = MathHelper.TwoPi * i / segments;
            var current = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            DrawLine(previous, current, color, thickness);
            previous = current;
        }
    }
}
