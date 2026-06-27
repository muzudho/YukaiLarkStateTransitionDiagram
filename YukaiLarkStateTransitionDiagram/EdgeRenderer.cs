namespace YukaiLarkStateTransitionDiagram;

using System;
using YukaiLarkStateTransitionDiagram.Theme;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public sealed class EdgeRenderer
{
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
        var thickness = selected ? 4f : 3f;

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
        _primitiveRenderer.DrawHandle(start, Theme.TransitionHandleColor);
        _primitiveRenderer.DrawHandle(end, Theme.TransitionHandleColor);
        _primitiveRenderer.DrawHandle(control1, Theme.TransitionControlHandleColor);
        _primitiveRenderer.DrawHandle(control2, Theme.TransitionControlHandleColor);
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
    public void DrawTransitionHandleCue(Vector2 center)
    {
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
        _spriteBatch.Draw(texture, position, Theme.TransitionLabelColor);
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
            ? Theme.SelectedTransitionLabelColor * (alpha * 0.62f)
            : Theme.SelectedTransitionLabelColor * alpha;
        _spriteBatch.Draw(texture, position, labelColor);

        if (showCaret && alpha > 0f)
        {
            var caretLabel = isEmpty ? displayLabel : editingLabel;
            var caretIndex = isEmpty ? 0 : Math.Clamp(editingCaretIndex, 0, caretLabel.Length);
            DrawEditingCaret(position, texture, caretLabel, caretIndex, Theme.SelectedTransitionLabelColor * alpha);
        }
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

        DrawArrowHead(end, CubicBezierTangent(start, control1, control2, end, 1f), color, selected ? 4f : 3f);
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

        // 矢印の頭の左右の羽を描く
        DrawSymmetricArrowHead(arrowEndLocation, direction, color, thickness);
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

        // 矢印の頭の左右の羽を描く
        DrawSymmetricArrowHead(arrowEndLocation, direction, color, thickness);
    }

    /// <summary>
    /// 左右対称な矢印の羽を描く。
    /// </summary>
    /// <param name="tip"></param>
    /// <param name="direction"></param>
    /// <param name="color"></param>
    /// <param name="thickness"></param>
    private void DrawSymmetricArrowHead(Vector2 tip, Vector2 direction, Color color, float thickness)
    {
        var wingLength = 18f;
        var wingAngle = MathHelper.ToRadians(28f);
        var leftWing = Rotate(direction, wingAngle) * wingLength;
        var rightWing = Rotate(direction, -wingAngle) * wingLength;

        // 左右の羽を描く
        _primitiveRenderer.DrawLine(tip, tip - leftWing, color, thickness);
        _primitiveRenderer.DrawLine(tip, tip - rightWing, color, thickness);
    }

    /// <summary>
    /// ベクトルを回転する。
    /// </summary>
    /// <param name="vector"></param>
    /// <param name="radians"></param>
    /// <returns></returns>
    private static Vector2 Rotate(Vector2 vector, float radians)
    {
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        return new Vector2(
            vector.X * cos - vector.Y * sin,
            vector.X * sin + vector.Y * cos);
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
