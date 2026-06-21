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

    public NodeRenderer(
        PrimitiveRenderer primitiveRenderer,
        SpriteBatch spriteBatch,
        Color[] palette,
        Func<string, bool, Texture2D> getLabelTexture)
    {
        _primitiveRenderer = primitiveRenderer;
        _spriteBatch = spriteBatch;
        _palette = palette;
        _getLabelTexture = getLabelTexture;
    }

    /// <summary>
    /// ノードを描画する
    /// </summary>
    /// <param name="node">描画するノード</param>
    /// <param name="selected">選択されているかどうか</param>
    /// <param name="editingNode">編集中のノード</param>
    /// <param name="editingLabel">編集中のラベル</param>
    public void DrawNode(DiagramNode node, bool selected, DiagramNode? editingNode, string editingLabel)
    {
        var fill = node.Kind == NodeKind.Normal && _palette.Length > 0
            ? _palette[node.ColorIndex % _palette.Length]
            : new Color(5, 6, 8);

        _primitiveRenderer.DrawCircle(node.Position, node.Radius + 4, selected ? new Color(255, 255, 255) : new Color(10, 12, 16));
        _primitiveRenderer.DrawCircle(node.Position, node.Radius, fill);

        // 通常ノード
        if (node.Kind == NodeKind.Normal)
        {
            _primitiveRenderer.DrawCircleOutline(node.Position, node.Radius, new Color(15, 18, 24), 3f);
        }
        // 開始ノード
        else if (node.Kind == NodeKind.Start)
        {
            _primitiveRenderer.DrawCircleOutline(node.Position, node.Radius - 1f, Color.White, 5f);
        }
        // 終了ノード
        else
        {
            _primitiveRenderer.DrawCircleOutline(node.Position, node.Radius - 2f, Color.White, 2f);
            _primitiveRenderer.DrawCircleOutline(node.Position, node.Radius - 7f, Color.White, 2f);
        }

        var label = node == editingNode ? editingLabel + "_" : node.Label;
        DrawNodeLabel(label, node.Position, node == editingNode);
    }

    /// <summary>
    /// 作成前の開始ノードを薄く描画します。
    /// </summary>
    /// <param name="node">描画する仮ノード</param>
    /// <param name="opacity">不透明度</param>
    public void DrawStartNodeGhost(DiagramNode node, float opacity)
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

    /// <summary>
    /// ノードのリサイズハンドルの中心座標を取得します。
    /// </summary>
    /// <param name="node">対象のノード</param>
    /// <returns>リサイズハンドルの中心座標</returns>
    private static Vector2 GetNodeResizeHandleCenter(DiagramNode node)
        => node.Position + new Vector2(node.Radius, node.Radius);
}
