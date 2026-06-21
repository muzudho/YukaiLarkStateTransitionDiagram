namespace YukaiLarkStateTransitionDiagram;

using System;
using System.Collections.Generic;
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
        string editingLabel)
    {
        var lineColor = selected ? Theme.SelectedTransitionLineColor : Theme.TransitionLineColor;
        var thickness = selected ? 4f : 3f;

        DrawBezierArrow(start, control1, control2, end, lineColor, thickness);
        DrawTransitionLabel(transition, start, control1, control2, end, selected, editing, editingLabel);
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
    public void DrawTransitionHoverCue(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end)
    {
        DrawBezierArrow(start, control1, control2, end, Theme.SelectedTransitionLineColor, 5f);
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
        string editingLabel)
    {
        var label = editing ? editingLabel + "_" : transition.Label;
        if (string.IsNullOrWhiteSpace(label) && !editing)
        {
            return;
        }

        var texture = _getLabelTexture(label, editing || selected);
        var center = GetTransitionLabelCenter(start, control1, control2, end, transition.LabelSide, texture);
        var position = center - new Vector2(texture.Width / 2f, texture.Height / 2f);
        var labelColor = selected ? Theme.SelectedTransitionLabelColor : Theme.TransitionLabelColor;
        _spriteBatch.Draw(texture, position, labelColor);
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
    private static Vector2 GetTransitionLabelCenter(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end, int labelSide, Texture2D labelTexture)
    {
        var midpoint = CubicBezier(start, control1, control2, end, 0.5f);
        var tangent = CubicBezierTangent(start, control1, control2, end, 0.5f);
        var side = labelSide == 0 ? -1f : 1f;
        if (MathF.Abs(tangent.X) >= MathF.Abs(tangent.Y))
        {
            return midpoint + new Vector2(0, side * (labelTexture.Height / 2f + 18f));
        }

        return midpoint + new Vector2(side * (labelTexture.Width / 2f + 22f), 0);
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

        // 矢印の頭の左右の羽を描く
        DrawSymmetricArrowHead(tip, direction, color, thickness);
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