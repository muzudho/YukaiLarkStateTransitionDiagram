namespace YukaiLarkStateTransitionDiagram;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using YukaiLarkStateTransitionDiagram.Persistence;

public partial class Game1
{
    private const int MaxHistoryCount = 100;

    private readonly Stack<DiagramDocument> _undoHistory = new();
    private readonly Stack<DiagramDocument> _redoHistory = new();
    private DiagramDocument? _pendingHistorySnapshot;

    private void ExecuteUndoableChange(Action change)
    {
        var before = CaptureDiagramDocument();
        change();
        if (!AreDiagramDocumentsEqual(before, CaptureDiagramDocument()))
        {
            PushHistorySnapshot(_undoHistory, before);
            _redoHistory.Clear();
        }
    }

    private void BeginPendingHistory()
    {
        if (_pendingHistorySnapshot is not null)
        {
            return;
        }

        _pendingHistorySnapshot = CaptureDiagramDocument();
    }

    private void CommitPendingHistory()
    {
        if (_pendingHistorySnapshot is null)
        {
            return;
        }

        if (!AreDiagramDocumentsEqual(_pendingHistorySnapshot, CaptureDiagramDocument()))
        {
            PushHistorySnapshot(_undoHistory, _pendingHistorySnapshot);
            _redoHistory.Clear();
        }
        _pendingHistorySnapshot = null;
    }

    private void UndoDiagramChange()
    {
        _pendingHistorySnapshot = null;
        if (_undoHistory.Count == 0)
        {
            _status = "元に戻せる操作はありません。";
            return;
        }

        var current = CaptureDiagramDocument();
        var previous = _undoHistory.Pop();
        PushHistorySnapshot(_redoHistory, current);
        ApplyDiagramDocument(previous);
        _status = "操作を元に戻しました。Ctrl+Yでやり直せます。";
    }

    private void RedoDiagramChange()
    {
        _pendingHistorySnapshot = null;
        if (_redoHistory.Count == 0)
        {
            _status = "やり直せる操作はありません。";
            return;
        }

        var current = CaptureDiagramDocument();
        var next = _redoHistory.Pop();
        PushHistorySnapshot(_undoHistory, current);
        ApplyDiagramDocument(next);
        _status = "操作をやり直しました。Ctrl+Zで元に戻せます。";
    }

    private void ClearHistory()
    {
        _undoHistory.Clear();
        _redoHistory.Clear();
        _pendingHistorySnapshot = null;
    }

    private void PushHistorySnapshot(Stack<DiagramDocument> history, DiagramDocument document)
    {
        history.Push(CloneDiagramDocument(document));
        if (history.Count <= MaxHistoryCount)
        {
            return;
        }

        var snapshots = history.Reverse().Skip(1).ToList();
        history.Clear();
        foreach (var snapshot in snapshots)
        {
            history.Push(snapshot);
        }
    }

    private DiagramDocument CaptureDiagramDocument()
        => CloneDiagramDocument(new DiagramDocument
        {
            Data = new DiagramDataSection { Nodes = _nodes, Transitions = _transitions },
            AssistSuppression = _assistSuppression
        });

    private void ApplyDiagramDocument(DiagramDocument document)
    {
        var snapshot = CloneDiagramDocument(document);
        _nodes.Clear();
        _transitions.Clear();
        _assistSuppression.SuppressedSuggestions.Clear();
        _nodes.AddRange(snapshot.Nodes);
        _transitions.AddRange(snapshot.Transitions);
        _assistSuppression.SuppressedSuggestions.AddRange(snapshot.AssistSuppression.SuppressedSuggestions);
        foreach (var transition in _transitions)
        {
            InitializeTransitionEndpoints(transition);
        }

        _nextNodeId = _nodes.Count == 0 ? 1 : _nodes.Max(n => n.Id) + 1;
        _selectedNode = null;
        _selectedTransition = null;
        _draggedNode = null;
        _linkSource = null;
        _editingNode = null;
        _editingTransition = null;
        _isEditingFileName = false;
        _fileNameTextBoxController.Clear();
        _fileNameEditWarning = string.Empty;
        _draggedHandleTransition = null;
        _draggedHandleKind = TransitionHandleKind.None;
        _draggedLabelTransition = null;
        _resizedNode = null;
        _textBoxController.Clear();
        _isPanning = false;
        _isExportSelecting = false;
        _exportSelectionDragging = false;
        _hasExportSelection = false;
    }

    private static DiagramDocument CloneDiagramDocument(DiagramDocument document)
        => new()
        {
            FormatVersion = document.FormatVersion,
            Data = new DiagramDataSection
            {
                Nodes = document.Nodes.Select(CloneDiagramNode).ToList(),
                Transitions = document.Transitions.Select(CloneDiagramTransition).ToList()
            },
            AssistSuppression = new AssistSuppressionSection
            {
                SuppressedSuggestions = document.AssistSuppression.SuppressedSuggestions.Select(CloneAssistSuggestionSuppression).ToList()
            }
        };

    private static DiagramNode CloneDiagramNode(DiagramNode node)
        => new()
        {
            Id = node.Id,
            Label = node.Label,
            Position = node.Position,
            RadiusUnits = node.RadiusUnits,
            ColorIndex = node.ColorIndex,
            Kind = node.Kind
        };

    private static DiagramTransition CloneDiagramTransition(DiagramTransition transition)
        => new()
        {
            SourceId = transition.SourceId,
            TargetId = transition.TargetId,
            Label = transition.Label,
            LabelSide = transition.LabelSide,
            LabelAnchorT = transition.LabelAnchorT,
            LabelOffset = transition.LabelOffset,
            SourceAngle = transition.SourceAngle,
            TargetAngle = transition.TargetAngle,
            ControlPoint1 = transition.ControlPoint1,
            ControlPoint2 = transition.ControlPoint2
        };

    private static AssistSuggestionSuppression CloneAssistSuggestionSuppression(AssistSuggestionSuppression suppression)
        => new()
        {
            Kind = suppression.Kind,
            SourceId = suppression.SourceId,
            TargetId = suppression.TargetId
        };

    private static bool AreDiagramDocumentsEqual(DiagramDocument left, DiagramDocument right)
        => JsonSerializer.Serialize(left, YukaiDialogJsonSerializer.Options) == JsonSerializer.Serialize(right, YukaiDialogJsonSerializer.Options);
}