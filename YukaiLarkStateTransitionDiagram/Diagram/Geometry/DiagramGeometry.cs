namespace YukaiLarkStateTransitionDiagram;

using System;
using Microsoft.Xna.Framework;

public partial class Game1
{
    private void InitializeTransitionEndpoints(DiagramTransition transition)
    {
        var source = FindNode(transition.SourceId);
        var target = FindNode(transition.TargetId);
        if (source is null || target is null)
        {
            return;
        }

        if (transition.SourceId == transition.TargetId)
        {
            transition.SourceAngle ??= -MathHelper.PiOver4;
            transition.TargetAngle ??= MathHelper.PiOver4;
            return;
        }

        transition.SourceAngle ??= AngleFromTo(source.Position, target.Position);
        transition.TargetAngle ??= AngleFromTo(target.Position, source.Position);
    }

    private void UpdateTransitionHandle(DiagramTransition transition, TransitionHandleKind kind, Vector2 mousePosition)
    {
        switch (kind)
        {
            case TransitionHandleKind.SourceEndpoint:
                UpdateTransitionEndpoint(transition, true, mousePosition);
                break;
            case TransitionHandleKind.TargetEndpoint:
                UpdateTransitionEndpoint(transition, false, mousePosition);
                break;
            case TransitionHandleKind.ControlPoint1:
                transition.ControlPoint1 = mousePosition;
                break;
            case TransitionHandleKind.ControlPoint2:
                transition.ControlPoint2 = mousePosition;
                break;
        }
    }

    private void UpdateTransitionEndpoint(DiagramTransition transition, bool isSource, Vector2 mousePosition)
    {
        var node = FindNode(isSource ? transition.SourceId : transition.TargetId);
        if (node is null)
        {
            return;
        }

        var angle = AngleFromTo(node.Position, mousePosition);
        if (isSource)
        {
            transition.SourceAngle = angle;
        }
        else
        {
            transition.TargetAngle = angle;
        }
    }

    /// <summary>
    /// ［遷移］エッジの尻
    /// </summary>
    /// <param name="transition"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    private bool TryGetTransitionEndpoints(DiagramTransition transition, out Vector2 start, out Vector2 end)
    {
        var source = FindNode(transition.SourceId);
        var target = FindNode(transition.TargetId);
        if (source is null || target is null)
        {
            start = Vector2.Zero;
            end = Vector2.Zero;
            return false;
        }

        var sourceAngle = transition.SourceAngle ?? AngleFromTo(source.Position, target.Position);
        var targetAngle = transition.TargetAngle ?? AngleFromTo(target.Position, source.Position);

        // 頭
        start = PointOnCircle(source.Position, source.Radius, sourceAngle);

        // 尻
        end = PointOnCircle(target.Position, target.Radius + TransitionHeadPadding, targetAngle);
        return true;
    }

    private const float TransitionHeadPadding = 12f;

    private bool TryGetTransitionGeometry(DiagramTransition transition, out Vector2 start, out Vector2 control1, out Vector2 control2, out Vector2 end)
    {
        if (!TryGetTransitionEndpoints(transition, out start, out end))
        {
            control1 = Vector2.Zero;
            control2 = Vector2.Zero;
            return false;
        }

        if (transition.SourceId == transition.TargetId)
        {
            var node = FindNode(transition.SourceId);
            if (node is null)
            {
                control1 = Vector2.Zero;
                control2 = Vector2.Zero;
                return false;
            }

            control1 = transition.ControlPoint1 ?? node.Position + new Vector2(node.Radius * 2.5f, -node.Radius * 2.2f);
            control2 = transition.ControlPoint2 ?? node.Position + new Vector2(node.Radius * 2.5f, node.Radius * 2.2f);
            return true;
        }

        var delta = end - start;
        control1 = transition.ControlPoint1 ?? start + delta / 3f;
        control2 = transition.ControlPoint2 ?? start + delta * 2f / 3f;
        return true;
    }

    private static Vector2 PointOnCircle(Vector2 center, float radius, float angle)
        => center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;

    private static float AngleFromTo(Vector2 from, Vector2 to)
        => MathF.Atan2(to.Y - from.Y, to.X - from.X);

    private static Vector2 Rotate(Vector2 vector, float angle)
    {
        var cos = MathF.Cos(angle);
        var sin = MathF.Sin(angle);
        return new Vector2(vector.X * cos - vector.Y * sin, vector.X * sin + vector.Y * cos);
    }

    private static float FindNearestTransitionT(Vector2 point, Vector2 start, Vector2 control1, Vector2 control2, Vector2 end)
    {
        const int samples = 64;
        var nearestT = 0f;
        var nearestDistance = float.MaxValue;
        for (var i = 0; i <= samples; i++)
        {
            var t = i / (float)samples;
            var candidate = EdgeRenderer.GetTransitionPoint(start, control1, control2, end, t);
            var distance = Vector2.DistanceSquared(point, candidate);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestT = t;
            }
        }

        return nearestT;
    }

    private static Vector2 CubicBezier(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end, float t)
    {
        var u = 1f - t;
        return u * u * u * start
            + 3f * u * u * t * control1
            + 3f * u * t * t * control2
            + t * t * t * end;
    }

    private static Vector2 CubicBezierTangent(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end, float t)
    {
        var u = 1f - t;
        return 3f * u * u * (control1 - start)
            + 6f * u * t * (control2 - control1)
            + 3f * t * t * (end - control2);
    }
    private static float DistanceToBezier(Vector2 point, Vector2 start, Vector2 control1, Vector2 control2, Vector2 end)
    {
        const int segments = 32;
        var best = float.MaxValue;
        var previous = start;
        for (var i = 1; i <= segments; i++)
        {
            var t = i / (float)segments;
            var current = CubicBezier(start, control1, control2, end, t);
            best = MathF.Min(best, DistanceToSegment(point, previous, current));
            previous = current;
        }

        return best;
    }
    private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var lengthSquared = ab.LengthSquared();
        if (lengthSquared == 0)
        {
            return Vector2.Distance(point, a);
        }
        var t = MathHelper.Clamp(Vector2.Dot(point - a, ab) / lengthSquared, 0f, 1f);
        var projection = a + t * ab;
        return Vector2.Distance(point, projection);
    }
}