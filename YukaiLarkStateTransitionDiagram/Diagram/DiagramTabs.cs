namespace YukaiLarkStateTransitionDiagram;

using System;
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
        ExecuteUndoableChange(() =>
        {
            _diagrams.RemoveAt(_currentDiagramIndex);
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