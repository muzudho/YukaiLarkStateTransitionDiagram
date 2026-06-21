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
            YukaiLarkAssistKind.CreateStartNode => CreateStartNode(operation),
            YukaiLarkAssistKind.CreateStateNode => CreateStateNode(operation),
            YukaiLarkAssistKind.CreateTransition => CreateTransition(operation),
            _ => new YukaiLarkAssistOperationResult(operation.NextNodeId, null, null, string.Empty, false)
        };

    private static YukaiLarkAssistOperationResult CreateStartNode(YukaiLarkAssistOperation operation)
    {
        var nextNodeId = operation.NextNodeId;
        DiagramNode? selectedNode = null;
        var screenPosition = operation.GetNodeScreenPosition(operation.Viewport, YukaiLarkAssistKind.CreateStartNode);
        var worldPosition = operation.ScreenToWorld(screenPosition);
        operation.ExecuteUndoableChange(() =>
        {
            var node = new DiagramNode
            {
                Id = nextNodeId++,
                Label = "開始",
                Position = operation.SnapToHalfGrid(worldPosition),
                RadiusUnits = DiagramNode.DefaultRadiusUnits,
                ColorIndex = 0,
                Kind = NodeKind.Start
            };
            operation.Nodes.Add(node);
            selectedNode = node;
        });

        return new YukaiLarkAssistOperationResult(
            nextNodeId,
            selectedNode,
            null,
            "開始ノードを作成しました。次はNで状態追加、Shift+ドラッグで遷移作成。",
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
            "状態ノードを作成しました。次は開始ノードから遷移をつなげます。",
            selectedNode is not null);
    }

    private static YukaiLarkAssistOperationResult CreateTransition(YukaiLarkAssistOperation operation)
    {
        var source = operation.Nodes.FirstOrDefault(node => node.Kind == NodeKind.Start);
        var target = operation.Nodes.FirstOrDefault(node => node.Kind != NodeKind.Start);
        if (source is null || target is null)
        {
            return new YukaiLarkAssistOperationResult(
                operation.NextNodeId,
                null,
                null,
                "遷移を作るには開始ノードと次の状態が必要です。",
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
            "開始から次の状態へ遷移を作成しました。F2・Enterでラベル編集できます。",
            selectedTransition is not null);
    }
}
