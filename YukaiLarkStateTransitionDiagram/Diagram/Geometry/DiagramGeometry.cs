namespace YukaiLarkStateTransitionDiagram;

using System;
using System.Collections.Generic;
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

        var firstWaypoint = transition.Waypoints.Count > 0 ? transition.Waypoints[0] : target.Position;
        var lastWaypoint = transition.Waypoints.Count > 0 ? transition.Waypoints[^1] : source.Position;
        transition.SourceAngle ??= AngleFromTo(source.Position, firstWaypoint);
        transition.TargetAngle ??= AngleFromTo(target.Position, lastWaypoint);
    }

    private void UpdateTransitionHandle(DiagramTransition transition, TransitionHandleKind kind, Vector2 mousePosition, int waypointIndex = -1)
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
                transition.ControlPoint1 = GetConstrainedEndpointControlPoint(transition, isSourceControlPoint: true, mousePosition);
                break;
            case TransitionHandleKind.ControlPoint2:
                transition.ControlPoint2 = GetConstrainedEndpointControlPoint(transition, isSourceControlPoint: false, mousePosition);
                break;
            case TransitionHandleKind.Waypoint:
                if (waypointIndex >= 0 && waypointIndex < transition.Waypoints.Count)
                {
                    MoveTransitionWaypoint(transition, waypointIndex, mousePosition);
                }
                break;
            case TransitionHandleKind.SegmentControlPoint1:
                SetTransitionSegmentControlPoint(transition, waypointIndex, isFirstControlPoint: true, mousePosition);
                break;
            case TransitionHandleKind.SegmentControlPoint2:
                SetTransitionSegmentControlPoint(transition, waypointIndex, isFirstControlPoint: false, mousePosition);
                break;
        }
    }

    private void MoveTransitionWaypoint(DiagramTransition transition, int waypointIndex, Vector2 position)
    {
        var previousPosition = transition.Waypoints[waypointIndex];
        var delta = position - previousPosition;
        if (delta.LengthSquared() <= 0.0001f)
        {
            return;
        }

        transition.Waypoints[waypointIndex] = position;
        MoveTransitionWaypointControlPoints(transition, waypointIndex, delta);
        UpdateTransitionWaypointEndpointAngles(transition);
    }

    private static void MoveTransitionWaypointControlPoints(DiagramTransition transition, int waypointIndex, Vector2 delta)
    {
        var previousSegmentIndex = waypointIndex;
        if (previousSegmentIndex >= 0 && previousSegmentIndex < transition.SegmentControls.Count)
        {
            var controls = transition.SegmentControls[previousSegmentIndex];
            if (controls.ControlPoint2.HasValue)
            {
                controls.ControlPoint2 += delta;
            }
        }

        var nextSegmentIndex = waypointIndex + 1;
        if (nextSegmentIndex >= 0 && nextSegmentIndex < transition.SegmentControls.Count)
        {
            var controls = transition.SegmentControls[nextSegmentIndex];
            if (controls.ControlPoint1.HasValue)
            {
                controls.ControlPoint1 += delta;
            }
        }
    }

    private Vector2 GetConstrainedEndpointControlPoint(DiagramTransition transition, bool isSourceControlPoint, Vector2 mousePosition)
    {
        if (!TryGetTransitionEndpoints(transition, out var start, out var end))
        {
            return mousePosition;
        }

        var anchor = isSourceControlPoint ? start : end;
        var angle = isSourceControlPoint
            ? transition.SourceAngle ?? AngleFromTo(start, end)
            : transition.TargetAngle ?? AngleFromTo(end, start);
        var direction = UnitVectorFromAngle(angle);
        var length = MathF.Max(Vector2.Distance(anchor, mousePosition), DiagramNode.RadiusUnit * 0.5f);
        return anchor + direction * length;
    }

    private void SetTransitionSegmentControlPoint(DiagramTransition transition, int segmentIndex, bool isFirstControlPoint, Vector2 position)
    {
        if (segmentIndex < 0 || !TryGetTransitionPath(transition, out var points) || segmentIndex >= points.Count - 1)
        {
            return;
        }

        EnsureTransitionSegmentControls(transition, segmentIndex + 1);
        if (isFirstControlPoint)
        {
            var constrainedPosition = segmentIndex == 0
                ? GetConstrainedPathEndpointControlPoint(transition, points, isSourceControlPoint: true, position)
                : position;
            transition.SegmentControls[segmentIndex].ControlPoint1 = constrainedPosition;
            AlignPreviousSegmentControlPointAngle(transition, points, segmentIndex, constrainedPosition);
        }
        else
        {
            var constrainedPosition = segmentIndex == points.Count - 2
                ? GetConstrainedPathEndpointControlPoint(transition, points, isSourceControlPoint: false, position)
                : position;
            transition.SegmentControls[segmentIndex].ControlPoint2 = constrainedPosition;
            AlignNextSegmentControlPointAngle(transition, points, segmentIndex, constrainedPosition);
        }
    }


    private static Vector2 GetConstrainedPathEndpointControlPoint(DiagramTransition transition, IReadOnlyList<Vector2> points, bool isSourceControlPoint, Vector2 mousePosition)
    {
        var anchor = isSourceControlPoint ? points[0] : points[^1];
        var angle = isSourceControlPoint
            ? transition.SourceAngle ?? AngleFromTo(points[0], points[1])
            : transition.TargetAngle ?? AngleFromTo(points[^1], points[^2]);
        var direction = UnitVectorFromAngle(angle);
        var length = MathF.Max(Vector2.Distance(anchor, mousePosition), DiagramNode.RadiusUnit * 0.5f);
        return anchor + direction * length;
    }

    private static void AlignPreviousSegmentControlPointAngle(DiagramTransition transition, IReadOnlyList<Vector2> points, int segmentIndex, Vector2 movedControlPoint)
    {
        if (segmentIndex <= 0)
        {
            return;
        }

        var joint = points[segmentIndex];
        var direction = movedControlPoint - joint;
        if (direction.LengthSquared() <= 0.01f)
        {
            return;
        }

        var oppositeSegmentIndex = segmentIndex - 1;
        EnsureTransitionSegmentControls(transition, oppositeSegmentIndex + 1);
        GetTransitionPathSegmentControlPoints(points, oppositeSegmentIndex, transition.SegmentControls, out _, out var currentOppositeControlPoint);
        var length = Vector2.Distance(joint, currentOppositeControlPoint);
        transition.SegmentControls[oppositeSegmentIndex].ControlPoint2 = joint - Vector2.Normalize(direction) * length;
    }

    private static void AlignNextSegmentControlPointAngle(DiagramTransition transition, IReadOnlyList<Vector2> points, int segmentIndex, Vector2 movedControlPoint)
    {
        if (segmentIndex + 1 >= points.Count - 1)
        {
            return;
        }

        var joint = points[segmentIndex + 1];
        var direction = movedControlPoint - joint;
        if (direction.LengthSquared() <= 0.01f)
        {
            return;
        }

        var oppositeSegmentIndex = segmentIndex + 1;
        EnsureTransitionSegmentControls(transition, oppositeSegmentIndex + 1);
        GetTransitionPathSegmentControlPoints(points, oppositeSegmentIndex, transition.SegmentControls, out var currentOppositeControlPoint, out _);
        var length = Vector2.Distance(joint, currentOppositeControlPoint);
        transition.SegmentControls[oppositeSegmentIndex].ControlPoint1 = joint - Vector2.Normalize(direction) * length;
    }

    private static void EnsureTransitionSegmentControls(DiagramTransition transition, int count)
    {
        while (transition.SegmentControls.Count < count)
        {
            transition.SegmentControls.Add(new TransitionSegmentControls());
        }
    }

    private void UpdateTransitionWaypointEndpointAngles(DiagramTransition transition)
    {
        if (transition.Waypoints.Count == 0)
        {
            return;
        }

        var source = FindNode(transition.SourceId);
        var target = FindNode(transition.TargetId);
        if (source is null || target is null)
        {
            return;
        }

        transition.SourceAngle = AngleFromTo(source.Position, transition.Waypoints[0]);
        transition.TargetAngle = AngleFromTo(target.Position, transition.Waypoints[^1]);
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

        var firstWaypoint = transition.Waypoints.Count > 0 ? transition.Waypoints[0] : target.Position;
        var lastWaypoint = transition.Waypoints.Count > 0 ? transition.Waypoints[^1] : source.Position;
        var sourceAngle = transition.SourceAngle ?? AngleFromTo(source.Position, firstWaypoint);
        var targetAngle = transition.TargetAngle ?? AngleFromTo(target.Position, lastWaypoint);

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
        var handleLength = MathF.Max(delta.Length() / 3f, DiagramNode.RadiusUnit);
        var sourceDirection = UnitVectorFromAngle(transition.SourceAngle ?? AngleFromTo(start, end));
        var targetDirection = UnitVectorFromAngle(transition.TargetAngle ?? AngleFromTo(end, start));
        control1 = transition.ControlPoint1 ?? start + sourceDirection * handleLength;
        control2 = transition.ControlPoint2 ?? end + targetDirection * handleLength;
        return true;
    }


    private bool TryGetTransitionPath(DiagramTransition transition, out List<Vector2> points)
    {
        points = new List<Vector2>();
        if (!TryGetTransitionEndpoints(transition, out var start, out var end))
        {
            return false;
        }

        points.Add(start);
        points.AddRange(transition.Waypoints);
        points.Add(end);
        ConstrainPathEndpointControlAngles(transition, points);
        return true;
    }


    private static void ConstrainPathEndpointControlAngles(DiagramTransition transition, IReadOnlyList<Vector2> points)
    {
        if (points.Count < 2)
        {
            return;
        }

        EnsureTransitionSegmentControls(transition, points.Count - 1);
        var sourceDirection = UnitVectorFromAngle(transition.SourceAngle ?? AngleFromTo(points[0], points[1]));
        GetTransitionPathSegmentControlPoints(points, 0, transition.SegmentControls, out var firstControlPoint, out _);
        var sourceHandleLength = MathF.Max(Vector2.Distance(points[0], firstControlPoint), DiagramNode.RadiusUnit * 0.5f);
        transition.SegmentControls[0].ControlPoint1 = points[0] + sourceDirection * sourceHandleLength;

        var lastSegmentIndex = points.Count - 2;
        var targetDirection = UnitVectorFromAngle(transition.TargetAngle ?? AngleFromTo(points[^1], points[^2]));
        GetTransitionPathSegmentControlPoints(points, lastSegmentIndex, transition.SegmentControls, out _, out var lastControlPoint);
        var targetHandleLength = MathF.Max(Vector2.Distance(points[^1], lastControlPoint), DiagramNode.RadiusUnit * 0.5f);
        transition.SegmentControls[lastSegmentIndex].ControlPoint2 = points[^1] + targetDirection * targetHandleLength;
    }

    private static Vector2 GetTransitionPathPoint(IReadOnlyList<Vector2> points, float t, IReadOnlyList<TransitionSegmentControls>? segmentControls = null)
    {
        if (points.Count == 0)
        {
            return Vector2.Zero;
        }
        if (points.Count == 1)
        {
            return points[0];
        }

        var totalLength = GetTransitionPathLength(points, segmentControls);
        if (totalLength <= 0f)
        {
            return points[0];
        }

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
        if (points.Count < 2)
        {
            return Vector2.UnitX;
        }

        var totalLength = GetTransitionPathLength(points, segmentControls);
        if (totalLength <= 0f)
        {
            return points[^1] - points[0];
        }

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

    private static float FindNearestTransitionPathT(Vector2 point, IReadOnlyList<Vector2> points, IReadOnlyList<TransitionSegmentControls>? segmentControls = null)
    {
        var totalLength = GetTransitionPathLength(points, segmentControls);
        if (totalLength <= 0f)
        {
            return 0f;
        }

        const int samplesPerSegment = 24;
        var walked = 0f;
        var bestT = 0f;
        var bestDistance = float.MaxValue;
        for (var i = 0; i < points.Count - 1; i++)
        {
            GetTransitionPathSegmentControlPoints(points, i, segmentControls, out var control1, out var control2);
            var previous = points[i];
            var segmentWalked = 0f;
            for (var sample = 1; sample <= samplesPerSegment; sample++)
            {
                var localT = sample / (float)samplesPerSegment;
                var current = CubicBezier(points[i], control1, control2, points[i + 1], localT);
                var segmentLength = Vector2.Distance(previous, current);
                var distance = DistanceToSegment(point, previous, current);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestT = (walked + segmentWalked + (segmentLength * 0.5f)) / totalLength;
                }
                segmentWalked += segmentLength;
                previous = current;
            }
            walked += segmentWalked;
        }

        return MathHelper.Clamp(bestT, 0f, 1f);
    }

    private static float DistanceToTransitionPath(Vector2 point, IReadOnlyList<Vector2> points, IReadOnlyList<TransitionSegmentControls>? segmentControls = null)
    {
        var best = float.MaxValue;
        for (var i = 0; i < points.Count - 1; i++)
        {
            GetTransitionPathSegmentControlPoints(points, i, segmentControls, out var control1, out var control2);
            best = MathF.Min(best, DistanceToBezier(point, points[i], control1, control2, points[i + 1]));
        }
        return best;
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

    private static Vector2 UnitVectorFromAngle(float angle)
        => new(MathF.Cos(angle), MathF.Sin(angle));

    private static Vector2 PointOnCircle(Vector2 center, float radius, float angle)
        => center + UnitVectorFromAngle(angle) * radius;

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