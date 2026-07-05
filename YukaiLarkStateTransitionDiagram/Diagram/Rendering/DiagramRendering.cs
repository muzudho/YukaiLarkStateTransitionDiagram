namespace YukaiLarkStateTransitionDiagram;

using System;
using System.Collections.Generic;
using System.Linq;
using YukaiLarkStateTransitionDiagram.Assistants;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

public partial class Game1
{
    private Matrix GetViewMatrix()
        => Matrix.CreateScale(_cameraZoom) * Matrix.CreateTranslation(_cameraOffset.X, _cameraOffset.Y, 0f);
    private Vector2 ScreenToWorld(Vector2 screenPosition)
        => (screenPosition - _cameraOffset) / _cameraZoom;
    private Vector2 WorldToScreen(Vector2 worldPosition)
        => worldPosition * _cameraZoom + _cameraOffset;
    private static Vector2 SnapToHalfGrid(Vector2 position)
    {
        const float unit = DiagramNode.RadiusUnit;
        return new Vector2(
            MathF.Round(position.X / unit) * unit,
            MathF.Round(position.Y / unit) * unit);
    }
    private void DrawGrid(int spacing, Color color)
    {
        var topLeft = ScreenToWorld(Vector2.Zero);
        var bottomRight = ScreenToWorld(new Vector2(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height));
        DrawGrid(spacing, color, topLeft, bottomRight);
    }

    private void DrawGrid(int spacing, Color color, Vector2 topLeft, Vector2 bottomRight)
    {
        var startX = (int)MathF.Floor(topLeft.X / spacing) * spacing;
        var endX = (int)MathF.Ceiling(bottomRight.X / spacing) * spacing;
        var startY = (int)MathF.Floor(topLeft.Y / spacing) * spacing;
        var endY = (int)MathF.Ceiling(bottomRight.Y / spacing) * spacing;
        for (var x = startX; x <= endX; x += spacing)
        {
            _primitiveRenderer.DrawLine(new Vector2(x, topLeft.Y), new Vector2(x, bottomRight.Y), color, 1f);
        }
        for (var y = startY; y <= endY; y += spacing)
        {
            _primitiveRenderer.DrawLine(new Vector2(topLeft.X, y), new Vector2(bottomRight.X, y), color, 1f);
        }
    }

    private void DrawDraggedNodeSnapGridLines(DiagramNode node, TimeSpan totalGameTime)
    {
        var topLeft = ScreenToWorld(Vector2.Zero);
        var bottomRight = ScreenToWorld(new Vector2(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height));
        var pulse = 0.5f + (MathF.Sin((float)totalGameTime.TotalSeconds * 7.2f) * 0.5f);
        var color = _nodeRenderer.GetNodeSnapIndicatorColor(node) * MathHelper.Lerp(0.48f, 0.72f, pulse);
        var thickness = MathHelper.Lerp(2.2f, 3f, pulse);

        _primitiveRenderer.DrawLine(new Vector2(node.Position.X, topLeft.Y), new Vector2(node.Position.X, bottomRight.Y), color, thickness);
        _primitiveRenderer.DrawLine(new Vector2(topLeft.X, node.Position.Y), new Vector2(bottomRight.X, node.Position.Y), color, thickness);
    }

    private DiagramNode? GetNodeHoverCueTarget()
    {
        if (IsEditingLabel || _isEditingDiagramTabName || _isEditingFileName || _draggedNode is not null || _resizedNode is not null || _draggedHandleTransition is not null || _isPanning || _linkSource is not null)
        {
            return null;
        }

        var mouse = Mouse.GetState();
        var mouseWorld = ScreenToWorld(mouse.Position.ToVector2());
        var handle = FindTransitionHandleAt(mouseWorld);
        if (handle.Transition is not null)
        {
            return null;
        }

        var node = FindNodeAt(mouseWorld);
        return node is not null && node != _selectedNode
            ? node
            : null;
    }

    private void DrawHoverCue(TimeSpan totalGameTime)
    {
        if (IsEditingLabel || _isEditingDiagramTabName || _isEditingFileName || _draggedNode is not null || _resizedNode is not null || _draggedHandleTransition is not null || _isPanning || _linkSource is not null)
        {
            return;
        }

        var mouse = Mouse.GetState();
        var mouseWorld = ScreenToWorld(mouse.Position.ToVector2());
        var handle = FindTransitionHandleAt(mouseWorld);
        if (handle.Transition is not null && TryGetTransitionGeometry(handle.Transition, out var start, out var control1, out var control2, out var end))
        {
            var center = handle.Kind switch
            {
                TransitionHandleKind.SourceEndpoint => start,
                TransitionHandleKind.TargetEndpoint => end,
                TransitionHandleKind.ControlPoint1 => control1,
                TransitionHandleKind.ControlPoint2 => control2,
                TransitionHandleKind.Waypoint when handle.WaypointIndex >= 0 && handle.WaypointIndex < handle.Transition.Waypoints.Count => handle.Transition.Waypoints[handle.WaypointIndex],
                TransitionHandleKind.SegmentControlPoint1 when handle.WaypointIndex >= 0 && TryGetTransitionPath(handle.Transition, out var handlePoints1) => GetTransitionSegmentControlPoint(handlePoints1, handle.Transition.SegmentControls, handle.WaypointIndex, true),
                TransitionHandleKind.SegmentControlPoint2 when handle.WaypointIndex >= 0 && TryGetTransitionPath(handle.Transition, out var handlePoints2) => GetTransitionSegmentControlPoint(handlePoints2, handle.Transition.SegmentControls, handle.WaypointIndex, false),
                _ => Vector2.Zero
            };
            if (center != Vector2.Zero)
            {
                var controlHandle = handle.Kind is TransitionHandleKind.ControlPoint1
                    or TransitionHandleKind.ControlPoint2
                    or TransitionHandleKind.SegmentControlPoint1
                    or TransitionHandleKind.SegmentControlPoint2;
                _edgeRenderer.DrawTransitionHandleCue(center, controlHandle);
            }
            return;
        }

        var node = FindNodeAt(mouseWorld);
        if (node is not null)
        {
            return;
        }

        var transition = FindTransitionAt(mouseWorld);
        if (transition is not null && transition != _selectedTransition)
        {
            if (transition.Waypoints.Count > 0 && TryGetTransitionPath(transition, out var points))
            {
                _edgeRenderer.DrawTransitionPathHoverCue(transition, points, totalGameTime);
            }
            else if (TryGetTransitionGeometry(transition, out start, out control1, out control2, out end))
            {
                _edgeRenderer.DrawTransitionHoverCue(start, control1, control2, end, totalGameTime);
            }
        }
    }


    private static Vector2 GetTransitionSegmentControlPoint(IReadOnlyList<Vector2> points, IReadOnlyList<TransitionSegmentControls> segmentControls, int segmentIndex, bool firstControlPoint)
    {
        GetTransitionPathSegmentControlPoints(points, segmentIndex, segmentControls, out var control1, out var control2);
        return firstControlPoint ? control1 : control2;
    }

    private void DrawDiagramScene(Matrix transformMatrix, bool includeInteraction, TimeSpan totalGameTime)
    {
        _spriteBatch.Begin(samplerState: SamplerState.LinearClamp, transformMatrix: transformMatrix);
        DrawGrid(40, _boardTheme.GridColor);
        if (includeInteraction && _draggedNode is not null && !IsAltDown(Keyboard.GetState()))
        {
            DrawDraggedNodeSnapGridLines(_draggedNode, totalGameTime);
        }
        DrawDiagramContent(includeInteraction, totalGameTime);
        _spriteBatch.End();
    }

    private void DrawDiagramContent(bool includeInteraction, TimeSpan totalGameTime)
    {
        var editingDisplayLabel = GetEditingDisplayLabel();
        var editingDisplayCaretIndex = GetEditingDisplayCaretIndex();
        var showEditingCaret = _textBoxController.IsCaretNavigationKeyHeld || ((int)(totalGameTime.TotalSeconds * 2)) % 2 == 0;
        if (includeInteraction)
        {
            DrawTransitionGhost(totalGameTime);
        }

        foreach (var transition in _transitions)
        {
            var displayTransition = CanTransitionHaveEvent(transition)
                ? transition
                : new DiagramTransition
                {
                    SourceId = transition.SourceId,
                    TargetId = transition.TargetId,
                    LabelSide = transition.LabelSide,
                    LabelAnchorT = transition.LabelAnchorT,
                    LabelOffset = transition.LabelOffset,
                    ControlPoint1 = transition.ControlPoint1,
                    ControlPoint2 = transition.ControlPoint2,
                    Waypoints = transition.Waypoints.ToList(),
                    SegmentControls = transition.SegmentControls.Select(CloneTransitionSegmentControls).ToList()
                };
            if (transition.Waypoints.Count > 0 && TryGetTransitionPath(transition, out var points))
            {
                _edgeRenderer.DrawTransitionPath(
                    displayTransition,
                    points,
                    includeInteraction && transition == _selectedTransition,
                    transition == _editingTransition,
                    editingDisplayLabel,
                    editingDisplayCaretIndex,
                    showEditingCaret,
                    !CanTransitionHaveEvent(transition));
            }
            else if (TryGetTransitionGeometry(transition, out var start, out var control1, out var control2, out var end))
            {
                _edgeRenderer.DrawTransition(
                    displayTransition,
                    start,
                    control1,
                    control2,
                    end,
                    includeInteraction && transition == _selectedTransition,
                    transition == _editingTransition,
                    editingDisplayLabel,
                    editingDisplayCaretIndex,
                    showEditingCaret,
                    !CanTransitionHaveEvent(transition));
            }
        }
        if (includeInteraction)
        {
            DrawTransitionEventGhost(totalGameTime);
        }
        if (includeInteraction)
        {
            DrawHoverCue(totalGameTime);
            if (_linkSource is not null)
            {
                var mouse = Mouse.GetState();
                var mouseWorld = ScreenToWorld(mouse.Position.ToVector2());
                var previewPoints = new List<Vector2> { _linkSource.Position };
                previewPoints.AddRange(_linkWaypoints);
                previewPoints.Add(mouseWorld);
                if (previewPoints.Count > 2)
                {
                    _edgeRenderer.DrawTransitionPathGhost(previewPoints, 0.9f);
                }
                else
                {
                    _edgeRenderer.DrawLinkPreview(_linkSource.Position, mouseWorld);
                }
            }
        }
        if (includeInteraction)
        {
            DrawStartMarkerGhost(totalGameTime);
            DrawStateNodeGhost(totalGameTime);
        }
        var hoveredNode = includeInteraction ? GetNodeHoverCueTarget() : null;
        foreach (var node in _nodes)
        {
            var inactive = includeInteraction && IsInactiveDuringTransitionLink(node);
            _nodeRenderer.DrawNode(node, includeInteraction && node == _selectedNode, _editingNode, editingDisplayLabel, editingDisplayCaretIndex, showEditingCaret, totalGameTime, inactive, node == hoveredNode);
        }
        if (includeInteraction)
        {
            DrawStateNodeLabelEditGhost(totalGameTime);
        }
        if (includeInteraction && _draggedNode is not null && !IsAltDown(Keyboard.GetState()))
        {
            _nodeRenderer.DrawNodeSnapIndicator(_draggedNode, totalGameTime);
        }
        if (includeInteraction && _selectedNode is not null && !IsInactiveDuringTransitionLink(_selectedNode))
        {
            _nodeRenderer.DrawNodeResizeHandle(_selectedNode);
        }
        if (includeInteraction && _selectedTransition is not null)
        {
            if (_selectedTransition.Waypoints.Count > 0 && TryGetTransitionPath(_selectedTransition, out var points))
            {
                _edgeRenderer.DrawTransitionPathHandles(_selectedTransition, points);
                if (_draggedHandleTransition == _selectedTransition)
                {
                    _edgeRenderer.DrawTransitionPathRelatedHandleCue(points, _selectedTransition.SegmentControls, _draggedHandleKind, _draggedWaypointIndex);
                }
            }
            else if (TryGetTransitionGeometry(_selectedTransition, out var start, out var control1, out var control2, out var end))
            {
                if (_selectedTransition.SourceId == _selectedTransition.TargetId)
                {
                    _edgeRenderer.DrawTransitionHandles(start, control1, control2, end);
                }
                else
                {
                    _edgeRenderer.DrawTransitionEndpointHandles(start, end);
                }
            }
        }
    }

    private void DrawStartMarkerGhost(TimeSpan totalGameTime)
    {
        var context = CreateAssistantContext();
        if (!_yukaiLarkAssistant.ShouldDrawStartMarkerGhost(context))
        {
            return;
        }

        var screenPosition = _yukaiLarkAssistant.GetNodeScreenPosition(GraphicsDevice.Viewport, YukaiLarkAssistKind.CreateStartMarker);
        var worldPosition = SnapToHalfGrid(ScreenToWorld(screenPosition));
        var bob = YukaiLarkAssistant.GetAssistBobOffset(totalGameTime);
        var ghostNode = new DiagramNode
        {
            Label = "開始",
            Position = worldPosition + new Vector2(0f, bob),
            RadiusUnits = DiagramNode.TerminalRadiusUnits,
            ColorIndex = 0,
            Kind = NodeKind.StartMarker
        };
        _nodeRenderer.DrawStartMarkerGhost(ghostNode, 1f);
    }

    private void DrawStateNodeGhost(TimeSpan totalGameTime)
    {
        var context = CreateAssistantContext();
        if (!_yukaiLarkAssistant.ShouldDrawStateNodeGhost(context))
        {
            return;
        }

        var nodeId = _nextNodeId;
        var assistKind = context.NormalNodeCount == 1 && context.HasStartToNormalTransition
            ? YukaiLarkAssistKind.CreateSecondStateNode
            : YukaiLarkAssistKind.CreateStateNode;
        var screenPosition = _yukaiLarkAssistant.GetNodeScreenPosition(GraphicsDevice.Viewport, assistKind);
        var worldPosition = SnapToHalfGrid(ScreenToWorld(screenPosition));
        var bob = YukaiLarkAssistant.GetAssistBobOffset(totalGameTime);
        var ghostNode = new DiagramNode
        {
            Label = $"状態{nodeId}",
            Position = worldPosition + new Vector2(0f, bob),
            RadiusUnits = DiagramNode.DefaultRadiusUnits,
            ColorIndex = (nodeId - 1) % _boardTheme.NormalNodePalette.Length,
            Kind = NodeKind.Normal
        };
        _nodeRenderer.DrawStateNodeGhost(ghostNode, 1f);
    }

    private void DrawStateNodeLabelEditGhost(TimeSpan totalGameTime)
    {
        var context = CreateAssistantContext();
        if (!_yukaiLarkAssistant.ShouldDrawStateNodeLabelEditGhost(context))
        {
            return;
        }

        var node = _nodes
            .Where(node => IsDefaultLabeledNormalNode(node) && !_skippedStateNodeLabelEditAssistNodeIds.Contains(node.Id))
            .OrderBy(n => n.Id)
            .FirstOrDefault();
        if (node is null)
        {
            return;
        }

        _nodeRenderer.DrawStateNodeLabelEditGhost(node, totalGameTime);
    }

    private void DrawTransitionGhost(TimeSpan totalGameTime)
    {
        var context = CreateAssistantContext();
        if (!_yukaiLarkAssistant.ShouldDrawTransitionGhost(context))
        {
            return;
        }

        if (!TryGetAssistantTransitionEndpoints(out var source, out var target))
        {
            return;
        }

        var transition = new DiagramTransition { SourceId = source.Id, TargetId = target.Id };
        InitializeTransitionEndpoints(transition);
        if (!TryGetTransitionGeometry(transition, out var start, out var control1, out var control2, out var end))
        {
            return;
        }

        var offset = new Vector2(0f, YukaiLarkAssistant.GetAssistBobOffset(totalGameTime));
        _edgeRenderer.DrawTransitionGhost(start + offset, control1 + offset, control2 + offset, end + offset, 1f);
    }

    private void DrawTransitionEventGhost(TimeSpan totalGameTime)
    {
        var context = CreateAssistantContext();
        if (!_yukaiLarkAssistant.ShouldDrawTransitionEventGhost(context))
        {
            return;
        }

        var transition = _transitions.FirstOrDefault(t => CanTransitionHaveEvent(t) && string.IsNullOrWhiteSpace(t.Label));
        if (transition is null || !TryGetTransitionGeometry(transition, out var start, out var control1, out var control2, out var end))
        {
            return;
        }

        var offset = new Vector2(0f, YukaiLarkAssistant.GetAssistBobOffset(totalGameTime));
        _edgeRenderer.DrawTransitionEventGhost(transition, start + offset, control1 + offset, control2 + offset, end + offset, 1f);
    }
}