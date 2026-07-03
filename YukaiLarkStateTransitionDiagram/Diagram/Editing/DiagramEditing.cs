namespace YukaiLarkStateTransitionDiagram;

using System.Collections.Generic;
using System.Linq;
using YukaiLarkStateTransitionDiagram.Assistants;
using Microsoft.Xna.Framework;

public partial class Game1
{
    private void AddNode(Vector2 position)
    {
        ExecuteUndoableChange(() =>
        {
            var node = new DiagramNode
            {
                Id = _nextNodeId++,
                Label = $"状態{_nextNodeId - 1}",
                Position = SnapToHalfGrid(position),
                RadiusUnits = DiagramNode.DefaultRadiusUnits,
                ColorIndex = (_nextNodeId - 2) % _boardTheme.NormalNodePalette.Length
            };
            _nodes.Add(node);
            _selectedNode = node;
            _selectedTransition = null;
        });
        _status = "状態を追加しました。F2・Enterでラベルを編集できます。";
    }
    private void AddEndMarker(Vector2 position)
    {
        ExecuteUndoableChange(() =>
        {
            var node = new DiagramNode
            {
                Id = _nextNodeId++,
                Label = "終了",
                Position = SnapToHalfGrid(position),
                RadiusUnits = DiagramNode.TerminalRadiusUnits,
                ColorIndex = 0,
                Kind = NodeKind.EndMarker
            };
            _nodes.Add(node);
            _selectedNode = node;
            _selectedTransition = null;
        });
        _status = "終了マークを追加しました。必要なら状態から終了へ遷移をつなげます。";
    }

    private void HandleStartMarkerShortcut(Vector2 position)
    {
        var startMarker = _nodes.FirstOrDefault(node => node.Kind == NodeKind.StartMarker);
        if (startMarker is null)
        {
            AddStartMarker(position);
            return;
        }

        CenterViewOnWorldPosition(startMarker.Position);
        _selectedNode = startMarker;
        _selectedTransition = null;
        _status = "開始マークが画面中央に来るよう表示位置を移動しました。";
    }

    private void AddStartMarker(Vector2 position)
    {
        ExecuteUndoableChange(() =>
        {
            if (_nodes.Any(node => node.Kind == NodeKind.StartMarker))
            {
                return;
            }

            var node = new DiagramNode
            {
                Id = _nextNodeId++,
                Label = "開始",
                Position = SnapToHalfGrid(position),
                RadiusUnits = DiagramNode.TerminalRadiusUnits,
                ColorIndex = 0,
                Kind = NodeKind.StartMarker
            };
            _nodes.Add(node);
            _selectedNode = node;
            _selectedTransition = null;
        });
        _status = "開始マークを追加しました。Sで開始マークへ戻れます。";
    }

    private void CenterViewOnWorldPosition(Vector2 worldPosition)
    {
        var viewport = GraphicsDevice.Viewport;
        var screenCenter = new Vector2(viewport.Width / 2f, viewport.Height / 2f);
        _cameraOffset = screenCenter - worldPosition * _cameraZoom;
        _isPanning = false;
    }

    private void AddTransition(int sourceId, int targetId, IReadOnlyList<Vector2>? waypoints = null)
    {
        var source = FindNode(sourceId);
        var target = FindNode(targetId);
        if (source is not null && !CanStartTransitionFrom(source))
        {
            _status = GetCannotStartTransitionStatus(source);
            return;
        }

        if (target is not null && !CanEndTransitionAt(target))
        {
            _status = "開始マークは出発専用です。開始マークへ遷移は伸ばせません。";
            return;
        }

        if (source?.Kind == NodeKind.StartMarker && target?.Kind == NodeKind.EndMarker)
        {
            _status = "開始マークから終了マークへ直接はつなげません。先にNで状態を追加してください。";
            return;
        }

        if (HasTransition(sourceId, targetId))
        {
            _status = "同じ向きの遷移は既にあります。";
            return;
        }
        ExecuteUndoableChange(() =>
        {
            var transition = new DiagramTransition
            {
                SourceId = sourceId,
                TargetId = targetId,
                Waypoints = waypoints?.ToList() ?? new List<Vector2>()
            };
            InitializeTransitionEndpoints(transition);
            if (!CanTransitionHaveEvent(transition))
            {
                transition.Label = string.Empty;
            }
            _transitions.Add(transition);
        });
    }
    private void DeleteSelection()
    {
        if (_selectedNode is not null)
        {
            var node = _selectedNode;
            ExecuteUndoableChange(() =>
            {
                var id = node.Id;
                RemoveSubstateDiagramTree(node.SubstateDiagramId);
                _nodes.Remove(node);
                _transitions.RemoveAll(t => t.SourceId == id || t.TargetId == id);
            });
            _status = node.Kind switch
            {
                NodeKind.StartMarker => "ユカイラーク: 開始マークを削除したんですね？",
                NodeKind.EndMarker => "選択中の終了マークを削除しました。",
                _ => "選択中の状態を削除しました。"
            };
            if (node.Kind == NodeKind.StartMarker)
            {
                _yukaiLarkAssistant.NotifyAssistCompleted(YukaiLarkAssistKind.DeleteStartMarker);
            }
            _selectedNode = null;
            return;
        }
        if (_selectedTransition is not null)
        {
            var transition = _selectedTransition;
            ExecuteUndoableChange(() => _transitions.Remove(transition));
            _selectedTransition = null;
            _status = "選択中の遷移を削除しました。";
        }
    }
    private DiagramNode? FindNode(int id) => _nodes.FirstOrDefault(n => n.Id == id);

    private bool IsInactiveDuringTransitionLink(DiagramNode node)
    {
        if (_invalidLinkSource is not null)
        {
            return node == _invalidLinkSource;
        }

        return _linkSource is not null
            && node != _linkSource
            && (!CanEndTransitionAt(node) || HasTransition(_linkSource.Id, node.Id));
    }

    private bool HasTransition(int sourceId, int targetId)
        => _transitions.Any(t => t.SourceId == sourceId && t.TargetId == targetId);

    private bool CanTransitionHaveEvent(DiagramTransition transition)
    {
        var source = FindNode(transition.SourceId);
        var target = FindNode(transition.TargetId);
        return source?.Kind != NodeKind.StartMarker || target?.Kind != NodeKind.Normal;
    }

    private bool CanStartTransitionFrom(DiagramNode node)
        => node.Kind != NodeKind.EndMarker
            && !HasStartMarkerOutgoingTransition(node);

    private bool HasStartMarkerOutgoingTransition(DiagramNode node)
        => node.Kind == NodeKind.StartMarker && HasOutgoingTransition(node);

    private bool HasOutgoingTransition(DiagramNode node)
        => _transitions.Any(transition => transition.SourceId == node.Id);

    private string GetCannotStartTransitionStatus(DiagramNode node)
        => node.Kind switch
        {
            NodeKind.EndMarker => "終了マークは到着専用です。終了マークから遷移は伸ばせません。",
            NodeKind.StartMarker => "開始マークから出る遷移は1本だけです。2本目は作成できません。",
            _ => "この状態からは遷移を伸ばせません。"
        };

    private static bool CanEndTransitionAt(DiagramNode node)
        => node.Kind != NodeKind.StartMarker;
}