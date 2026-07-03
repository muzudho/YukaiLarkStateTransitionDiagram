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
        if (_isEditingFileName || IsEditingLabel)
        {
            EndTextInputIme();
        }

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