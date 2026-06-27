namespace YukaiLarkStateTransitionDiagram.Assistants;

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

internal sealed class YukaiLarkAssistOperation
{
    public required YukaiLarkAssistKind Kind { get; init; }
    public required Viewport Viewport { get; init; }
    public required List<DiagramNode> Nodes { get; init; }
    public required List<DiagramTransition> Transitions { get; init; }
    public required int NextNodeId { get; init; }
    public required int PaletteLength { get; init; }
    public required Func<Vector2, Vector2> ScreenToWorld { get; init; }
    public required Func<Vector2, Vector2> SnapToHalfGrid { get; init; }
    public required Action<Action> ExecuteUndoableChange { get; init; }
    public required Action<DiagramTransition> InitializeTransitionEndpoints { get; init; }
    public required Func<Viewport, YukaiLarkAssistKind, Vector2> GetNodeScreenPosition { get; init; }
    public required float ShiftDiagramLeftDistance { get; init; }
}

internal readonly record struct YukaiLarkAssistOperationResult(
    int NextNodeId,
    DiagramNode? SelectedNode,
    DiagramTransition? SelectedTransition,
    string Status,
    bool Completed);

internal static class YukaiLarkAssistOperations
{
    public static YukaiLarkAssistOperationResult Run(YukaiLarkAssistOperation operation)
        => operation.Kind switch
        {
            YukaiLarkAssistKind.CreateStartMarker => CreateStartMarker(operation),
            YukaiLarkAssistKind.CreateStateNode => CreateStateNode(operation, YukaiLarkAssistKind.CreateStateNode),
            YukaiLarkAssistKind.CreateSecondStateNode => CreateStateNode(operation, YukaiLarkAssistKind.CreateSecondStateNode),
            YukaiLarkAssistKind.CreateTransition => CreateTransition(operation, YukaiLarkAssistKind.CreateTransition),
            YukaiLarkAssistKind.ConnectUnreachedStateNode => CreateTransition(operation, YukaiLarkAssistKind.ConnectUnreachedStateNode),
            YukaiLarkAssistKind.AddTransitionEvent => AddTransitionEvent(operation),
            YukaiLarkAssistKind.ShiftDiagramLeft => ShiftDiagramLeft(operation),
            YukaiLarkAssistKind.CreateEndMarker => CreateEndMarker(operation),
            _ => new YukaiLarkAssistOperationResult(operation.NextNodeId, null, null, string.Empty, false)
        };

    private static YukaiLarkAssistOperationResult CreateStartMarker(YukaiLarkAssistOperation operation)
    {
        var nextNodeId = operation.NextNodeId;
        DiagramNode? selectedNode = null;
        var screenPosition = operation.GetNodeScreenPosition(operation.Viewport, YukaiLarkAssistKind.CreateStartMarker);
        var worldPosition = operation.ScreenToWorld(screenPosition);
        operation.ExecuteUndoableChange(() =>
        {
            var node = new DiagramNode
            {
                Id = nextNodeId++,
                Label = "開始",
                Position = operation.SnapToHalfGrid(worldPosition),
                RadiusUnits = DiagramNode.TerminalRadiusUnits,
                ColorIndex = 0,
                Kind = NodeKind.StartMarker
            };
            operation.Nodes.Add(node);
            selectedNode = node;
        });

        return new YukaiLarkAssistOperationResult(
            nextNodeId,
            selectedNode,
            null,
            "開始マークを作成しました。次はNで状態追加、Shift+ドラッグで遷移作成。",
            selectedNode is not null);
    }

    private static YukaiLarkAssistOperationResult CreateEndMarker(YukaiLarkAssistOperation operation)
    {
        var nextNodeId = operation.NextNodeId;
        DiagramNode? selectedNode = null;
        var screenPosition = operation.GetNodeScreenPosition(operation.Viewport, YukaiLarkAssistKind.CreateEndMarker);
        var worldPosition = operation.ScreenToWorld(screenPosition);
        operation.ExecuteUndoableChange(() =>
        {
            var node = new DiagramNode
            {
                Id = nextNodeId++,
                Label = "終了",
                Position = operation.SnapToHalfGrid(worldPosition),
                RadiusUnits = DiagramNode.TerminalRadiusUnits,
                ColorIndex = 0,
                Kind = NodeKind.EndMarker
            };
            operation.Nodes.Add(node);
            selectedNode = node;
        });

        return new YukaiLarkAssistOperationResult(
            nextNodeId,
            selectedNode,
            null,
            "終了マークを作成しました。必要なら状態から終了へ遷移をつなげます。",
            selectedNode is not null);
    }

    private static YukaiLarkAssistOperationResult CreateStateNode(YukaiLarkAssistOperation operation, YukaiLarkAssistKind kind)
    {
        var nextNodeId = operation.NextNodeId;
        DiagramNode? selectedNode = null;
        var screenPosition = operation.GetNodeScreenPosition(operation.Viewport, kind);
        var worldPosition = operation.ScreenToWorld(screenPosition);
        operation.ExecuteUndoableChange(() =>
        {
            var nodeId = nextNodeId++;
            var node = new DiagramNode
            {
                Id = nodeId,
                Label = $"状態{nodeId}",
                Position = operation.SnapToHalfGrid(worldPosition),
                RadiusUnits = DiagramNode.DefaultRadiusUnits,
                ColorIndex = (nodeId - 1) % operation.PaletteLength
            };
            operation.Nodes.Add(node);
            selectedNode = node;
        });

        var status = kind == YukaiLarkAssistKind.CreateSecondStateNode
            ? "2つ目の状態ノードを作成しました。次は通常ノード同士の遷移をつなげます。"
            : "状態ノードを作成しました。次は開始マークから遷移をつなげます。";
        return new YukaiLarkAssistOperationResult(
            nextNodeId,
            selectedNode,
            null,
            status,
            selectedNode is not null);
    }

    private static YukaiLarkAssistOperationResult ShiftDiagramLeft(YukaiLarkAssistOperation operation)
    {
        if (operation.Nodes.Count == 0 || operation.ShiftDiagramLeftDistance <= 0f)
        {
            return new YukaiLarkAssistOperationResult(
                operation.NextNodeId,
                null,
                null,
                "左へ寄せる図がありません。",
                false);
        }

        var delta = new Vector2(-operation.ShiftDiagramLeftDistance, 0f);
        operation.ExecuteUndoableChange(() =>
        {
            foreach (var node in operation.Nodes)
            {
                node.Position += delta;
            }

            foreach (var transition in operation.Transitions)
            {
                if (transition.ControlPoint1.HasValue)
                {
                    transition.ControlPoint1 += delta;
                }
                if (transition.ControlPoint2.HasValue)
                {
                    transition.ControlPoint2 += delta;
                }

                for (var i = 0; i < transition.Waypoints.Count; i++)
                {
                    transition.Waypoints[i] += delta;
                }

                foreach (var segmentControls in transition.SegmentControls)
                {
                    if (segmentControls.ControlPoint1.HasValue)
                    {
                        segmentControls.ControlPoint1 += delta;
                    }
                    if (segmentControls.ControlPoint2.HasValue)
                    {
                        segmentControls.ControlPoint2 += delta;
                    }
                }
            }
        });

        return new YukaiLarkAssistOperationResult(
            operation.NextNodeId,
            null,
            null,
            "図を左に寄せました。右側の作業スペースを広げました。",
            true);
    }
    private static YukaiLarkAssistOperationResult AddTransitionEvent(YukaiLarkAssistOperation operation)
    {
        var transition = operation.Transitions.FirstOrDefault(t => CanTransitionHaveEvent(operation.Nodes, t) && string.IsNullOrWhiteSpace(t.Label));
        if (transition is null)
        {
            return new YukaiLarkAssistOperationResult(
                operation.NextNodeId,
                null,
                null,
                "イベント未設定の遷移はありません。",
                false);
        }

        var sourceLabel = GetNodeLabel(operation.Nodes, transition.SourceId);
        var targetLabel = GetNodeLabel(operation.Nodes, transition.TargetId);
        return new YukaiLarkAssistOperationResult(
            operation.NextNodeId,
            null,
            transition,
            $"{sourceLabel} と {targetLabel} 間の遷移イベントを入力中です。Enterで確定します。",
            true);
    }

    private static bool CanTransitionHaveEvent(IEnumerable<DiagramNode> nodes, DiagramTransition transition)
    {
        var source = nodes.FirstOrDefault(node => node.Id == transition.SourceId);
        var target = nodes.FirstOrDefault(node => node.Id == transition.TargetId);
        return source?.Kind != NodeKind.StartMarker || target?.Kind != NodeKind.Normal;
    }

    private static string GetNodeLabel(IEnumerable<DiagramNode> nodes, int nodeId)
    {
        var node = nodes.FirstOrDefault(n => n.Id == nodeId);
        return node is null || string.IsNullOrWhiteSpace(node.Label) ? $"状態{nodeId}" : node.Label;
    }

    private static YukaiLarkAssistOperationResult CreateTransition(YukaiLarkAssistOperation operation, YukaiLarkAssistKind kind)
    {
        DiagramNode source;
        DiagramNode target;
        var hasEndpoints = kind == YukaiLarkAssistKind.ConnectUnreachedStateNode
            ? TryGetUnreachedNormalTransitionEndpoints(operation.Nodes, operation.Transitions, out source, out target)
            : TryGetTransitionEndpoints(operation.Nodes, operation.Transitions, out source, out target);
        if (!hasEndpoints)
        {
            return new YukaiLarkAssistOperationResult(
                operation.NextNodeId,
                null,
                null,
                "遷移を作るには接続元と接続先の状態が必要です。",
                false);
        }

        if (operation.Transitions.Any(t => t.SourceId == source.Id && t.TargetId == target.Id))
        {
            return new YukaiLarkAssistOperationResult(
                operation.NextNodeId,
                null,
                null,
                "同じ向きの遷移は既にあります。",
                false);
        }

        DiagramTransition? selectedTransition = null;
        operation.ExecuteUndoableChange(() =>
        {
            var transition = new DiagramTransition { SourceId = source.Id, TargetId = target.Id };
            operation.InitializeTransitionEndpoints(transition);
            operation.Transitions.Add(transition);
            selectedTransition = transition;
        });

        var status = kind == YukaiLarkAssistKind.ConnectUnreachedStateNode
            ? "入ってくる遷移がなかった通常ノードへ、近くのノードから遷移を作成しました。"
            : source.Kind == NodeKind.StartMarker
                ? "開始マークから次の状態へ遷移を作成しました。この遷移にはイベントを付けません。"
                : target.Kind == NodeKind.EndMarker
                    ? "開始から一番遠い状態から終了マークへ遷移を作成しました。次はイベントを追加できます。"
                    : "通常ノード同士の遷移を作成しました。次はイベントを追加できます。";
        return new YukaiLarkAssistOperationResult(
            operation.NextNodeId,
            null,
            selectedTransition,
            status,
            selectedTransition is not null);
    }

    private static bool TryGetUnreachedNormalTransitionEndpoints(
        IReadOnlyCollection<DiagramNode> nodes,
        IEnumerable<DiagramTransition> transitions,
        out DiagramNode source,
        out DiagramNode target)
    {
        var transitionList = transitions.ToList();
        var bestDistance = float.MaxValue;
        source = null!;
        target = null!;

        foreach (var candidateTarget in nodes.Where(node => node.Kind == NodeKind.Normal && !transitionList.Any(t => t.TargetId == node.Id)))
        {
            foreach (var candidateSource in nodes.Where(node => CanConnectUnreachedNormalFrom(node, candidateTarget, transitionList)))
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

    private static bool CanConnectUnreachedNormalFrom(DiagramNode source, DiagramNode target, IEnumerable<DiagramTransition> transitions)
        => source.Id != target.Id
            && source.Kind != NodeKind.EndMarker
            && (source.Kind != NodeKind.StartMarker || !transitions.Any(t => t.SourceId == source.Id))
            && !transitions.Any(t => t.SourceId == source.Id && t.TargetId == target.Id);
    private static bool TryGetTransitionEndpoints(
        IReadOnlyCollection<DiagramNode> nodes,
        IEnumerable<DiagramTransition> transitions,
        out DiagramNode source,
        out DiagramNode target)
    {
        var startMarker = nodes.FirstOrDefault(node => node.Kind == NodeKind.StartMarker);
        var endMarker = nodes.FirstOrDefault(node => node.Kind == NodeKind.EndMarker);
        var normalNodes = nodes
            .Where(node => node.Kind == NodeKind.Normal)
            .OrderBy(node => node.Id)
            .ToList();

        if (startMarker is not null && normalNodes.Count >= 1 && !transitions.Any(t => t.SourceId == startMarker.Id && t.TargetId == normalNodes[0].Id))
        {
            source = startMarker;
            target = normalNodes[0];
            return true;
        }

        if (normalNodes.Count >= 2 && !transitions.Any(t => t.SourceId == normalNodes[0].Id && t.TargetId == normalNodes[1].Id))
        {
            source = normalNodes[0];
            target = normalNodes[1];
            return true;
        }

        var normalToEndSource = startMarker is null
            ? normalNodes.LastOrDefault()
            : normalNodes
                .OrderByDescending(node => (node.Position - startMarker.Position).LengthSquared())
                .ThenBy(node => node.Id)
                .FirstOrDefault();
        if (endMarker is not null
            && normalToEndSource is not null
            && !transitions.Any(t => t.SourceId == normalToEndSource.Id && t.TargetId == endMarker.Id))
        {
            source = normalToEndSource;
            target = endMarker;
            return true;
        }

        source = null!;
        target = null!;
        return false;
    }
}
