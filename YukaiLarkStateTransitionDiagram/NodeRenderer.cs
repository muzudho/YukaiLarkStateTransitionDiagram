namespace YukaiLarkStateTransitionDiagram;

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

/// <summary>
/// ノードの描画
/// </summary>
public sealed class NodeRenderer
{
    private readonly PrimitiveRenderer _primitiveRenderer;
    private readonly SpriteBatch _spriteBatch;
    private readonly Color[] _palette;
    private readonly Func<string, bool, Texture2D> _getLabelTexture;

    public BoardTheme Theme { get; set; }

    public NodeRenderer(
        PrimitiveRenderer primitiveRenderer,
        SpriteBatch spriteBatch,
        Color[] palette,
        Func<string, bool, Texture2D> getLabelTexture,
        BoardTheme theme)
    {
        _primitiveRenderer = primitiveRenderer;
        _spriteBatch = spriteBatch;
        _palette = palette;
        _getLabelTexture = getLabelTexture;
        Theme = theme;
    }

    /// <summary>
    /// ノードを描画する
    /// </summary>
    /// <param name="node">描画するノード</param>
    /// <param name="selected">選択されているかどうか</param>
    /// <param name="editingNode">編集中のノード</param>
    /// <param name="editingLabel">編集中のラベル</param>
    public void DrawNode(DiagramNode node, bool selected, DiagramNode? editingNode, string editingLabel, int editingCaretIndex, bool showEditingCaret, bool inactive = false)
    {
        var baseFill = node.Kind == NodeKind.Normal && _palette.Length > 0
            ? _palette[node.ColorIndex % _palette.Length]
            : new Color(5, 6, 8);
        var fill = inactive ? GetInactiveColor(baseFill, 0.38f) : baseFill;
        var outerColor = inactive
            ? GetInactiveColor(selected ? new Color(255, 255, 255) : new Color(10, 12, 16), 0.52f)
            : selected ? new Color(255, 255, 255) : new Color(10, 12, 16);
        var outlineColor = inactive ? GetInactiveColor(Color.White, 0.56f) : Color.White;
        var normalOutlineColor = inactive ? GetInactiveColor(new Color(15, 18, 24), 0.56f) : new Color(15, 18, 24);
        var labelColor = inactive ? Theme.PanelMutedTextColor * 0.72f : Color.White;

        _primitiveRenderer.DrawCircle(node.Position, node.Radius + 4, outerColor);
        _primitiveRenderer.DrawCircle(node.Position, node.Radius, fill);

        // 通常ノード
        if (node.Kind == NodeKind.Normal)
        {
            _primitiveRenderer.DrawCircleOutline(node.Position, node.Radius, normalOutlineColor, 3f);
        }
        // 開始マーク
        else if (node.Kind == NodeKind.StartMarker)
        {
            _primitiveRenderer.DrawCircleOutline(node.Position, node.Radius - 1f, outlineColor, 5f);
        }
        // 終了マーク
        else
        {
            _primitiveRenderer.DrawCircleOutline(node.Position, node.Radius - 2f, outlineColor, 2f);
            _primitiveRenderer.DrawCircleOutline(node.Position, node.Radius - 7f, outlineColor, 2f);
        }

        if (node == editingNode)
        {
            DrawNodeLabel(editingLabel, node.Position, editing: true, labelColor);
            if (showEditingCaret)
            {
                DrawEditingCaret(editingLabel, editingCaretIndex, node.Position, labelColor);
            }
        }
        else
        {
            DrawNodeLabel(node.Label, node.Position, editing: false, labelColor);
        }
    }

    /// <summary>
    /// 作成前の開始マークを薄く描画します。
    /// </summary>
    /// <param name="node">描画する仮ノード</param>
    /// <param name="opacity">不透明度</param>
    public void DrawStartMarkerGhost(DiagramNode node, float opacity)
    {
        var alpha = MathHelper.Clamp(opacity, 0f, 1f);
        _primitiveRenderer.DrawCircle(node.Position, node.Radius + 8f, new Color(110, 185, 230) * (alpha * 0.32f));
        _primitiveRenderer.DrawCircle(node.Position, node.Radius + 4f, new Color(255, 255, 255) * (alpha * 0.34f));
        _primitiveRenderer.DrawCircle(node.Position, node.Radius, new Color(5, 6, 8) * (alpha * 0.46f));
        _primitiveRenderer.DrawCircleOutline(node.Position, node.Radius - 1f, Color.White * (alpha * 0.74f), 5f);
        DrawNodeLabel(node.Label, node.Position, editing: false, Color.White * (alpha * 0.74f));
    }

    /// <summary>
    /// 作成前の通常ノードを薄く描画します。
    /// </summary>
    /// <param name="node">描画する仮ノード</param>
    /// <param name="opacity">不透明度</param>
    public void DrawStateNodeGhost(DiagramNode node, float opacity)
    {
        var alpha = MathHelper.Clamp(opacity, 0f, 1f);
        var fill = _palette.Length > 0
            ? _palette[node.ColorIndex % _palette.Length]
            : new Color(60, 130, 220);

        _primitiveRenderer.DrawCircle(node.Position, node.Radius + 8f, fill * (alpha * 0.26f));
        _primitiveRenderer.DrawCircle(node.Position, node.Radius + 4f, new Color(255, 255, 255) * (alpha * 0.3f));
        _primitiveRenderer.DrawCircle(node.Position, node.Radius, fill * (alpha * 0.54f));
        _primitiveRenderer.DrawCircleOutline(node.Position, node.Radius, new Color(15, 18, 24) * (alpha * 0.72f), 3f);
        DrawNodeLabel(node.Label, node.Position, editing: false, Color.White * (alpha * 0.78f));
    }

    /// <summary>
    /// ノードのリサイズハンドルを描画します。
    /// </summary>
    /// <param name="node">描画するノード</param>
    public void DrawNodeResizeHandle(DiagramNode node)
    {
        var center = GetNodeResizeHandleCenter(node);
        _primitiveRenderer.DrawLine(node.Position + new Vector2(node.Radius * 0.72f, node.Radius * 0.72f), center, new Color(255, 230, 120), 2f);
        _primitiveRenderer.DrawHandle(center, new Color(255, 230, 120));
    }

    /// <summary>
    /// ノードのラベルを描画します。
    /// </summary>
    /// <param name="label">描画するラベル</param>
    /// <param name="center">ラベルの中心座標</param>
    /// <param name="editing">編集中かどうか</param>
    private void DrawNodeLabel(string label, Vector2 center, bool editing)
        => DrawNodeLabel(label, center, editing, Color.White);

    private void DrawNodeLabel(string label, Vector2 center, bool editing, Color color)
    {
        var texture = _getLabelTexture(label, editing);
        var position = center - new Vector2(texture.Width / 2f, texture.Height / 2f);
        _spriteBatch.Draw(texture, position, color);
    }

    private void DrawEditingCaret(string label, int caretIndex, Vector2 center, Color color)
    {
        var texture = _getLabelTexture(label, true);
        var texturePosition = center - new Vector2(texture.Width / 2f, texture.Height / 2f);
        var normalizedCaretIndex = Math.Clamp(caretIndex, 0, label.Length);
        var textWidth = TextRenderer.MeasureLabelTextWidth(label);
        var prefixWidth = normalizedCaretIndex <= 0
            ? 0f
            : TextRenderer.MeasureLabelTextWidth(label[..normalizedCaretIndex]);
        var textLeft = texturePosition.X + ((texture.Width - textWidth) / 2f);
        var x = textLeft + prefixWidth + 1f;
        var top = texturePosition.Y + 6f;
        var bottom = texturePosition.Y + texture.Height - 6f;
        _primitiveRenderer.DrawLine(new Vector2(x, top), new Vector2(x, bottom), color, 2f);
    }

    private Color GetInactiveColor(Color color, float opacity)
        => Blend(color, Theme.BackgroundColor, 0.62f) * opacity;

    private static Color Blend(Color from, Color to, float amount)
    {
        var clamped = MathHelper.Clamp(amount, 0f, 1f);
        return new Color(
            (byte)MathF.Round(MathHelper.Lerp(from.R, to.R, clamped)),
            (byte)MathF.Round(MathHelper.Lerp(from.G, to.G, clamped)),
            (byte)MathF.Round(MathHelper.Lerp(from.B, to.B, clamped)),
            (byte)MathF.Round(MathHelper.Lerp(from.A, to.A, clamped)));
    }

    /// <summary>
    /// ノードのリサイズハンドルの中心座標を取得します。
    /// </summary>
    /// <param name="node">対象のノード</param>
    /// <returns>リサイズハンドルの中心座標</returns>
    private static Vector2 GetNodeResizeHandleCenter(DiagramNode node)
        => node.Position + new Vector2(node.Radius, node.Radius);
}
