namespace YukaiLarkStateTransitionDiagram;

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

public partial class Game1
{
    private const int SubstateLinkMenuMaxVisibleItems = 9;

    private bool _isSubstateLinkMenuOpen;
    private DiagramNode? _substateLinkTargetNode;
    private readonly List<SubstateLinkCandidate> _substateLinkCandidates = new();
    private int _selectedSubstateLinkCandidateIndex;

    private sealed record SubstateLinkCandidate(int DiagramId, string Name);

    private void OpenSubstateLinkMenu()
    {
        if (_selectedNode is null)
        {
            _status = "既存サブステートへ紐づける通常ノードを選択してください。";
            return;
        }

        if (_selectedNode.Kind != NodeKind.Normal)
        {
            _status = "既存サブステートへ紐づけられるのは通常ノードだけです。";
            return;
        }

        if (_selectedNode.SubstateDiagramId.HasValue)
        {
            _status = "選択中の通常ノードには既にサブステートがあります。Alt+Downで中へ入れます。";
            return;
        }

        _substateLinkCandidates.Clear();
        _substateLinkCandidates.AddRange(GetLinkableSubstateCandidates(CurrentDiagram.Id));
        if (_substateLinkCandidates.Count == 0)
        {
            _status = "紐づけ可能な既存サブステートがありません。空きタブを作るか、未使用の図を用意してください。";
            return;
        }

        _isSubstateLinkMenuOpen = true;
        _substateLinkTargetNode = _selectedNode;
        _selectedSubstateLinkCandidateIndex = 0;
        _draggedNode = null;
        _resizedNode = null;
        _linkSource = null;
        _isPanning = false;
        _status = "既存サブステートを選択中です。Enterで紐づけ、Escでキャンセルします。";
    }

    private void CloseSubstateLinkMenu(string status)
    {
        _isSubstateLinkMenuOpen = false;
        _substateLinkTargetNode = null;
        _substateLinkCandidates.Clear();
        _selectedSubstateLinkCandidateIndex = 0;
        _status = status;
    }

    private void HandleSubstateLinkMenuKeyboard(KeyboardState keyboard)
    {
        if (IsNewKeyPress(keyboard, Keys.Escape))
        {
            CloseSubstateLinkMenu("既存サブステートの紐づけをキャンセルしました。");
            return;
        }

        if (IsNewKeyPress(keyboard, Keys.Up))
        {
            MoveSelectedSubstateLinkCandidate(-1);
            return;
        }

        if (IsNewKeyPress(keyboard, Keys.Down))
        {
            MoveSelectedSubstateLinkCandidate(1);
            return;
        }

        if (IsNewKeyPress(keyboard, Keys.Enter))
        {
            CommitSelectedSubstateLinkCandidate();
            return;
        }

        if (TryGetSubstateLinkShortcutIndex(keyboard, out var index))
        {
            if (index < _substateLinkCandidates.Count)
            {
                _selectedSubstateLinkCandidateIndex = index;
                CommitSelectedSubstateLinkCandidate();
            }
        }
    }

    private void HandleSubstateLinkMenuMouse(MouseState mouse)
    {
        if (mouse.LeftButton != ButtonState.Pressed || _previousMouse.LeftButton != ButtonState.Released)
        {
            return;
        }

        for (var i = 0; i < _substateLinkCandidates.Count; i++)
        {
            if (GetSubstateLinkMenuItemRectangle(i).Contains(mouse.Position))
            {
                _selectedSubstateLinkCandidateIndex = i;
                CommitSelectedSubstateLinkCandidate();
                return;
            }
        }

        if (!GetSubstateLinkMenuPanelRectangle().Contains(mouse.Position))
        {
            CloseSubstateLinkMenu("既存サブステートの紐づけをキャンセルしました。");
        }
    }

    private void MoveSelectedSubstateLinkCandidate(int delta)
    {
        if (_substateLinkCandidates.Count == 0)
        {
            return;
        }

        _selectedSubstateLinkCandidateIndex = (_selectedSubstateLinkCandidateIndex + delta + _substateLinkCandidates.Count) % _substateLinkCandidates.Count;
    }

    private void CommitSelectedSubstateLinkCandidate()
    {
        if (_substateLinkTargetNode is null || _substateLinkCandidates.Count == 0)
        {
            CloseSubstateLinkMenu("既存サブステートの紐づけをキャンセルしました。");
            return;
        }

        var candidate = _substateLinkCandidates[Math.Clamp(_selectedSubstateLinkCandidateIndex, 0, _substateLinkCandidates.Count - 1)];
        var targetNode = _substateLinkTargetNode;
        var targetLabel = string.IsNullOrWhiteSpace(targetNode.Label) ? $"状態{targetNode.Id}" : targetNode.Label;
        ExecuteUndoableChange(() => targetNode.SubstateDiagramId = candidate.DiagramId);

        _isSubstateLinkMenuOpen = false;
        _substateLinkTargetNode = null;
        _substateLinkCandidates.Clear();
        _selectedSubstateLinkCandidateIndex = 0;

        var existingIndex = _diagrams.FindIndex(diagram => diagram.Id == candidate.DiagramId);
        if (existingIndex >= 0)
        {
            SelectDiagramTab(existingIndex);
        }

        _status = $"{targetLabel} に {candidate.Name} をサブステートとして紐づけました。Alt+Upで親の図へ戻れます。";
    }

    private List<SubstateLinkCandidate> GetLinkableSubstateCandidates(int parentDiagramId)
    {
        var usedSubstateIds = _diagrams
            .SelectMany(diagram => diagram.Nodes)
            .Where(node => node.Kind == NodeKind.Normal && node.SubstateDiagramId.HasValue)
            .Select(node => node.SubstateDiagramId!.Value)
            .ToHashSet();

        return _diagrams
            .Where(diagram => diagram.Id != parentDiagramId)
            .Where(diagram => !usedSubstateIds.Contains(diagram.Id))
            .Where(diagram => !WouldCreateSubstateCycle(parentDiagramId, diagram.Id))
            .OrderBy(diagram => diagram.Id)
            .Take(SubstateLinkMenuMaxVisibleItems)
            .Select(diagram => new SubstateLinkCandidate(diagram.Id, string.IsNullOrWhiteSpace(diagram.Name) ? $"ダイアグラム{diagram.Id}" : diagram.Name))
            .ToList();
    }

    private bool WouldCreateSubstateCycle(int parentDiagramId, int candidateChildDiagramId)
        => IsDiagramReachable(candidateChildDiagramId, parentDiagramId);

    private bool IsDiagramReachable(int startDiagramId, int targetDiagramId)
    {
        var diagramById = _diagrams.ToDictionary(diagram => diagram.Id);
        var visited = new HashSet<int>();
        var pending = new Stack<int>();
        pending.Push(startDiagramId);

        while (pending.Count > 0)
        {
            var diagramId = pending.Pop();
            if (diagramId == targetDiagramId)
            {
                return true;
            }

            if (!visited.Add(diagramId) || !diagramById.TryGetValue(diagramId, out var diagram))
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

        return false;
    }

    private bool TryGetSubstateLinkShortcutIndex(KeyboardState keyboard, out int index)
    {
        for (var i = 0; i < Math.Min(9, _substateLinkCandidates.Count); i++)
        {
            var key = Keys.D1 + i;
            var numPadKey = Keys.NumPad1 + i;
            if (IsNewKeyPress(keyboard, key) || IsNewKeyPress(keyboard, numPadKey))
            {
                index = i;
                return true;
            }
        }

        index = -1;
        return false;
    }

    private void DrawSubstateLinkMenuOverlay()
    {
        if (!_isSubstateLinkMenuOpen)
        {
            return;
        }

        var viewport = GraphicsDevice.Viewport;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), WithAlpha(Blend(_boardTheme.BackgroundColor, Color.Black, 0.42f), 150));

        var panel = GetSubstateLinkMenuPanelRectangle();
        _spriteBatch.Draw(_pixel, new Rectangle(panel.X + 6, panel.Y + 8, panel.Width, panel.Height), WithAlpha(Blend(_boardTheme.BackgroundColor, Color.Black, 0.42f), 115));
        _spriteBatch.Draw(_pixel, panel, WithAlpha(_boardTheme.PanelBackgroundColor, 246));
        DrawScreenRectangleOutline(panel, WithAlpha(_boardTheme.PanelTopEdgeColor, 235), 2);

        var nodeLabel = _substateLinkTargetNode is null || string.IsNullOrWhiteSpace(_substateLinkTargetNode.Label)
            ? "選択中の状態"
            : _substateLinkTargetNode.Label;
        DrawUiText("既存サブステートへ紐づけ", new Vector2(panel.X + 24, panel.Y + 22), _boardTheme.PanelPrimaryTextColor, 24, true);
        DrawUiText($"{nodeLabel} に入る既存の図を選んでください。", new Vector2(panel.X + 24, panel.Y + 58), _boardTheme.PanelSecondaryTextColor, 15, false);
        DrawUiText("Up/Downで選択、Enterで紐づけ、Escでキャンセル", new Vector2(panel.X + 24, panel.Y + 82), _boardTheme.PanelMutedTextColor, 13, false);

        for (var i = 0; i < Math.Min(_substateLinkCandidates.Count, SubstateLinkMenuMaxVisibleItems); i++)
        {
            DrawSubstateLinkMenuItem(i, _substateLinkCandidates[i]);
        }
    }

    private void DrawSubstateLinkMenuItem(int index, SubstateLinkCandidate candidate)
    {
        var bounds = GetSubstateLinkMenuItemRectangle(index);
        var selected = index == _selectedSubstateLinkCandidateIndex;
        var fill = selected
            ? WithAlpha(Blend(_boardTheme.PanelBackgroundColor, _keyCapTheme.FaceColor, 0.36f), 250)
            : index % 2 == 0
                ? WithAlpha(Blend(_boardTheme.PanelBackgroundColor, _boardTheme.BackgroundColor, 0.12f), 245)
                : WithAlpha(Blend(_boardTheme.PanelBackgroundColor, _keyCapTheme.FaceColor, 0.16f), 245);
        var edge = selected ? WithAlpha(_boardTheme.SelectedTransitionLineColor, 232) : WithAlpha(_boardTheme.PanelTopEdgeColor, 190);
        _spriteBatch.Draw(_pixel, bounds, fill);
        DrawScreenRectangleOutline(bounds, edge, selected ? 2 : 1);

        var shortcut = (index + 1).ToString();
        DrawUiText(shortcut, new Vector2(bounds.X + 14, bounds.Y + 12), _keyCapTheme.LabelTextColor, 16, true);
        DrawUiText(candidate.Name, new Vector2(bounds.X + 46, bounds.Y + 8), _boardTheme.PanelPrimaryTextColor, 17, true);
        DrawUiText($"ID {candidate.DiagramId}", new Vector2(bounds.X + 46, bounds.Y + 32), _boardTheme.PanelMutedTextColor, 12, false);
    }

    private Rectangle GetSubstateLinkMenuItemRectangle(int index)
    {
        var panel = GetSubstateLinkMenuPanelRectangle();
        return new Rectangle(panel.X + 24, panel.Y + 122 + index * 50, panel.Width - 48, 44);
    }

    private Rectangle GetSubstateLinkMenuPanelRectangle()
    {
        var viewport = GraphicsDevice.Viewport;
        var visibleCount = Math.Clamp(_substateLinkCandidates.Count, 1, SubstateLinkMenuMaxVisibleItems);
        var width = Math.Clamp(viewport.Width - 64, 520, 720);
        var height = Math.Min(viewport.Height - 64, 146 + visibleCount * 50);
        var x = (viewport.Width - width) / 2;
        var y = Math.Max(24, (viewport.Height - height) / 2);
        return new Rectangle(x, y, width, height);
    }
}
