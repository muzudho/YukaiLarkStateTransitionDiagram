namespace YukaiLarkStateTransitionDiagram;

using System;
using System.Collections.Generic;
using System.Linq;
using YukaiLarkStateTransitionDiagram.Navigation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

public partial class Game1
{
    private void AddDiagramTab()
    {
        ExecuteUndoableChange(() =>
        {
            var id = GetNextDiagramId();
            _diagrams.Add(DiagramInstance.CreateNew(id));
            _currentDiagramIndex = _diagrams.Count - 1;
            AddInitialMarkers();
        });

        ResetTransientDiagramInteractionState();
        _status = $"{CurrentDiagram.Name} を追加しました。Ctrl+Tabで切り替えられます。";
    }

    private void SelectNextDiagramTab()
        => SelectDiagramTab((_currentDiagramIndex + 1) % _diagrams.Count);

    private void SelectPreviousDiagramTab()
        => SelectDiagramTab((_currentDiagramIndex - 1 + _diagrams.Count) % _diagrams.Count);

    private void DeleteCurrentDiagramTab()
    {
        if (_diagrams.Count <= 1)
        {
            _status = "最後のタブは削除できません。";
            return;
        }

        var deletedName = CurrentDiagram.Name;
        var deletedId = CurrentDiagram.Id;
        ExecuteUndoableChange(() =>
        {
            var deletedIds = CollectSubstateDiagramTreeIds(deletedId);
            _diagrams.RemoveAll(diagram => deletedIds.Contains(diagram.Id));
            ClearSubstateReferencesTo(deletedIds);
            if (_diagrams.Count == 0)
            {
                _diagrams.Add(DiagramInstance.CreateDefault());
            }
            _currentDiagramIndex = Math.Clamp(_currentDiagramIndex, 0, _diagrams.Count - 1);
            CurrentDiagram.RefreshNextNodeId();
        });

        ResetTransientDiagramInteractionState();
        _status = $"{deletedName} を削除しました。Ctrl+Zで元に戻せます。";
    }

    private void BeginDiagramTabNameEdit()
    {
        _isEditingDiagramTabName = true;
        _editingNode = null;
        _editingTransition = null;
        _draggedNode = null;
        _resizedNode = null;
        _linkSource = null;
        _isPanning = false;
        _textBoxController.Begin(CurrentDiagram.Name);
        BeginTextInputIme();
        _status = "タブ名を編集中です。Enterで確定、Escでキャンセルします。";
    }

    private void CommitDiagramTabNameEdit()
    {
        var name = _textBoxController.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            name = $"ダイアグラム{CurrentDiagram.Id}";
        }

        if (CurrentDiagram.Name != name)
        {
            ExecuteUndoableChange(() => CurrentDiagram.Name = name);
        }

        EndTextInputIme();
        _isEditingDiagramTabName = false;
        _textBoxController.Clear();
        _status = $"タブ名を {CurrentDiagram.Name} に更新しました。Ctrl+Sで保存できます。";
    }

    private void CancelDiagramTabNameEdit()
    {
        EndTextInputIme();
        _isEditingDiagramTabName = false;
        _textBoxController.Clear();
        _status = "タブ名編集をキャンセルしました。";
    }

    private void SelectDiagramTab(int index)
    {
        if (index < 0 || index >= _diagrams.Count || index == _currentDiagramIndex)
        {
            return;
        }

        _currentDiagramIndex = index;
        CurrentDiagram.RefreshNextNodeId();
        ResetTransientDiagramInteractionState();
        _status = $"{CurrentDiagram.Name} に切り替えました。";
    }

    private bool TryHandleDiagramTabClick(MouseState mouse)
    {
        var viewport = GraphicsDevice.Viewport;
        if (!DiagramTabRenderer.GetTabBarBounds(viewport).Contains(mouse.Position))
        {
            return false;
        }

        var addBounds = DiagramTabRenderer.GetAddTabBounds(viewport, _diagrams);
        if (addBounds != Rectangle.Empty && addBounds.Contains(mouse.Position))
        {
            AddDiagramTab();
            return true;
        }

        for (var i = 0; i < _diagrams.Count; i++)
        {
            var closeBounds = DiagramTabRenderer.GetTabCloseBounds(viewport, _diagrams, i);
            if (closeBounds != Rectangle.Empty && closeBounds.Contains(mouse.Position))
            {
                SelectDiagramTab(i);
                DeleteCurrentDiagramTab();
                return true;
            }

            var bounds = DiagramTabRenderer.GetTabBounds(viewport, _diagrams, i);
            if (bounds != Rectangle.Empty && bounds.Contains(mouse.Position))
            {
                SelectDiagramTab(i);
                return true;
            }
        }

        return true;
    }

    private void EnterSelectedNodeSubstate()
    {
        if (_selectedNode is null)
        {
            _status = "中へ入る状態を選択してください。";
            return;
        }

        if (_selectedNode.Kind != NodeKind.Normal)
        {
            _status = "サブステートを持てるのは通常ノードだけです。";
            return;
        }

        var parentNode = _selectedNode;
        if (parentNode.SubstateDiagramId is { } existingDiagramId)
        {
            var existingIndex = _diagrams.FindIndex(diagram => diagram.Id == existingDiagramId);
            if (existingIndex >= 0)
            {
                SelectDiagramTab(existingIndex);
                _status = $"{parentNode.Label} のサブステートへ入りました。Alt+Upで親の図へ戻れます。";
                return;
            }
        }

        ExecuteUndoableChange(() =>
        {
            var id = GetNextDiagramId();
            var diagram = DiagramInstance.CreateNew(id);
            diagram.Name = GetSubstateDiagramName(parentNode);
            _diagrams.Add(diagram);
            parentNode.SubstateDiagramId = id;
            _currentDiagramIndex = _diagrams.Count - 1;
            AddInitialMarkers();
        });

        ResetTransientDiagramInteractionState();
        _status = $"{parentNode.Label} のサブステートを作成して入りました。Alt+Upで親の図へ戻れます。";
    }

    private void ExitToParentSubstate()
    {
        if (!TryFindParentSubstate(out var parentDiagramIndex, out var parentNode))
        {
            _status = "この図には戻り先の親ステートがありません。";
            return;
        }

        SelectDiagramTab(parentDiagramIndex);
        _status = $"{parentNode.Label} のある親の図へ戻りました。";
    }

    private bool HasParentSubstate()
        => TryFindParentSubstate(out _, out _);

    private bool TryFindParentSubstate(out int parentDiagramIndex, out DiagramNode parentNode)
    {
        var currentDiagramId = CurrentDiagram.Id;
        for (var diagramIndex = 0; diagramIndex < _diagrams.Count; diagramIndex++)
        {
            var diagram = _diagrams[diagramIndex];
            foreach (var node in diagram.Nodes)
            {
                if (node.Kind == NodeKind.Normal && node.SubstateDiagramId == currentDiagramId)
                {
                    parentDiagramIndex = diagramIndex;
                    parentNode = node;
                    return true;
                }
            }
        }

        parentDiagramIndex = -1;
        parentNode = null!;
        return false;
    }

    private static string GetSubstateDiagramName(DiagramNode parentNode)
    {
        var label = string.IsNullOrWhiteSpace(parentNode.Label) ? $"状態{parentNode.Id}" : parentNode.Label.Trim();
        return label.Length > 16 ? $"{label[..16]}…" : $"{label}の中";
    }

    private void RemoveSubstateDiagramTree(int? rootDiagramId)
    {
        if (rootDiagramId is not { } id)
        {
            return;
        }

        var removeIds = CollectSubstateDiagramTreeIds(id);
        if (removeIds.Count == 0)
        {
            return;
        }

        _diagrams.RemoveAll(diagram => removeIds.Contains(diagram.Id));
        ClearSubstateReferencesTo(removeIds);
        if (_diagrams.Count == 0)
        {
            _diagrams.Add(DiagramInstance.CreateDefault());
        }

        _currentDiagramIndex = Math.Clamp(_currentDiagramIndex, 0, _diagrams.Count - 1);
    }

    private HashSet<int> CollectSubstateDiagramTreeIds(int rootDiagramId)
    {
        var diagramById = _diagrams.ToDictionary(diagram => diagram.Id);
        var removeIds = new HashSet<int>();
        var pending = new Stack<int>();
        pending.Push(rootDiagramId);

        while (pending.Count > 0)
        {
            var id = pending.Pop();
            if (!removeIds.Add(id) || !diagramById.TryGetValue(id, out var diagram))
            {
                continue;
            }

            foreach (var childId in diagram.Nodes
                .Where(node => node.Kind == NodeKind.Normal && node.SubstateDiagramId.HasValue)
                .Select(node => node.SubstateDiagramId!.Value))
            {
                pending.Push(childId);
            }
        }

        return removeIds;
    }

    private void ClearSubstateReferencesTo(HashSet<int> removedDiagramIds)
    {
        foreach (var diagram in _diagrams)
        {
            foreach (var node in diagram.Nodes)
            {
                if (node.SubstateDiagramId is { } id && removedDiagramIds.Contains(id))
                {
                    node.SubstateDiagramId = null;
                }
            }
        }
    }

    private int GetNextDiagramId()
    {
        var maxId = 0;
        foreach (var diagram in _diagrams)
        {
            maxId = Math.Max(maxId, diagram.Id);
        }

        return maxId + 1;
    }

    private void ResetTransientDiagramInteractionState()
    {
        if (_isEditingFileName || _isEditingDiagramTabName || IsEditingLabel)
        {
            EndTextInputIme();
        }

        _isEditingDiagramTabName = false;

        _selectedNode = null;
        _selectedTransition = null;
        _draggedNode = null;
        _draggedNodeTransitionSnapshots.Clear();
        _linkSource = null;
        _invalidLinkSource = null;
        _editingNode = null;
        _editingTransition = null;
        _draggedHandleTransition = null;
        _draggedHandleKind = TransitionHandleKind.None;
        _draggedWaypointIndex = -1;
        _selectedWaypointTransition = null;
        _selectedWaypointIndex = -1;
        _draggedLabelTransition = null;
        _resizedNode = null;
        _linkWaypoints.Clear();
        _textBoxController.Clear();
        _isPanning = false;
        _isMiniMapDragging = false;
        _isExportSelecting = false;
        _exportSelectionDragging = false;
        _hasExportSelection = false;
    }
}