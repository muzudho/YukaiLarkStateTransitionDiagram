namespace YukaiLarkStateTransitionDiagram;

using System;
using System.Collections.Generic;
using YukaiLarkStateTransitionDiagram.Theme;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public sealed class EdgeRenderer
{
    private const float TransitionLineThickness = 3f;

    private readonly PrimitiveRenderer _primitiveRenderer;
    private readonly SpriteBatch _spriteBatch;
    private readonly Func<string, bool, Texture2D> _getLabelTexture;

    public EdgeRenderer(
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
    /// テーマ
    /// </summary>
    public BoardTheme Theme { get; set; }


    public void DrawTransitionPath(
        DiagramTransition transition,
        IReadOnlyList<Vector2> points,
        bool selected,
        bool editing,
        string editingLabel,
        int editingCaretIndex,
        bool showEditingCaret,
        bool drawStartMarkerFlowLine)
    {
        var lineColor = selected ? Theme.SelectedTransitionLineColor : Theme.TransitionLineColor;
        const float thickness = TransitionLineThickness;

        if (selected)
        {
            DrawSelectedTransitionPathEffect(points, transition.SegmentControls);
        }

        if (drawStartMarkerFlowLine)
        {
            DrawDoublePathArrow(points, transition.SegmentControls, lineColor, selected);
            return;
        }

        DrawPathArrow(points, transition.SegmentControls, lineColor, thickness);
        DrawTransitionPathLabel(transition, points, selected, editing, editingLabel, editingCaretIndex, showEditingCaret);
    }

    public void DrawTransitionPathHandles(DiagramTransition transition, IReadOnlyList<Vector2> points)
    {
        if (points.Count < 2)
        {
            return;
        }

        _primitiveRenderer.DrawHandle(points[0], Theme.TransitionHandleColor, Theme.HandleOutlineColor);
        _primitiveRenderer.DrawHandle(points[^1], Theme.TransitionHandleColor, Theme.HandleOutlineColor);
        for (var i = 0; i < points.Count - 1; i++)
        {
            GetTransitionPathSegmentControlPoints(points, i, transition.SegmentControls, out var control1, out var control2);
            _primitiveRenderer.DrawLine(points[i], control1, Theme.TransitionGuideColor, 1f);
            _primitiveRenderer.DrawLine(points[i + 1], control2, Theme.TransitionGuideColor, 1f);
            DrawTransitionControlHandle(control1);
            DrawTransitionControlHandle(control2);
        }
        for (var i = 1; i < points.Count - 1; i++)
        {
            _primitiveRenderer.DrawCircle(points[i], 7.5f, Theme.TransitionControlHandleColor);
            _primitiveRenderer.DrawCircleOutline(points[i], 10f, Theme.TransitionHandleColor, 2f);
        }
    }

    public void DrawTransitionPathGhost(IReadOnlyList<Vector2> points, float opacity)
    {
        var alpha = MathHelper.Clamp(opacity, 0f, 1f);
        DrawPathArrow(points, null, Theme.SelectedTransitionLineColor * (alpha * 0.68f), 5f);
        DrawPathArrow(points, null, Theme.TransitionLineColor * (alpha * 0.42f), 3f);
    }

    public void DrawTransitionPathHoverCue(DiagramTransition transition, IReadOnlyList<Vector2> points, TimeSpan totalGameTime)
    {
        _ = totalGameTime;
        DrawPathStroke(points, transition.SegmentControls, Theme.TransitionLineColor * 0.28f, 8f);
        DrawPathStroke(points, transition.SegmentControls, Theme.TransitionLineColor * 0.9f, 4f);
    }

    /// <summary>
    /// エッジを描く
    /// </summary>
    /// <param name="transition"></param>
    /// <param name="start"></param>
    /// <param name="control1"></param>
    /// <param name="control2"></param>
    /// <param name="end"></param>
    /// <param name="selected"></param>
    /// <param name="editing"></param>
    /// <param name="editingLabel"></param>
    public void DrawTransition(
        DiagramTransition transition,
        Vector2 start,
        Vector2 control1,
        Vector2 control2,
        Vector2 end,
        bool selected,
        bool editing,
        string editingLabel,
        int editingCaretIndex,
        bool showEditingCaret,
        bool drawStartMarkerFlowLine)
    {
        var lineColor = selected ? Theme.SelectedTransitionLineColor : Theme.TransitionLineColor;
        const float thickness = TransitionLineThickness;

        if (drawStartMarkerFlowLine)
        {
            if (selected)
            {
                DrawSelectedTransitionEffect(start, control1, control2, end);
            }

            DrawDoubleBezierArrow(start, control1, control2, end, lineColor, selected);
            return;
        }

        if (selected)
        {
            DrawSelectedTransitionEffect(start, control1, control2, end);
        }

        DrawBezierArrow(start, control1, control2, end, lineColor, thickness);
        DrawTransitionLabel(transition, start, control1, control2, end, selected, editing, editingLabel, editingCaretIndex, showEditingCaret);
    }

    /// <summary>
    /// ベジェ曲線の矢印を描く
    /// </summary>
    /// <param name="start"></param>
    /// <param name="control1"></param>
    /// <param name="control2"></param>
    /// <param name="end"></param>
    public void DrawTransitionHandles(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end)
    {
        _primitiveRenderer.DrawLine(start, control1, Theme.TransitionGuideColor, 1f);
        _primitiveRenderer.DrawLine(end, control2, Theme.TransitionGuideColor, 1f);
        _primitiveRenderer.DrawHandle(start, Theme.TransitionHandleColor, Theme.HandleOutlineColor);
        _primitiveRenderer.DrawHandle(end, Theme.TransitionHandleColor, Theme.HandleOutlineColor);
        DrawTransitionControlHandle(control1);
        DrawTransitionControlHandle(control2);
    }


    public void DrawTransitionEndpointHandles(Vector2 start, Vector2 end)
    {
        _primitiveRenderer.DrawHandle(start, Theme.TransitionHandleColor, Theme.HandleOutlineColor);
        _primitiveRenderer.DrawHandle(end, Theme.TransitionHandleColor, Theme.HandleOutlineColor);
    }

    private void DrawTransitionControlHandle(Vector2 center)
    {
        _primitiveRenderer.DrawDiamondHandle(center, Theme.TransitionControlHandleColor, Theme.HandleOutlineColor);
    }

    /// <summary>
    /// ベジェ曲線の矢印を描く
    /// </summary>
    /// <param name="start"></param>
    /// <param name="control1"></param>
    /// <param name="control2"></param>
    /// <param name="end"></param>
    public void DrawTransitionHoverCue(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end, TimeSpan totalGameTime)
    {
        DrawWobblingBezierStroke(start, control1, control2, end, totalGameTime);
    }

    /// <summary>
    /// 作成前の遷移を薄く描画します。
    /// </summary>
    /// <param name="start"></param>
    /// <param name="control1"></param>
    /// <param name="control2"></param>
    /// <param name="end"></param>
    /// <param name="opacity">不透明度</param>
    public void DrawTransitionGhost(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end, float opacity)
    {
        var alpha = MathHelper.Clamp(opacity, 0f, 1f);
        DrawBezierArrow(start, control1, control2, end, Theme.SelectedTransitionLineColor * (alpha * 0.68f), 5f);
        DrawBezierArrow(start, control1, control2, end, Theme.TransitionLineColor * (alpha * 0.42f), 3f);
    }

    /// <summary>
    /// ベジェ曲線の矢印を描く
    /// </summary>
    /// <param name="center"></param>
    public void DrawTransitionHandleCue(Vector2 center, bool controlHandle = false)
    {
        if (controlHandle)
        {
            _primitiveRenderer.DrawDiamondHandle(center, Theme.SelectedTransitionLineColor * 0.72f, Theme.SelectedTransitionLineColor);
            return;
        }

        _primitiveRenderer.DrawCircleOutline(center, 13f, Theme.SelectedTransitionLineColor, 3f);
    }

    /// <summary>
    /// リンクプレビューを描く。
    /// </summary>
    /// <param name="start"></param>
    /// <param name="end"></param>
    public void DrawLinkPreview(Vector2 start, Vector2 end)
    {
        DrawArrow(start, end, Theme.SelectedTransitionLineColor, 3f);
    }


    private void DrawTransitionPathLabel(
        DiagramTransition transition,
        IReadOnlyList<Vector2> points,
        bool selected,
        bool editing,
        string editingLabel,
        int editingCaretIndex,
        bool showEditingCaret)
    {
        if (editing)
        {
            DrawTransitionPathEventEditor(transition, points, editingLabel, editingCaretIndex, showEditingCaret, 1f);
            return;
        }

        var label = transition.Label;
        if (string.IsNullOrWhiteSpace(label))
        {
            return;
        }

        var texture = _getLabelTexture(label, false);
        var center = GetTransitionPathLabelCenter(points, transition, texture.Width, texture.Height);
        var position = center - new Vector2(texture.Width / 2f, texture.Height / 2f);
        DrawTransitionPathLabelLeader(transition, points, position, texture, selected ? 0.72f : 0.46f);
        _spriteBatch.Draw(texture, position, selected ? Theme.SelectedTransitionLabelColor : Theme.TransitionLabelColor);
        if (selected)
        {
            DrawSelectedTransitionLabelUnderline(position, texture, label);
        }
    }

    private void DrawTransitionPathEventEditor(
        DiagramTransition transition,
        IReadOnlyList<Vector2> points,
        string editingLabel,
        int editingCaretIndex,
        bool showCaret,
        float opacity)
    {
        var isEmpty = string.IsNullOrEmpty(editingLabel);
        var displayLabel = isEmpty ? "イベント名" : editingLabel;
        var texture = _getLabelTexture(displayLabel, true);
        var center = GetTransitionPathLabelCenter(points, transition, texture.Width, texture.Height);
        var position = center - new Vector2(texture.Width / 2f, texture.Height / 2f);
        var alpha = MathHelper.Clamp(opacity, 0f, 1f);
        DrawTransitionPathLabelLeader(transition, points, position, texture, 0.58f * alpha);
        var labelColor = isEmpty
            ? Theme.LabelEditorPlaceholderTextColor * alpha
            : Theme.LabelEditorTextColor * alpha;
        DrawLabelEditorBackground(position, texture);
        _spriteBatch.Draw(texture, position, labelColor);

        if (showCaret && alpha > 0f)
        {
            var caretLabel = isEmpty ? displayLabel : editingLabel;
            var caretIndex = isEmpty ? 0 : Math.Clamp(editingCaretIndex, 0, caretLabel.Length);
            DrawEditingCaret(position, texture, caretLabel, caretIndex, Theme.LabelEditorTextColor * alpha);
        }
    }

    private void DrawTransitionPathLabelLeader(DiagramTransition transition, IReadOnlyList<Vector2> points, Vector2 labelPosition, Texture2D texture, float opacity)
    {
        var anchor = GetTransitionPathPoint(points, MathHelper.Clamp(transition.LabelAnchorT, 0f, 1f), transition.SegmentControls);
        var left = labelPosition.X;
        var top = labelPosition.Y;
        var right = labelPosition.X + texture.Width;
        var bottom = labelPosition.Y + texture.Height;
        var attach = new Vector2(MathHelper.Clamp(anchor.X, left, right), MathHelper.Clamp(anchor.Y, top, bottom));
        if (Vector2.Distance(anchor, attach) <= 10f)
        {
            return;
        }
        _primitiveRenderer.DrawLine(anchor, attach, Theme.TransitionGuideColor * opacity, 1f);
    }

    public static Vector2 GetTransitionPathLabelCenter(IReadOnlyList<Vector2> points, DiagramTransition transition, int labelWidth, int labelHeight)
    {
        var anchorT = MathHelper.Clamp(transition.LabelAnchorT, 0f, 1f);
        var anchor = GetTransitionPathPoint(points, anchorT, transition.SegmentControls);
        if (transition.LabelOffset.HasValue)
        {
            return anchor + transition.LabelOffset.Value;
        }

        var tangent = GetTransitionPathTangent(points, anchorT, transition.SegmentControls);
        var side = transition.LabelSide == 0 ? -1f : 1f;
        if (MathF.Abs(tangent.X) >= MathF.Abs(tangent.Y))
        {
            return anchor + new Vector2(0, side * (labelHeight / 2f + 18f));
        }
        return anchor + new Vector2(side * (labelWidth / 2f + 22f), 0);
    }

    /// <summary>
    /// ラベルを描く。
    /// </summary>
    /// <param name="transition"></param>
    /// <param name="start"></param>
    /// <param name="control1"></param>
    /// <param name="control2"></param>
    /// <param name="end"></param>
    /// <param name="selected"></param>
    /// <param name="editing"></param>
    /// <param name="editingLabel"></param>
    private void DrawTransitionLabel(
        DiagramTransition transition,
        Vector2 start,
        Vector2 control1,
        Vector2 control2,
        Vector2 end,
        bool selected,
        bool editing,
        string editingLabel,
        int editingCaretIndex,
        bool showEditingCaret)
    {
        if (editing)
        {
            DrawTransitionEventEditor(transition, start, control1, control2, end, editingLabel, editingCaretIndex, showEditingCaret, 1f);
            return;
        }

        var label = transition.Label;
        if (string.IsNullOrWhiteSpace(label))
        {
            return;
        }

        var texture = _getLabelTexture(label, false);
        var center = GetTransitionLabelCenter(start, control1, control2, end, transition, texture);
        var position = center - new Vector2(texture.Width / 2f, texture.Height / 2f);
        DrawTransitionLabelLeader(transition, start, control1, control2, end, position, texture, selected ? 0.72f : 0.46f);
        _spriteBatch.Draw(texture, position, selected ? Theme.SelectedTransitionLabelColor : Theme.TransitionLabelColor);
        if (selected)
        {
            DrawSelectedTransitionLabelUnderline(position, texture, label);
        }
    }

    private void DrawSelectedTransitionLabelUnderline(Vector2 texturePosition, Texture2D texture, string label)
    {
        var textWidth = TextRenderer.MeasureLabelTextWidth(label);
        var left = texturePosition.X + ((texture.Width - textWidth) / 2f);
        var right = left + textWidth;
        var y = texturePosition.Y + texture.Height - 3f;
        _primitiveRenderer.DrawLine(new Vector2(left, y), new Vector2(right, y), Theme.SelectedTransitionLabelColor, 4f);
    }

    /// <summary>
    /// 作成前、または編集中のイベント入力欄を描画します。
    /// </summary>
    /// <param name="transition"></param>
    /// <param name="start"></param>
    /// <param name="control1"></param>
    /// <param name="control2"></param>
    /// <param name="end"></param>
    /// <param name="opacity">不透明度</param>
    public void DrawTransitionEventGhost(DiagramTransition transition, Vector2 start, Vector2 control1, Vector2 control2, Vector2 end, float opacity)
    {
        DrawTransitionEventEditor(transition, start, control1, control2, end, string.Empty, 0, false, opacity);
    }

    private void DrawTransitionEventEditor(
        DiagramTransition transition,
        Vector2 start,
        Vector2 control1,
        Vector2 control2,
        Vector2 end,
        string editingLabel,
        int editingCaretIndex,
        bool showCaret,
        float opacity)
    {
        var isEmpty = string.IsNullOrEmpty(editingLabel);
        var displayLabel = isEmpty ? "イベント名" : editingLabel;
        var texture = _getLabelTexture(displayLabel, true);
        var center = GetTransitionLabelCenter(start, control1, control2, end, transition, texture);
        var position = center - new Vector2(texture.Width / 2f, texture.Height / 2f);
        var alpha = MathHelper.Clamp(opacity, 0f, 1f);
        DrawTransitionLabelLeader(transition, start, control1, control2, end, position, texture, 0.58f * alpha);
        var labelColor = isEmpty
            ? Theme.LabelEditorPlaceholderTextColor * alpha
            : Theme.LabelEditorTextColor * alpha;
        DrawLabelEditorBackground(position, texture);
        _spriteBatch.Draw(texture, position, labelColor);

        if (showCaret && alpha > 0f)
        {
            var caretLabel = isEmpty ? displayLabel : editingLabel;
            var caretIndex = isEmpty ? 0 : Math.Clamp(editingCaretIndex, 0, caretLabel.Length);
            DrawEditingCaret(position, texture, caretLabel, caretIndex, Theme.LabelEditorTextColor * alpha);
        }
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

    private void DrawEditingCaret(Vector2 texturePosition, Texture2D texture, string label, int caretIndex, Color color)
    {
        var textWidth = TextRenderer.MeasureLabelTextWidth(label);
        var prefixWidth = caretIndex <= 0
            ? 0f
            : TextRenderer.MeasureLabelTextWidth(label[..caretIndex]);
        var textLeft = texturePosition.X + ((texture.Width - textWidth) / 2f);
        var x = textLeft + prefixWidth + 1f;
        var top = texturePosition.Y + 6f;
        var bottom = texturePosition.Y + texture.Height - 6f;
        _primitiveRenderer.DrawLine(new Vector2(x, top), new Vector2(x, bottom), color, 2f);
    }

    /// <summary>
    /// ラベルの中心を取得
    /// </summary>
    /// <param name="start"></param>
    /// <param name="control1"></param>
    /// <param name="control2"></param>
    /// <param name="end"></param>
    /// <param name="labelSide"></param>
    /// <param name="labelTexture"></param>
    /// <returns></returns>
    public static Vector2 GetTransitionLabelCenter(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end, DiagramTransition transition, Texture2D labelTexture)
        => GetTransitionLabelCenter(start, control1, control2, end, transition, labelTexture.Width, labelTexture.Height);

    public static Vector2 GetTransitionLabelCenter(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end, DiagramTransition transition, int labelWidth, int labelHeight)
    {
        var anchorT = MathHelper.Clamp(transition.LabelAnchorT, 0f, 1f);
        var anchor = CubicBezier(start, control1, control2, end, anchorT);
        if (transition.LabelOffset.HasValue)
        {
            return anchor + transition.LabelOffset.Value;
        }

        var tangent = CubicBezierTangent(start, control1, control2, end, anchorT);
        var side = transition.LabelSide == 0 ? -1f : 1f;
        if (MathF.Abs(tangent.X) >= MathF.Abs(tangent.Y))
        {
            return anchor + new Vector2(0, side * (labelHeight / 2f + 18f));
        }

        return anchor + new Vector2(side * (labelWidth / 2f + 22f), 0);
    }

    public static Vector2 GetTransitionLabelAnchor(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end, DiagramTransition transition)
        => CubicBezier(start, control1, control2, end, MathHelper.Clamp(transition.LabelAnchorT, 0f, 1f));

    public static Vector2 GetTransitionPoint(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end, float t)
        => CubicBezier(start, control1, control2, end, MathHelper.Clamp(t, 0f, 1f));

    private void DrawTransitionLabelLeader(
        DiagramTransition transition,
        Vector2 start,
        Vector2 control1,
        Vector2 control2,
        Vector2 end,
        Vector2 labelPosition,
        Texture2D texture,
        float opacity)
    {
        var anchor = GetTransitionLabelAnchor(start, control1, control2, end, transition);
        var left = labelPosition.X;
        var top = labelPosition.Y;
        var right = labelPosition.X + texture.Width;
        var bottom = labelPosition.Y + texture.Height;
        var attach = new Vector2(
            MathHelper.Clamp(anchor.X, left, right),
            MathHelper.Clamp(anchor.Y, top, bottom));

        if (Vector2.Distance(anchor, attach) <= 10f)
        {
            return;
        }

        _primitiveRenderer.DrawLine(anchor, attach, Theme.TransitionGuideColor * opacity, 1f);
    }

    /// <summary>
    /// ベジェ曲線の点を計算
    /// </summary>
    /// <param name="start"></param>
    /// <param name="control1"></param>
    /// <param name="control2"></param>
    /// <param name="end"></param>
    /// <param name="color"></param>
    /// <param name="thickness"></param>
    private void DrawBezierArrow(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end, Color color, float thickness)
    {
        const int segments = 32;
        var previous = start;
        for (var i = 1; i <= segments; i++)
        {
            var t = i / (float)segments;
            var current = CubicBezier(start, control1, control2, end, t);
            _primitiveRenderer.DrawLine(previous, current, color, thickness);
            previous = current;
        }

        var tangent = CubicBezierTangent(start, control1, control2, end, 1f);

        // 矢終尻の位置
        var arrowEndLocation = end;

        // 矢終を描く。
        DrawArrowHead(arrowEndLocation, tangent, color, thickness);
    }

    private void DrawDoubleBezierArrow(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end, Color color, bool selected)
    {
        const int segments = 32;
        var halfGap = selected ? 4.2f : 3.6f;
        var lineThickness = selected ? 2.4f : 2f;
        Vector2? previousLeft = null;
        Vector2? previousRight = null;

        for (var i = 0; i <= segments; i++)
        {
            var t = i / (float)segments;
            var point = CubicBezier(start, control1, control2, end, t);
            var tangent = CubicBezierTangent(start, control1, control2, end, t);
            if (tangent.LengthSquared() <= 0.01f)
            {
                tangent = end - start;
            }

            var normal = tangent.LengthSquared() > 0.01f
                ? Vector2.Normalize(new Vector2(-tangent.Y, tangent.X))
                : Vector2.UnitY;
            var left = point + normal * halfGap;
            var right = point - normal * halfGap;

            if (previousLeft.HasValue && previousRight.HasValue)
            {
                _primitiveRenderer.DrawLine(previousLeft.Value, left, color, lineThickness);
                _primitiveRenderer.DrawLine(previousRight.Value, right, color, lineThickness);
            }

            previousLeft = left;
            previousRight = right;
        }

        DrawArrowHead(end, CubicBezierTangent(start, control1, control2, end, 1f), color, TransitionLineThickness);
    }

    private void DrawSelectedTransitionEffect(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end)
    {
        DrawBezierStroke(start, control1, control2, end, Theme.SelectedTransitionLineColor * 0.22f, 12f);
        DrawBezierStroke(start, control1, control2, end, Theme.SelectedTransitionLabelColor * 0.34f, 7f);
        DrawTransitionSelectionMarkers(start, control1, control2, end);
    }

    private void DrawBezierStroke(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end, Color color, float thickness)
    {
        const int segments = 32;
        var previous = start;
        for (var i = 1; i <= segments; i++)
        {
            var t = i / (float)segments;
            var current = CubicBezier(start, control1, control2, end, t);
            _primitiveRenderer.DrawLine(previous, current, color, thickness);
            previous = current;
        }
    }

    private void DrawTransitionSelectionMarkers(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end)
    {
        ReadOnlySpan<float> markerPositions = [0.22f, 0.5f, 0.78f];
        foreach (var t in markerPositions)
        {
            var center = CubicBezier(start, control1, control2, end, t);
            _primitiveRenderer.DrawCircle(center, 5.5f, Theme.SelectedTransitionLabelColor * 0.72f);
            _primitiveRenderer.DrawCircleOutline(center, 7.5f, Theme.SelectedTransitionLineColor * 0.78f, 2f);
        }
    }

    private void DrawWobblingBezierStroke(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end, TimeSpan totalGameTime)
    {
        const int segments = 32;
        const float wobbleAmplitude = 2.2f;
        var phase = (float)totalGameTime.TotalSeconds * 18f;
        var previous = GetWobblingBezierPoint(start, control1, control2, end, 0f, phase, wobbleAmplitude);

        for (var i = 1; i <= segments; i++)
        {
            var t = i / (float)segments;
            var current = GetWobblingBezierPoint(start, control1, control2, end, t, phase, wobbleAmplitude);
            _primitiveRenderer.DrawLine(previous, current, Theme.TransitionLineColor * 0.28f, 8f);
            _primitiveRenderer.DrawLine(previous, current, Theme.TransitionLineColor * 0.9f, 4f);
            previous = current;
        }
    }

    private static Vector2 GetWobblingBezierPoint(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end, float t, float phase, float amplitude)
    {
        var point = CubicBezier(start, control1, control2, end, t);
        var tangent = CubicBezierTangent(start, control1, control2, end, t);
        if (tangent.LengthSquared() <= 0.01f)
        {
            tangent = end - start;
        }

        var normal = tangent.LengthSquared() > 0.01f
            ? Vector2.Normalize(new Vector2(-tangent.Y, tangent.X))
            : Vector2.UnitY;
        var wobble = MathF.Sin(phase + (t * MathHelper.TwoPi * 3f)) * amplitude;
        return point + (normal * wobble);
    }


    private void DrawPathArrow(IReadOnlyList<Vector2> points, IReadOnlyList<TransitionSegmentControls>? segmentControls, Color color, float thickness)
    {
        DrawPathStroke(points, segmentControls, color, thickness);
        if (points.Count >= 2)
        {
            DrawArrowHead(points[^1], GetTransitionPathTangent(points, 1f, segmentControls), color, thickness);
        }
    }

    private void DrawPathStroke(IReadOnlyList<Vector2> points, IReadOnlyList<TransitionSegmentControls>? segmentControls, Color color, float thickness)
    {
        for (var i = 0; i < points.Count - 1; i++)
        {
            GetTransitionPathSegmentControlPoints(points, i, segmentControls, out var control1, out var control2);
            DrawBezierStroke(points[i], control1, control2, points[i + 1], color, thickness);
        }
    }

    private void DrawDoublePathArrow(IReadOnlyList<Vector2> points, IReadOnlyList<TransitionSegmentControls>? segmentControls, Color color, bool selected)
    {
        const int segmentsPerPathSegment = 24;
        var halfGap = selected ? 4.2f : 3.6f;
        var lineThickness = selected ? 2.4f : 2f;
        Vector2? previousLeft = null;
        Vector2? previousRight = null;

        for (var segment = 0; segment < points.Count - 1; segment++)
        {
            GetTransitionPathSegmentControlPoints(points, segment, segmentControls, out var control1, out var control2);
            for (var i = 0; i <= segmentsPerPathSegment; i++)
            {
                var t = i / (float)segmentsPerPathSegment;
                var point = CubicBezier(points[segment], control1, control2, points[segment + 1], t);
                var tangent = CubicBezierTangent(points[segment], control1, control2, points[segment + 1], t);
                if (tangent.LengthSquared() <= 0.01f)
                {
                    tangent = points[segment + 1] - points[segment];
                }

                var normal = tangent.LengthSquared() > 0.01f
                    ? Vector2.Normalize(new Vector2(-tangent.Y, tangent.X))
                    : Vector2.UnitY;
                var left = point + normal * halfGap;
                var right = point - normal * halfGap;

                if (previousLeft.HasValue && previousRight.HasValue)
                {
                    _primitiveRenderer.DrawLine(previousLeft.Value, left, color, lineThickness);
                    _primitiveRenderer.DrawLine(previousRight.Value, right, color, lineThickness);
                }

                previousLeft = left;
                previousRight = right;
            }
        }

        if (points.Count >= 2)
        {
            DrawArrowHead(points[^1], GetTransitionPathTangent(points, 1f, segmentControls), color, TransitionLineThickness);
        }
    }

    private void DrawSelectedTransitionPathEffect(IReadOnlyList<Vector2> points, IReadOnlyList<TransitionSegmentControls>? segmentControls)
    {
        DrawPathStroke(points, segmentControls, Theme.SelectedTransitionLineColor * 0.22f, 12f);
        DrawPathStroke(points, segmentControls, Theme.SelectedTransitionLabelColor * 0.34f, 7f);
        ReadOnlySpan<float> markerPositions = [0.22f, 0.5f, 0.78f];
        foreach (var t in markerPositions)
        {
            var center = GetTransitionPathPoint(points, t, segmentControls);
            _primitiveRenderer.DrawCircle(center, 5.5f, Theme.SelectedTransitionLabelColor * 0.72f);
            _primitiveRenderer.DrawCircleOutline(center, 7.5f, Theme.SelectedTransitionLineColor * 0.78f, 2f);
        }
    }

    private static Vector2 GetTransitionPathPoint(IReadOnlyList<Vector2> points, float t, IReadOnlyList<TransitionSegmentControls>? segmentControls = null)
    {
        if (points.Count == 0) return Vector2.Zero;
        if (points.Count == 1) return points[0];
        var totalLength = GetTransitionPathLength(points, segmentControls);
        if (totalLength <= 0f) return points[0];
        var targetLength = MathHelper.Clamp(t, 0f, 1f) * totalLength;
        var walked = 0f;
        for (var i = 0; i < points.Count - 1; i++)
        {
            GetTransitionPathSegmentControlPoints(points, i, segmentControls, out var control1, out var control2);
            var segmentLength = GetCubicBezierLength(points[i], control1, control2, points[i + 1]);
            if (walked + segmentLength >= targetLength)
            {
                var segmentT = segmentLength <= 0f ? 0f : (targetLength - walked) / segmentLength;
                return CubicBezier(points[i], control1, control2, points[i + 1], segmentT);
            }
            walked += segmentLength;
        }
        return points[^1];
    }

    private static Vector2 GetTransitionPathTangent(IReadOnlyList<Vector2> points, float t, IReadOnlyList<TransitionSegmentControls>? segmentControls = null)
    {
        if (points.Count < 2) return Vector2.UnitX;
        var totalLength = GetTransitionPathLength(points, segmentControls);
        if (totalLength <= 0f) return points[^1] - points[0];
        var targetLength = MathHelper.Clamp(t, 0f, 1f) * totalLength;
        var walked = 0f;
        for (var i = 0; i < points.Count - 1; i++)
        {
            GetTransitionPathSegmentControlPoints(points, i, segmentControls, out var control1, out var control2);
            var segmentLength = GetCubicBezierLength(points[i], control1, control2, points[i + 1]);
            if (walked + segmentLength >= targetLength)
            {
                var segmentT = segmentLength <= 0f ? 0f : (targetLength - walked) / segmentLength;
                return CubicBezierTangent(points[i], control1, control2, points[i + 1], segmentT);
            }
            walked += segmentLength;
        }
        GetTransitionPathSegmentControlPoints(points, points.Count - 2, segmentControls, out var lastControl1, out var lastControl2);
        return CubicBezierTangent(points[^2], lastControl1, lastControl2, points[^1], 1f);
    }

    private static float GetTransitionPathLength(IReadOnlyList<Vector2> points, IReadOnlyList<TransitionSegmentControls>? segmentControls = null)
    {
        var length = 0f;
        for (var i = 0; i < points.Count - 1; i++)
        {
            GetTransitionPathSegmentControlPoints(points, i, segmentControls, out var control1, out var control2);
            length += GetCubicBezierLength(points[i], control1, control2, points[i + 1]);
        }
        return length;
    }

    private static void GetTransitionPathSegmentControlPoints(IReadOnlyList<Vector2> points, int segmentIndex, IReadOnlyList<TransitionSegmentControls>? segmentControls, out Vector2 control1, out Vector2 control2)
    {
        var start = points[segmentIndex];
        var end = points[segmentIndex + 1];
        var previous = segmentIndex > 0 ? points[segmentIndex - 1] : start;
        var next = segmentIndex + 2 < points.Count ? points[segmentIndex + 2] : end;
        const float smoothness = 1f / 6f;
        var automaticControl1 = start + (end - previous) * smoothness;
        var automaticControl2 = end - (next - start) * smoothness;
        var customControls = segmentControls is not null && segmentIndex < segmentControls.Count
            ? segmentControls[segmentIndex]
            : null;
        control1 = customControls?.ControlPoint1 ?? automaticControl1;
        control2 = customControls?.ControlPoint2 ?? automaticControl2;
    }

    private static float GetCubicBezierLength(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end)
    {
        const int segments = 24;
        var length = 0f;
        var previous = start;
        for (var i = 1; i <= segments; i++)
        {
            var t = i / (float)segments;
            var current = CubicBezier(start, control1, control2, end, t);
            length += Vector2.Distance(previous, current);
            previous = current;
        }
        return length;
    }

    /// <summary>
    /// 矢印を描く。
    /// </summary>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <param name="color"></param>
    /// <param name="thickness"></param>
    private void DrawArrow(Vector2 start, Vector2 end, Color color, float thickness)
    {
        // 矢印の線を描く
        _primitiveRenderer.DrawLine(start, end, color, thickness);

        // 矢終を描く
        var direction = Vector2.Normalize(end - start);
        var arrowEndLocation = end;

        // 矢終を三角の角として描く。
        DrawArrowTipTriangle(arrowEndLocation, direction, color, thickness);
    }

    /// <summary>
    /// 矢印の頭を描く。
    /// </summary>
    /// <param name="tip"></param>
    /// <param name="tangent"></param>
    /// <param name="color"></param>
    /// <param name="thickness"></param>
    private void DrawArrowHead(Vector2 tip, Vector2 tangent, Color color, float thickness)
    {
        if (tangent.LengthSquared() <= 0.01f) return;

        // 向き。
        var direction = Vector2.Normalize(tangent);
        var arrowEndLocation = tip;

        // 矢終を三角の角として描く。
        DrawArrowTipTriangle(arrowEndLocation, direction, color, thickness);
    }

    /// <summary>
    /// 矢終を塗りつぶし三角の角として描く。
    /// </summary>
    /// <param name="tip"></param>
    /// <param name="direction"></param>
    /// <param name="color"></param>
    /// <param name="thickness"></param>
    private void DrawArrowTipTriangle(Vector2 tip, Vector2 direction, Color color, float thickness)
    {
        var length = MathF.Max(12f, thickness * 4.5f);
        var halfWidth = MathF.Max(6f, thickness * 2.4f);
        var normal = new Vector2(-direction.Y, direction.X);
        var baseCenter = tip - (direction * length);
        var leftBase = baseCenter + (normal * halfWidth);
        var rightBase = baseCenter - (normal * halfWidth);

        const int fillSteps = 10;
        for (var i = 0; i <= fillSteps; i++)
        {
            var t = i / (float)fillSteps;
            var left = Vector2.Lerp(tip, leftBase, t);
            var right = Vector2.Lerp(tip, rightBase, t);
            _primitiveRenderer.DrawLine(left, right, color, MathF.Max(1.5f, thickness));
        }
    }
    /// <summary>
    /// ベジェ曲線の点を計算
    /// </summary>
    /// <param name="start"></param>
    /// <param name="control1"></param>
    /// <param name="control2"></param>
    /// <param name="end"></param>
    /// <param name="t"></param>
    /// <returns></returns>
    private static Vector2 CubicBezier(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end, float t)
    {
        var u = 1f - t;
        return u * u * u * start
            + 3f * u * u * t * control1
            + 3f * u * t * t * control2
            + t * t * t * end;
    }

    /// <summary>
    /// ベジェ曲線の接線を計算
    /// </summary>
    /// <param name="start"></param>
    /// <param name="control1"></param>
    /// <param name="control2"></param>
    /// <param name="end"></param>
    /// <param name="t"></param>
    /// <returns></returns>
    private static Vector2 CubicBezierTangent(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end, float t)
    {
        var u = 1f - t;
        return 3f * u * u * (control1 - start)
            + 6f * u * t * (control2 - control1)
            + 3f * t * t * (end - control2);
    }
}
