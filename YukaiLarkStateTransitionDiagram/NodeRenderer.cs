namespace YukaiLarkStateTransitionDiagram;

using System;
using YukaiLarkStateTransitionDiagram.Theme;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

/// <summary>
/// ノードの描画
/// </summary>
public sealed class NodeRenderer
{
    private readonly PrimitiveRenderer _primitiveRenderer;
    private readonly SpriteBatch _spriteBatch;
    private readonly Func<string, bool, Texture2D> _getLabelTexture;

    public BoardTheme Theme { get; set; }

    public NodeRenderer(
        PrimitiveRenderer primitiveRenderer,
        SpriteBatch spriteBatch,
        Func<string, bool, Texture2D> getLabelTexture,
        BoardTheme theme)
    {
        _primitiveRenderer = primitiveRenderer;
        _spriteBatch = spriteBatch;
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
    public void DrawNode(DiagramNode node, bool selected, DiagramNode? editingNode, string editingLabel, int editingCaretIndex, bool showEditingCaret, TimeSpan totalGameTime, bool inactive = false, bool hovered = false)
    {
        var baseFill = node.Kind switch
        {
            NodeKind.Normal => GetNormalNodeFillColor(node),
            NodeKind.StartMarker => Theme.StartMarkerFillColor,
            _ => Theme.EndMarkerFillColor
        };
        var fill = inactive ? GetInactiveColor(baseFill, 0.38f) : baseFill;
        var outerColor = inactive
            ? GetInactiveColor(selected ? Theme.SelectedNodeOuterRingColor : Theme.NodeOuterRingColor, 0.52f)
            : selected ? Theme.SelectedNodeOuterRingColor : Theme.NodeOuterRingColor;
        var markerOutlineColor = node.Kind switch
        {
            NodeKind.StartMarker => Theme.StartMarkerOutlineColor,
            NodeKind.EndMarker => Theme.EndMarkerOutlineColor,
            _ => Theme.MarkerOutlineColor
        };
        var outlineColor = inactive ? GetInactiveColor(markerOutlineColor, 0.56f) : markerOutlineColor;
        var normalOutlineColor = inactive ? GetInactiveColor(Theme.NormalNodeOutlineColor, 0.56f) : Theme.NormalNodeOutlineColor;
        var labelColor = GetNodeLabelColor(baseFill, inactive);

        if (selected && !inactive)
        {
            DrawSelectedNodeGlow(node, totalGameTime);
        }

        var hideBaseOuterRing = node.Kind == NodeKind.Normal && hovered && !inactive;
        if (!hideBaseOuterRing)
        {
            _primitiveRenderer.DrawCircle(node.Position, node.Radius + 4, outerColor);
        }

        _primitiveRenderer.DrawCircle(node.Position, node.Radius, fill);
        if (selected && !inactive)
        {
            DrawSelectedNodeSweep(node, totalGameTime);
        }

        var drawHoveredOutline = hovered && !inactive;
        // 通常ノード
        if (node.Kind == NodeKind.Normal)
        {
            var normalOutlineRadius = hovered && !inactive ? node.Radius - 1f : node.Radius;
            DrawNodeOutlineCircle(node.Position, normalOutlineRadius, normalOutlineColor, 3f, drawHoveredOutline, totalGameTime, hoverThickness: 4.5f);
            if (node.SubstateDiagramId.HasValue)
            {
                var substateRingColor = selected && !inactive
                    ? Theme.SelectedNodeOuterRingColor
                    : Blend(normalOutlineColor, GetNormalNodeFillColor(node), 0.35f);
                var substateRingRadius = MathF.Max(6f, node.Radius - 8f);
                DrawNodeOutlineCircle(node.Position, substateRingRadius, substateRingColor, 2f, false, totalGameTime);
            }
        }
        // 開始マーク
        else if (node.Kind == NodeKind.StartMarker)
        {
            DrawNodeOutlineCircle(node.Position, node.Radius - 1f, outlineColor, 5f, drawHoveredOutline, totalGameTime);
        }
        // 終了マーク
        else
        {
            DrawNodeOutlineCircle(node.Position, node.Radius - 2f, outlineColor, 2f, drawHoveredOutline, totalGameTime);
            DrawNodeOutlineCircle(node.Position, node.Radius - 7f, outlineColor, 2f, drawHoveredOutline, totalGameTime, 1.2f);
        }

        if (node == editingNode)
        {
            var editorLabelColor = inactive ? labelColor : Theme.LabelEditorTextColor;
            DrawNodeLabel(editingLabel, node.Position, editing: true, editorLabelColor);
            if (showEditingCaret)
            {
                DrawEditingCaret(editingLabel, editingCaretIndex, node.Position, editorLabelColor);
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
        _primitiveRenderer.DrawCircle(node.Position, node.Radius + 8f, Theme.NodeGhostHaloColor * (alpha * 0.32f));
        _primitiveRenderer.DrawCircle(node.Position, node.Radius + 4f, Theme.NodeGhostInnerHaloColor * (alpha * 0.34f));
        _primitiveRenderer.DrawCircle(node.Position, node.Radius, Theme.StartMarkerFillColor * (alpha * 0.46f));
        _primitiveRenderer.DrawCircleOutline(node.Position, node.Radius - 1f, Theme.StartMarkerOutlineColor * (alpha * 0.74f), 5f);
        DrawNodeLabel(node.Label, node.Position, editing: false, GetNodeLabelColor(Theme.StartMarkerFillColor, inactive: false) * (alpha * 0.74f));
    }

    /// <summary>
    /// 作成前の通常ノードを薄く描画します。
    /// </summary>
    /// <param name="node">描画する仮ノード</param>
    /// <param name="opacity">不透明度</param>
    public void DrawStateNodeGhost(DiagramNode node, float opacity)
    {
        var alpha = MathHelper.Clamp(opacity, 0f, 1f);
        var fill = GetNormalNodeFillColor(node);

        _primitiveRenderer.DrawCircle(node.Position, node.Radius + 8f, fill * (alpha * 0.26f));
        _primitiveRenderer.DrawCircle(node.Position, node.Radius + 4f, Theme.NodeGhostInnerHaloColor * (alpha * 0.3f));
        _primitiveRenderer.DrawCircle(node.Position, node.Radius, fill * (alpha * 0.54f));
        _primitiveRenderer.DrawCircleOutline(node.Position, node.Radius, Theme.NormalNodeOutlineColor * (alpha * 0.72f), 3f);
        DrawNodeLabel(node.Label, node.Position, editing: false, GetNodeLabelColor(fill, inactive: false) * (alpha * 0.78f));
    }

    public void DrawStateNodeLabelEditGhost(DiagramNode node, TimeSpan totalGameTime)
    {
        var pulse = 0.5f + (MathF.Sin((float)totalGameTime.TotalSeconds * 5.8f) * 0.5f);
        var bob = MathF.Sin((float)totalGameTime.TotalSeconds * 8.5f) * 2.4f;
        var center = node.Position + new Vector2(0f, bob);
        var fill = GetNormalNodeFillColor(node);

        _primitiveRenderer.DrawCircle(center, node.Radius + 14f, fill * MathHelper.Lerp(0.10f, 0.18f, pulse));
        _primitiveRenderer.DrawCircle(center, node.Radius + 9f, Theme.NodeGhostHaloColor * MathHelper.Lerp(0.22f, 0.34f, pulse));
        _primitiveRenderer.DrawCircleOutline(center, node.Radius + 7f, Theme.NodeGhostInnerHaloColor * MathHelper.Lerp(0.60f, 0.82f, pulse), 3.2f);
        DrawWobblingCircle(center, node.Radius + 13f, Theme.NodeGhostHaloColor * MathHelper.Lerp(0.58f, 0.86f, pulse), 2.4f, totalGameTime, 1.8f);
    }

    /// <summary>
    /// ノードのリサイズハンドルを描画します。
    /// </summary>
    /// <param name="node">描画するノード</param>
    public void DrawNodeResizeHandle(DiagramNode node)
    {
        var center = GetNodeResizeHandleCenter(node);
        _primitiveRenderer.DrawLine(node.Position + new Vector2(node.Radius * 0.72f, node.Radius * 0.72f), center, Theme.NodeResizeHandleColor, 2f);
        _primitiveRenderer.DrawHandle(center, Theme.NodeResizeHandleColor, Theme.HandleOutlineColor);
    }

    /// <summary>
    /// ドラッグ中のノード中心がグリッドに吸着していることを示す丸を描画します。
    /// </summary>
    /// <param name="node">ドラッグ中のノード</param>
    /// <param name="totalGameTime">経過時間</param>
    public void DrawNodeSnapIndicator(DiagramNode node, TimeSpan totalGameTime)
    {
        var pulse = 0.5f + (MathF.Sin((float)totalGameTime.TotalSeconds * 7.2f) * 0.5f);
        var indicatorColor = GetNodeSnapIndicatorColor(node);

        _primitiveRenderer.DrawCircle(node.Position, 9f, Theme.HandleOutlineColor * MathHelper.Lerp(0.42f, 0.58f, pulse));
        _primitiveRenderer.DrawCircle(node.Position, 6f, indicatorColor * MathHelper.Lerp(0.82f, 1f, pulse));
        _primitiveRenderer.DrawCircleOutline(node.Position, 8f, Theme.PhotoPaperColor * MathHelper.Lerp(0.72f, 0.92f, pulse), 2f);
    }

    public Color GetNodeSnapIndicatorColor(DiagramNode node)
    {
        var fill = node.Kind switch
        {
            NodeKind.Normal => GetNormalNodeFillColor(node),
            NodeKind.StartMarker => Theme.StartMarkerFillColor,
            _ => Theme.EndMarkerFillColor
        };
        return Blend(fill, Theme.SelectedTransitionLineColor, 0.44f);
    }

    private void DrawNodeOutlineCircle(Vector2 center, float radius, Color color, float thickness, bool hovered, TimeSpan totalGameTime, float amplitude = 2.1f, float? hoverThickness = null)
    {
        if (hovered)
        {
            DrawWobblingCircle(center, radius, color, hoverThickness ?? thickness, totalGameTime, amplitude);
            return;
        }

        _primitiveRenderer.DrawCircleOutline(center, radius, color, thickness);
    }

    private void DrawWobblingCircle(Vector2 center, float radius, Color color, float thickness, TimeSpan totalGameTime, float amplitude)
    {
        const int segments = 96;
        var phase = (float)totalGameTime.TotalSeconds * 18f;
        var previous = GetWobblingCirclePoint(center, radius, 0f, phase, amplitude);

        for (var i = 1; i <= segments; i++)
        {
            var angle = MathHelper.TwoPi * i / segments;
            var current = GetWobblingCirclePoint(center, radius, angle, phase, amplitude);
            _primitiveRenderer.DrawLine(previous, current, color, thickness);
            previous = current;
        }
    }

    private static Vector2 GetWobblingCirclePoint(Vector2 center, float radius, float angle, float phase, float amplitude)
    {
        var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        var wobble = MathF.Sin(phase + (angle * 5f)) * amplitude;
        return center + (direction * (radius + wobble));
    }

    private void DrawSelectedNodeGlow(DiagramNode node, TimeSpan totalGameTime)
    {
        var pulse = 0.5f + (MathF.Sin((float)totalGameTime.TotalSeconds * 4.4f) * 0.5f);
        _primitiveRenderer.DrawCircle(node.Position, node.Radius + 10f, Theme.NodeSelectedGlowColor * MathHelper.Lerp(0.12f, 0.22f, pulse));
        _primitiveRenderer.DrawCircle(node.Position, node.Radius + 6f, Theme.NodeSelectedInnerGlowColor * MathHelper.Lerp(0.18f, 0.28f, pulse));
    }

    private void DrawSelectedNodeSweep(DiagramNode node, TimeSpan totalGameTime)
    {
        var progress = (float)(totalGameTime.TotalSeconds * 0.58 % 1.0);
        var sweepCenter = MathHelper.Lerp(-node.Radius, node.Radius, progress);
        var sweepHalfHeight = MathF.Max(18f, node.Radius * 0.48f);

        for (var y = -node.Radius; y <= node.Radius; y++)
        {
            var distance = MathF.Abs(y - sweepCenter);
            if (distance > sweepHalfHeight)
            {
                continue;
            }

            var halfWidth = (float)Math.Sqrt(node.Radius * node.Radius - y * y);
            var strength = 1f - (distance / sweepHalfHeight);
            var alpha = 0.05f + (strength * 0.22f);
            var rectangle = new Rectangle(
                (int)(node.Position.X - halfWidth),
                (int)(node.Position.Y + y),
                (int)(halfWidth * 2),
                1);
            _primitiveRenderer.DrawPixelRectangle(rectangle, Theme.NodeSelectedSweepColor * alpha);
        }
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
        if (editing)
        {
            DrawLabelEditorBackground(position, texture);
        }

        _spriteBatch.Draw(texture, position, color);
    }

    private void DrawLabelEditorBackground(Vector2 position, Texture2D texture)
    {
        var bounds = new Rectangle((int)position.X, (int)position.Y, texture.Width, texture.Height);
        _primitiveRenderer.DrawPixelRectangle(bounds, Theme.LabelEditorBackgroundColor);
        _primitiveRenderer.DrawLine(new Vector2(bounds.Left, bounds.Top), new Vector2(bounds.Right, bounds.Top), Theme.LabelEditorBorderColor, 2f);
        _primitiveRenderer.DrawLine(new Vector2(bounds.Right, bounds.Top), new Vector2(bounds.Right, bounds.Bottom), Theme.LabelEditorBorderColor, 2f);
        _primitiveRenderer.DrawLine(new Vector2(bounds.Right, bounds.Bottom), new Vector2(bounds.Left, bounds.Bottom), Theme.LabelEditorBorderColor, 2f);
        _primitiveRenderer.DrawLine(new Vector2(bounds.Left, bounds.Bottom), new Vector2(bounds.Left, bounds.Top), Theme.LabelEditorBorderColor, 2f);
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


    private Color GetNormalNodeFillColor(DiagramNode node)
    {
        var palette = Theme.NormalNodePalette;
        return palette[node.ColorIndex % palette.Length];
    }
    private Color GetNodeLabelColor(Color fill, bool inactive)
    {
        if (inactive)
        {
            return Theme.PanelMutedTextColor * 0.72f;
        }

        return GetReadableTextColor(fill);
    }

    private Color GetReadableTextColor(Color fill)
    {
        var darkText = WithAlpha(Blend(Theme.TransitionLabelColor, Color.Black, 0.68f), 248);
        var lightText = WithAlpha(Theme.PhotoPaperColor, 248);
        var darkContrast = GetContrastRatio(fill, darkText);
        var lightContrast = GetContrastRatio(fill, lightText);
        var bestText = darkContrast >= lightContrast ? darkText : lightText;
        var bestContrast = MathF.Max(darkContrast, lightContrast);

        if (bestContrast >= 4.5f)
        {
            return bestText;
        }

        var blackContrast = GetContrastRatio(fill, Color.Black);
        var whiteContrast = GetContrastRatio(fill, Color.White);
        return blackContrast >= whiteContrast
            ? WithAlpha(Color.Black, 248)
            : WithAlpha(Color.White, 248);
    }

    private static float GetContrastRatio(Color first, Color second)
    {
        var firstLuminance = GetRelativeLuminance(first);
        var secondLuminance = GetRelativeLuminance(second);
        var lighter = MathF.Max(firstLuminance, secondLuminance);
        var darker = MathF.Min(firstLuminance, secondLuminance);
        return (lighter + 0.05f) / (darker + 0.05f);
    }

    private static float GetRelativeLuminance(Color color)
        => (0.2126f * GetLinearChannel(color.R))
            + (0.7152f * GetLinearChannel(color.G))
            + (0.0722f * GetLinearChannel(color.B));

    private static float GetLinearChannel(byte channel)
    {
        var value = channel / 255f;
        return value <= 0.03928f
            ? value / 12.92f
            : MathF.Pow((value + 0.055f) / 1.055f, 2.4f);
    }
    private static Color WithAlpha(Color color, byte alpha)
        => new(color.R, color.G, color.B, alpha);
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
