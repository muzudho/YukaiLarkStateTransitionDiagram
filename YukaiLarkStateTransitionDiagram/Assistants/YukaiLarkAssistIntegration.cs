namespace YukaiLarkStateTransitionDiagram;

using System;
using System.Collections.Generic;
using System.Linq;
using YukaiLarkStateTransitionDiagram.Assistants;
using YukaiLarkStateTransitionDiagram.Navigation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public partial class Game1
{
    private readonly HashSet<int> _skippedStateNodeLabelEditAssistNodeIds = new();
    private YukaiLarkAssistantContext CreateAssistantContext()
    {
        var missingTransitionEventSummary = GetMissingTransitionEventSummary();
        var normalNodes = _nodes
            .Where(node => node.Kind == NodeKind.Normal)
            .OrderBy(node => node.Id)
            .ToList();
        var startMarker = _nodes.FirstOrDefault(node => node.Kind == NodeKind.StartMarker);
        var endMarker = _nodes.FirstOrDefault(node => node.Kind == NodeKind.EndMarker);
        var normalToEndSource = startMarker is null
            ? normalNodes.LastOrDefault()
            : normalNodes
                .OrderByDescending(node => (node.Position - startMarker.Position).LengthSquared())
                .ThenBy(node => node.Id)
                .FirstOrDefault();
        var defaultLabeledNormalNode = normalNodes.FirstOrDefault(node =>
            IsDefaultLabeledNormalNode(node)
            && !_skippedStateNodeLabelEditAssistNodeIds.Contains(node.Id));
        var defaultStateNodeLabelSummary = defaultLabeledNormalNode?.Label ?? string.Empty;
        var hasStartToNormalTransition = startMarker is not null
            && normalNodes.Count >= 1
            && _transitions.Any(t => t.SourceId == startMarker.Id && t.TargetId == normalNodes[0].Id);
        var hasNormalToNormalTransition = normalNodes.Count >= 2
            && _transitions.Any(t => t.SourceId == normalNodes[0].Id && t.TargetId == normalNodes[1].Id);
        var hasNormalToEndTransition = endMarker is not null
            && normalToEndSource is not null
            && (_transitions.Any(t => t.SourceId == normalToEndSource.Id && t.TargetId == endMarker.Id)
                || IsAssistSuggestionSuppressed(
                    AssistSuggestionKind.NormalToEndTransition,
                    normalToEndSource.Id,
                    endMarker.Id));
        var isNormalToEndTransitionSuggestion = endMarker is not null
            && normalToEndSource is not null
            && hasStartToNormalTransition
            && (normalNodes.Count < 2 || hasNormalToNormalTransition)
            && !hasNormalToEndTransition;

        var shouldSuggestShiftDiagramLeft = TryGetDiagramShiftLeftDistance(out var shiftDiagramLeftDistance);
        var hasUnreachedNormalNode = TryGetUnreachedNormalTransitionEndpoints(out _, out _);

        return new YukaiLarkAssistantContext(
            startMarker is not null,
            endMarker is not null,
            normalNodes.Count,
            defaultLabeledNormalNode is not null,
            defaultStateNodeLabelSummary,
            hasStartToNormalTransition,
            hasNormalToNormalTransition,
            hasNormalToEndTransition,
            isNormalToEndTransitionSuggestion,
            !string.IsNullOrEmpty(missingTransitionEventSummary),
            missingTransitionEventSummary,
            shouldSuggestShiftDiagramLeft,
            shiftDiagramLeftDistance,
            hasUnreachedNormalNode,
            !IsEditingLabel
                && !_isEditingDiagramTabName
                && !_isEditingFileName
                && !_isExportSelecting
                && !_isPanning
                && _draggedNode is null
                && _linkSource is null
                && _draggedHandleTransition is null
                && _resizedNode is null);
    }
    private bool TryGetDiagramShiftLeftDistance(out float distance)
    {
        distance = 0f;
        var viewport = GraphicsDevice.Viewport;
        if (_nodes.Count < 2 || viewport.Width < 760 || _cameraZoom <= 0f)
        {
            return false;
        }

        var minX = float.MaxValue;
        var maxX = float.MinValue;
        void IncludeScreenX(Vector2 worldPosition)
        {
            var screenX = WorldToScreen(worldPosition).X;
            minX = MathF.Min(minX, screenX);
            maxX = MathF.Max(maxX, screenX);
        }

        foreach (var node in _nodes)
        {
            var screenPosition = WorldToScreen(node.Position);
            var screenRadius = node.Radius * _cameraZoom;
            minX = MathF.Min(minX, screenPosition.X - screenRadius);
            maxX = MathF.Max(maxX, screenPosition.X + screenRadius);
        }

        foreach (var transition in _transitions)
        {
            if (transition.ControlPoint1.HasValue)
            {
                IncludeScreenX(transition.ControlPoint1.Value);
            }
            if (transition.ControlPoint2.HasValue)
            {
                IncludeScreenX(transition.ControlPoint2.Value);
            }
            foreach (var waypoint in transition.Waypoints)
            {
                IncludeScreenX(waypoint);
            }
            foreach (var segmentControls in transition.SegmentControls)
            {
                if (segmentControls.ControlPoint1.HasValue)
                {
                    IncludeScreenX(segmentControls.ControlPoint1.Value);
                }
                if (segmentControls.ControlPoint2.HasValue)
                {
                    IncludeScreenX(segmentControls.ControlPoint2.Value);
                }
            }
        }

        var workAreaRight = viewport.Width;
        if (InspectorPanelRenderer.TryGetPanelBounds(viewport, out var inspectorBounds))
        {
            workAreaRight = inspectorBounds.X;
        }

        const float leftComfortPadding = 80f;
        const float minShiftScreen = 48f;
        var workAreaLeft = leftComfortPadding;
        var workAreaCenter = (workAreaLeft + workAreaRight) / 2f;
        var diagramCenter = (minX + maxX) / 2f;
        var screenShift = diagramCenter - workAreaCenter;
        if (screenShift < minShiftScreen)
        {
            return false;
        }

        var worldShift = screenShift / _cameraZoom;
        distance = MathF.Floor(worldShift / DiagramNode.RadiusUnit) * DiagramNode.RadiusUnit;
        return distance > 0f;
    }
    private static bool IsDefaultLabeledNormalNode(DiagramNode node)
        => node.Kind == NodeKind.Normal
            && string.Equals(node.Label, $"状態{node.Id}", StringComparison.Ordinal);

    private string GetMissingTransitionEventSummary()
    {
        var transition = _transitions.FirstOrDefault(t => CanTransitionHaveEvent(t) && string.IsNullOrWhiteSpace(t.Label));
        if (transition is null)
        {
            return string.Empty;
        }

        var sourceLabel = GetNodeLabel(transition.SourceId);
        var targetLabel = GetNodeLabel(transition.TargetId);
        return $"{sourceLabel} と {targetLabel}";
    }

    private string GetNodeLabel(int nodeId)
    {
        var node = FindNode(nodeId);
        return node is null || string.IsNullOrWhiteSpace(node.Label) ? $"状態{nodeId}" : node.Label;
    }

    private void RunYukaiLarkAssist(YukaiLarkAssistKind kind)
    {
        var context = CreateAssistantContext();
        var result = YukaiLarkAssistOperations.Run(new YukaiLarkAssistOperation
        {
            Kind = kind,
            Viewport = GraphicsDevice.Viewport,
            Nodes = _nodes,
            Transitions = _transitions,
            NextNodeId = _nextNodeId,
            PaletteLength = _boardTheme.NormalNodePalette.Length,
            ScreenToWorld = ScreenToWorld,
            SnapToHalfGrid = SnapToHalfGrid,
            ExecuteUndoableChange = ExecuteUndoableChange,
            InitializeTransitionEndpoints = InitializeTransitionEndpoints,
            GetNodeScreenPosition = _yukaiLarkAssistant.GetNodeScreenPosition,
            ShiftDiagramLeftDistance = context.ShiftDiagramLeftDistance,
            SkippedStateNodeLabelEditAssistNodeIds = _skippedStateNodeLabelEditAssistNodeIds
        });

        _nextNodeId = result.NextNodeId;
        if (kind != YukaiLarkAssistKind.ShiftDiagramLeft)
        {
            _selectedNode = result.SelectedNode;
            _selectedTransition = result.SelectedTransition;
        }
        if (kind == YukaiLarkAssistKind.AddTransitionEvent && result.SelectedTransition is not null)
        {
            BeginTransitionLabelEdit(result.SelectedTransition);
        }
        if (kind == YukaiLarkAssistKind.EditStateNodeLabel && result.SelectedNode is not null)
        {
            BeginLabelEdit(result.SelectedNode);
        }

        if (result.Completed)
        {
            _yukaiLarkAssistant.Reset();
            _yukaiLarkAssistant.NotifyAssistCompleted(kind);
        }

        if (!string.IsNullOrEmpty(result.Status))
        {
            _status = result.Status;
        }
    }

    private void SuppressYukaiLarkAssist(YukaiLarkAssistKind kind)
    {
        if (kind == YukaiLarkAssistKind.EditStateNodeLabel)
        {
            SkipStateNodeLabelEditAssist();
            return;
        }

        if (kind != YukaiLarkAssistKind.CreateTransition
            || !TryGetNormalToEndTransitionSuggestion(out var source, out var target))
        {
            _status = "このアシストは抑制できません。";
            return;
        }

        ExecuteUndoableChange(() =>
        {
            if (!IsAssistSuggestionSuppressed(AssistSuggestionKind.NormalToEndTransition, source.Id, target.Id))
            {
                _assistSuppression.SuppressedSuggestions.Add(new AssistSuggestionSuppression
                {
                    Kind = AssistSuggestionKind.NormalToEndTransition,
                    SourceId = source.Id,
                    TargetId = target.Id
                });
            }
        });

        _yukaiLarkAssistant.Reset();
        _status = $"{GetNodeLabel(source.Id)} から終了マークへつなぐ提案を、この図では抑制しました。Ctrl+Sで保存できます。";
    }

    private void SkipStateNodeLabelEditAssist()
    {
        var node = _nodes
            .Where(node => IsDefaultLabeledNormalNode(node) && !_skippedStateNodeLabelEditAssistNodeIds.Contains(node.Id))
            .OrderBy(node => node.Id)
            .FirstOrDefault();
        if (node is null)
        {
            _status = "ラベル編集を提案できる通常ノードはありません。";
            return;
        }

        _skippedStateNodeLabelEditAssistNodeIds.Add(node.Id);
        _yukaiLarkAssistant.Reset();
        _status = $"{node.Label} のラベル編集アシストを今回は見送ります。F2・Enterではいつでも編集できます。";
    }
    private bool TryGetUnreachedNormalTransitionEndpoints(out DiagramNode source, out DiagramNode target)
    {
        var bestDistance = float.MaxValue;
        source = null!;
        target = null!;

        foreach (var candidateTarget in _nodes.Where(node => node.Kind == NodeKind.Normal && !_transitions.Any(t => t.TargetId == node.Id)))
        {
            foreach (var candidateSource in _nodes.Where(node => CanConnectUnreachedNormalFrom(node, candidateTarget)))
            {
                var distance = (candidateSource.Position - candidateTarget.Position).LengthSquared();
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                source = candidateSource;
                target = candidateTarget;
            }
        }

        return source is not null && target is not null;
    }

    private bool CanConnectUnreachedNormalFrom(DiagramNode source, DiagramNode target)
        => source.Id != target.Id
            && source.Kind != NodeKind.EndMarker
            && (source.Kind != NodeKind.StartMarker || !HasOutgoingTransition(source))
            && !_transitions.Any(t => t.SourceId == source.Id && t.TargetId == target.Id);
    private bool TryGetAssistantTransitionEndpoints(out DiagramNode source, out DiagramNode target)
    {
        var startMarker = _nodes.FirstOrDefault(node => node.Kind == NodeKind.StartMarker);
        var endMarker = _nodes.FirstOrDefault(node => node.Kind == NodeKind.EndMarker);
        var normalNodes = _nodes
            .Where(node => node.Kind == NodeKind.Normal)
            .OrderBy(node => node.Id)
            .ToList();

        if (startMarker is not null
            && normalNodes.Count >= 1
            && !HasOutgoingTransition(startMarker))
        {
            source = startMarker;
            target = normalNodes[0];
            return true;
        }

        if (normalNodes.Count >= 2
            && !_transitions.Any(t => t.SourceId == normalNodes[0].Id && t.TargetId == normalNodes[1].Id))
        {
            source = normalNodes[0];
            target = normalNodes[1];
            return true;
        }

        if (TryGetNormalToEndTransitionSuggestion(out source, out target))
        {
            return true;
        }

        if (TryGetUnreachedNormalTransitionEndpoints(out source, out target))
        {
            return true;
        }

        source = null!;
        target = null!;
        return false;
    }

    private bool TryGetNormalToEndTransitionSuggestion(out DiagramNode source, out DiagramNode target)
    {
        var startMarker = _nodes.FirstOrDefault(node => node.Kind == NodeKind.StartMarker);
        var endMarker = _nodes.FirstOrDefault(node => node.Kind == NodeKind.EndMarker);
        var normalNodes = _nodes
            .Where(node => node.Kind == NodeKind.Normal)
            .OrderBy(node => node.Id)
            .ToList();
        var normalToEndSource = startMarker is null
            ? normalNodes.LastOrDefault()
            : normalNodes
                .OrderByDescending(node => (node.Position - startMarker.Position).LengthSquared())
                .ThenBy(node => node.Id)
                .FirstOrDefault();
        if (endMarker is not null
            && normalToEndSource is not null
            && !_transitions.Any(t => t.SourceId == normalToEndSource.Id && t.TargetId == endMarker.Id)
            && !IsAssistSuggestionSuppressed(AssistSuggestionKind.NormalToEndTransition, normalToEndSource.Id, endMarker.Id))
        {
            source = normalToEndSource;
            target = endMarker;
            return true;
        }

        source = null!;
        target = null!;
        return false;
    }

    private bool IsAssistSuggestionSuppressed(AssistSuggestionKind kind, int sourceId, int targetId)
        => _assistSuppression.SuppressedSuggestions.Any(suppression =>
            suppression.Kind == kind
            && suppression.SourceId == sourceId
            && suppression.TargetId == targetId);

    private void DrawYukaiLarkMascot(Viewport viewport, TimeSpan totalGameTime)
    {
        var mascotTexture = GetYukaiLarkMascotTexture();
        if (mascotTexture is null)
        {
            return;
        }

        _yukaiLarkAssistant.Draw(
            _spriteBatch,
            mascotTexture,
            _pixel,
            viewport,
            totalGameTime,
            CreateAssistantContext(),
            _boardTheme,
            _keyCapTheme,
            GetAssistantAvoidBounds(viewport),
            DrawScreenRectangleOutline,
            DrawUiText);
    }

    private Rectangle GetAssistantAvoidBounds(Viewport viewport)
    {
        var hasInspectorBounds = InspectorPanelRenderer.TryGetPanelBounds(viewport, out var inspectorBounds);
        var hasMiniMapBounds = TryGetMiniMapBounds(viewport, out var miniMapBounds);
        return (hasInspectorBounds, hasMiniMapBounds) switch
        {
            (true, true) => Rectangle.Union(inspectorBounds, miniMapBounds),
            (true, false) => inspectorBounds,
            (false, true) => miniMapBounds,
            _ => Rectangle.Empty
        };
    }
}