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
            YukaiLarkAssistKind.CreateStateNode => CreateStateNode(operation),
            YukaiLarkAssistKind.CreateTransition => CreateTransition(operation),
            YukaiLarkAssistKind.AddTransitionEvent => AddTransitionEvent(operation),
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

    private static YukaiLarkAssistOperationResult CreateStateNode(YukaiLarkAssistOperation operation)
    {
        var nextNodeId = operation.NextNodeId;
        DiagramNode? selectedNode = null;
        var screenPosition = operation.GetNodeScreenPosition(operation.Viewport, YukaiLarkAssistKind.CreateStateNode);
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

        return new YukaiLarkAssistOperationResult(
            nextNodeId,
            selectedNode,
            null,
            "状態ノードを作成しました。次は開始マークから遷移をつなげます。",
            selectedNode is not null);
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

    private static YukaiLarkAssistOperationResult CreateTransition(YukaiLarkAssistOperation operation)
    {
        var source = operation.Nodes.FirstOrDefault(node => node.Kind == NodeKind.StartMarker);
        var target = operation.Nodes.FirstOrDefault(node => node.Kind == NodeKind.Normal);
        if (source is null || target is null)
        {
            return new YukaiLarkAssistOperationResult(
                operation.NextNodeId,
                null,
                null,
                "遷移を作るには開始マークと次の状態が必要です。",
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

        return new YukaiLarkAssistOperationResult(
            operation.NextNodeId,
            null,
            selectedTransition,
            "開始マークから次の状態へ遷移を作成しました。この遷移にはイベントを付けません。",
            selectedTransition is not null);
    }
}
