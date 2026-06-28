namespace YukaiLarkStateTransitionDiagram;

using System;
using System.Collections.Generic;
using System.Linq;
using YukaiLarkStateTransitionDiagram.Assistants;
using YukaiLarkStateTransitionDiagram.MiniMap;
using YukaiLarkStateTransitionDiagram.Navigation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

public partial class Game1
{
    private const float MouseWheelZoomFactor = 1.12f;

    private MouseState _previousMouse;
    private DiagramNode? _draggedNode;
    private DiagramTransition? _draggedHandleTransition;
    private TransitionHandleKind _draggedHandleKind;
    private int _draggedWaypointIndex = -1;
    private DiagramTransition? _selectedWaypointTransition;
    private int _selectedWaypointIndex = -1;
    private DiagramTransition? _draggedLabelTransition;
    private Vector2 _labelDragOffset;
    private Vector2 _labelDragStartMouse;
    private bool _labelDragPlacementChanged;
    private readonly List<TransitionNodeDragSnapshot> _draggedNodeTransitionSnapshots = new();
    private DiagramNode? _resizedNode;
    private Vector2 _dragOffset;
    private Vector2 _panStartMouse;
    private Vector2 _panStartCamera;
    private bool _panMoved;
    private MouseCursor? _currentMouseCursor;
    private bool _isMiniMapDragging;
    private bool _isPanning;
    private readonly List<Vector2> _linkWaypoints = new();

    private void HandleMouse(KeyboardState keyboard, MouseState mouse)
    {
        var screenMousePosition = mouse.Position.ToVector2();
        var mousePosition = ScreenToWorld(screenMousePosition);
        var leftPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;
        var leftReleased = mouse.LeftButton == ButtonState.Released && _previousMouse.LeftButton == ButtonState.Pressed;
        var shiftDown = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
        var snapNodes = !IsAltDown(keyboard);

        if (_isMiniMapDragging)
        {
            if (mouse.LeftButton == ButtonState.Pressed)
            {
                CenterViewFromMiniMap(screenMousePosition);
                _status = "ミニマップから表示位置を移動中です。";
            }

            if (leftReleased)
            {
                _isMiniMapDragging = false;
                _status = "ミニマップから表示位置を移動しました。";
            }

            return;
        }
        if (TryHandleMouseWheelZoom(mouse))
        {
            return;
        }
        if (leftPressed)
        {
            if (_linkSource is not null && !shiftDown)
            {
                if (TryContinueTransitionLink(mousePosition))
                {
                    return;
                }
            }

            if (TryBeginMiniMapDrag(screenMousePosition))
            {
                return;
            }
            if (GetThemeButtonRectangle(GraphicsDevice.Viewport).Contains(mouse.Position))
            {
                OpenThemeMenu();
                return;
            }

            if (HeaderRenderer.GetTitleHitBounds(GraphicsDevice.Viewport).Contains(mouse.Position))
            {
                BeginFileNameEdit();
                return;
            }

            if (_yukaiLarkAssistant.ShouldSuppressFromMouse(CreateAssistantContext(), mouse.Position, out var suppressedAssistKind))
            {
                SuppressYukaiLarkAssist(suppressedAssistKind);
                return;
            }

            if (_yukaiLarkAssistant.ShouldRunFromMouse(CreateAssistantContext(), mouse.Position, out var assistKind))
            {
                RunYukaiLarkAssist(assistKind);
                return;
            }

            _isPanning = false;
            if (shiftDown && TryAddWaypointToExistingTransition(mousePosition))
            {
                return;
            }

            _resizedNode = FindNodeResizeHandleAt(mousePosition);
            if (_resizedNode is not null)
            {
                _selectedNode = _resizedNode;
                _selectedTransition = null;
                _draggedNode = null;
                _draggedHandleTransition = null;
                _draggedHandleKind = TransitionHandleKind.None;
                BeginPendingHistory();
                UpdateNodeRadius(_resizedNode, mousePosition);
                _status = "状態サイズを変更中です。半グリッド単位に吸着します。";
                return;
            }

            var handle = FindTransitionHandleAt(mousePosition);
            if (handle.Transition is not null)
            {
                _selectedNode = null;
                _selectedTransition = handle.Transition;
                _draggedNode = null;
                _draggedHandleTransition = handle.Transition;
                _draggedHandleKind = handle.Kind;
                _draggedWaypointIndex = handle.WaypointIndex;
                _selectedWaypointTransition = handle.Kind == TransitionHandleKind.Waypoint ? handle.Transition : null;
                _selectedWaypointIndex = handle.Kind == TransitionHandleKind.Waypoint ? handle.WaypointIndex : -1;
                BeginPendingHistory();
                UpdateTransitionHandle(handle.Transition, handle.Kind, mousePosition, handle.WaypointIndex);
                _status = handle.Kind switch
                {
                    TransitionHandleKind.SourceEndpoint or TransitionHandleKind.TargetEndpoint => "遷移の接点を円周上で移動中です。",
                    TransitionHandleKind.Waypoint => "遷移の中間点を移動中です。Deleteで削除できます。",
                    TransitionHandleKind.SegmentControlPoint1 or TransitionHandleKind.SegmentControlPoint2 => "セグメントの曲がり具合を調整中です。",
                    _ => "遷移の曲がり方を調整中です。"
                };
                return;
            }

            var labelTransition = FindTransitionLabelAt(mousePosition);
            if (labelTransition is not null)
            {
                BeginTransitionLabelDrag(labelTransition, mousePosition);
                return;
            }

            var node = FindNodeAt(mousePosition);
            var transition = node is null ? FindTransitionAt(mousePosition) : null;
            if (node is not null || transition is not null)
            {
                _selectedNode = node;
                _selectedTransition = transition;
                _selectedWaypointTransition = null;
                _selectedWaypointIndex = -1;
            }
            if (transition is not null)
            {
                _status = CanTransitionHaveEvent(transition)
                    ? "遷移を選択しました。F2・Enterでラベル編集、ラベルはドラッグで移動、Deleteで削除できます。"
                    : "開始マークから最初の状態へ入る遷移にはイベントを付けられません。";
            }
            if (shiftDown && node is not null)
            {
                if (!CanStartTransitionFrom(node))
                {
                    _linkSource = null;
                    _linkWaypoints.Clear();
                    _invalidLinkSource = node;
                    _status = GetCannotStartTransitionStatus(node);
                    return;
                }

                _invalidLinkSource = null;
                _linkSource = node;
                _linkWaypoints.Clear();
                _status = "遷移を作成中です。空白クリックで中間点、接続先でクリックまたはマウスを離すと確定します。";
                return;
            }
            if (node is not null)
            {
                _draggedNode = node;
                _dragOffset = mousePosition - node.Position;
                CaptureDraggedNodeTransitionSnapshots(node);
                BeginPendingHistory();
                _status = "状態を選択しました。F2・Enterでラベル編集、Cで色変更、Tでテーマ選択。";
            }
            else if (transition is null)
            {
                _isPanning = true;
                _panMoved = false;
                _panStartMouse = screenMousePosition;
                _panStartCamera = _cameraOffset;
                _linkSource = null;
                _linkWaypoints.Clear();
                _status = "表示位置を移動中です。マウスを離すと停止します。";
            }
        }
        if (_draggedLabelTransition is not null && mouse.LeftButton == ButtonState.Pressed)
        {
            if (_labelDragPlacementChanged || Vector2.DistanceSquared(mousePosition, _labelDragStartMouse) > 9f)
            {
                UpdateTransitionLabelPlacement(_draggedLabelTransition, mousePosition - _labelDragOffset);
                _labelDragPlacementChanged = true;
            }
        }
        if (_draggedHandleTransition is not null && mouse.LeftButton == ButtonState.Pressed)
        {
            UpdateTransitionHandle(_draggedHandleTransition, _draggedHandleKind, mousePosition, _draggedWaypointIndex);
        }
        if (_draggedNode is not null && mouse.LeftButton == ButtonState.Pressed)
        {
            var position = mousePosition - _dragOffset;
            _draggedNode.Position = snapNodes ? SnapToHalfGrid(position) : position;
            UpdateDraggedNodeTransitions(_draggedNode);
        }
        if (_resizedNode is not null && mouse.LeftButton == ButtonState.Pressed)
        {
            UpdateNodeRadius(_resizedNode, mousePosition);
        }
        if (_isPanning && mouse.LeftButton == ButtonState.Pressed)
        {
            _cameraOffset = _panStartCamera + screenMousePosition - _panStartMouse;
            _panMoved = _panMoved || (screenMousePosition - _panStartMouse).LengthSquared() > 9f;
        }
        if (leftReleased)
        {
            if (_linkSource is not null)
            {
                var target = FindNodeAt(mousePosition);
                if (target is not null && (target != _linkSource || _linkWaypoints.Count > 0))
                {
                    var transitionCount = _transitions.Count;
                    AddTransition(_linkSource.Id, target.Id, _linkWaypoints);
                    if (_transitions.Count > transitionCount)
                    {
                        _selectedNode = null;
                        _selectedTransition = _transitions.LastOrDefault();
                        _status = _selectedTransition is not null && CanTransitionHaveEvent(_selectedTransition)
                            ? "遷移を作成しました。ハンドルで形を調整、F2・Enterでラベル編集。"
                            : "開始マークから最初の状態へ入る遷移を作成しました。この遷移にはイベントを付けられません。";
                    }
                }
                if (target is not null && (target != _linkSource || _linkWaypoints.Count > 0))
                {
                    _linkSource = null;
                    _linkWaypoints.Clear();
                }
            }
            _invalidLinkSource = null;
            if (_draggedLabelTransition is not null)
            {
                CommitPendingHistory();
                _status = _labelDragPlacementChanged
                    ? "遷移ラベルの位置を更新しました。Ctrl+Sで保存できます。"
                    : "遷移を選択しました。ラベルはドラッグで移動、F2・Enterで編集できます。";
            }
            if (_draggedHandleTransition is not null)
            {
                CommitPendingHistory();
                _status = "遷移の形を更新しました。Ctrl+Sで保存できます。";
            }
            if (_draggedNode is not null)
            {
                CommitPendingHistory();
                _status = snapNodes
                    ? "状態を移動しました。中心は半グリッドに吸着しています。"
                    : "状態を移動しました。Alt中は吸着しません。";
            }
            if (_resizedNode is not null)
            {
                CommitPendingHistory();
                _status = $"状態サイズを{_resizedNode.RadiusUnits}単位にしました。Ctrl+Sで保存できます。";
            }
            _draggedNode = null;
            _draggedNodeTransitionSnapshots.Clear();
            _resizedNode = null;
            _draggedHandleTransition = null;
            _draggedHandleKind = TransitionHandleKind.None;
            _draggedWaypointIndex = -1;
            _draggedLabelTransition = null;
            if (_isPanning)
            {
                _isPanning = false;
                if (!_panMoved)
                {
                    _selectedNode = null;
                    _selectedTransition = null;
                    _status = "選択を解除しました。";
                }
                else
                {
                    _status = "表示位置を移動しました。空白をドラッグするとまた移動できます。";
                }
                _panMoved = false;
            }
        }
    }
    private bool TryBeginMiniMapDrag(Vector2 screenMousePosition)
    {
        if (!TryGetMiniMapBounds(GraphicsDevice.Viewport, out var bounds)
            || !bounds.Contains(screenMousePosition))
        {
            return false;
        }

        _isMiniMapDragging = true;
        CenterViewFromMiniMap(screenMousePosition);
        _status = "ミニマップから表示位置を移動中です。";
        return true;
    }

    private void CenterViewFromMiniMap(Vector2 screenMousePosition)
    {
        if (!TryGetMiniMapBounds(GraphicsDevice.Viewport, out var bounds))
        {
            return;
        }

        var layout = MiniMapLayout.Create(bounds, _nodes, GraphicsDevice.Viewport, _cameraOffset, _cameraZoom);
        CenterViewOnWorldPosition(layout.MapToWorld(screenMousePosition));
    }


    private bool TryAddWaypointToExistingTransition(Vector2 mousePosition)
    {
        var transition = _selectedTransition;
        if (transition is null || FindNodeAt(mousePosition) is not null)
        {
            return false;
        }

        const float hitDistance = 12f;
        var waypoint = SnapToHalfGrid(mousePosition);
        var insertIndex = 0;

        if (transition.Waypoints.Count > 0)
        {
            if (!TryGetTransitionPath(transition, out var points) || DistanceToTransitionPath(mousePosition, points, transition.SegmentControls) > hitDistance)
            {
                return false;
            }
            insertIndex = FindNearestTransitionPathInsertIndex(mousePosition, points, transition.SegmentControls);
        }
        else
        {
            if (!TryGetTransitionGeometry(transition, out var start, out var control1, out var control2, out var end)
                || DistanceToBezier(mousePosition, start, control1, control2, end) > hitDistance)
            {
                return false;
            }
        }

        ExecuteUndoableChange(() =>
        {
            transition.Waypoints.Insert(insertIndex, waypoint);
            transition.SegmentControls.Insert(insertIndex, new TransitionSegmentControls());
            UpdateTransitionWaypointEndpointAngles(transition);
        });
        _selectedNode = null;
        _selectedTransition = transition;
        _selectedWaypointTransition = transition;
        _selectedWaypointIndex = insertIndex;
        _status = "選択中の遷移に中間点を追加しました。中間点はドラッグ移動、Deleteで削除できます。";
        return true;
    }

    private static int FindNearestTransitionPathInsertIndex(Vector2 point, IReadOnlyList<Vector2> points, IReadOnlyList<TransitionSegmentControls>? segmentControls)
    {
        var bestSegmentIndex = 0;
        var bestDistance = float.MaxValue;
        for (var i = 0; i < points.Count - 1; i++)
        {
            GetTransitionPathSegmentControlPoints(points, i, segmentControls, out var control1, out var control2);
            var distance = DistanceToBezier(point, points[i], control1, control2, points[i + 1]);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestSegmentIndex = i;
            }
        }

        return Math.Clamp(bestSegmentIndex, 0, Math.Max(0, points.Count - 2));
    }

    private bool TryContinueTransitionLink(Vector2 mousePosition)
    {
        var target = FindNodeAt(mousePosition);
        if (target is not null)
        {
            if (target == _linkSource && _linkWaypoints.Count == 0)
            {
                _status = "空白クリックで中間点を追加するか、別の状態をクリックして遷移を確定してください。";
                return true;
            }

            var transitionCount = _transitions.Count;
            AddTransition(_linkSource!.Id, target.Id, _linkWaypoints);
            if (_transitions.Count > transitionCount)
            {
                _selectedNode = null;
                _selectedTransition = _transitions.LastOrDefault();
                _status = _selectedTransition is not null && CanTransitionHaveEvent(_selectedTransition)
                    ? "中間点つきの遷移を作成しました。中間点はドラッグ移動、Deleteで削除できます。"
                    : "開始マークから最初の状態へ入る遷移を作成しました。この遷移にはイベントを付けられません。";
            }
            _linkSource = null;
            _linkWaypoints.Clear();
            return true;
        }

        _linkWaypoints.Add(SnapToHalfGrid(mousePosition));
        _status = $"遷移の中間点を追加しました（{_linkWaypoints.Count}個）。接続先の状態をクリックすると確定します。";
        return true;
    }

    private bool TryDeleteSelectedWaypoint()
    {
        if (_selectedWaypointTransition is null
            || _selectedWaypointIndex < 0
            || _selectedWaypointIndex >= _selectedWaypointTransition.Waypoints.Count)
        {
            return false;
        }

        var transition = _selectedWaypointTransition;
        var index = _selectedWaypointIndex;
        ExecuteUndoableChange(() =>
        {
            transition.Waypoints.RemoveAt(index);
            if (index < transition.SegmentControls.Count)
            {
                transition.SegmentControls.RemoveAt(index);
            }
        });
        _selectedWaypointTransition = null;
        _selectedWaypointIndex = -1;
        _selectedTransition = transition;
        _selectedNode = null;
        _status = "遷移の中間点を削除しました。Ctrl+Sで保存できます。";
        return true;
    }

    private bool TryHandleMouseWheelZoom(MouseState mouse)
    {
        var wheelDelta = mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;
        if (wheelDelta == 0)
        {
            return false;
        }

        if (_draggedNode is not null
            || _resizedNode is not null
            || _draggedHandleTransition is not null
            || _draggedLabelTransition is not null
            || _linkSource is not null
            || _isPanning
            || _isMiniMapDragging)
        {
            return false;
        }

        var screenPosition = mouse.Position.ToVector2();
        var worldBeforeZoom = ScreenToWorld(screenPosition);
        var wheelSteps = wheelDelta / 120f;
        var zoomFactor = MathF.Pow(MouseWheelZoomFactor, wheelSteps);
        _cameraZoom = MathHelper.Clamp(_cameraZoom * zoomFactor, MinCameraZoom, MaxCameraZoom);
        _cameraOffset = screenPosition - worldBeforeZoom * _cameraZoom;
        _status = $"表示倍率: {MathF.Round(_cameraZoom * 100f)}%。マウス位置を中心に拡大縮小しました。";
        return true;
    }

    private void CaptureDraggedNodeTransitionSnapshots(DiagramNode node)
    {
        _draggedNodeTransitionSnapshots.Clear();
        foreach (var transition in _transitions.Where(t => t.SourceId == node.Id || t.TargetId == node.Id))
        {
            InitializeTransitionEndpoints(transition);
            var source = FindNode(transition.SourceId);
            var target = FindNode(transition.TargetId);
            if (source is null || target is null)
            {
                continue;
            }

            TryGetTransitionGeometry(transition, out _, out var control1, out var control2, out _);
            var selfLoop = transition.SourceId == transition.TargetId;
            _draggedNodeTransitionSnapshots.Add(new TransitionNodeDragSnapshot(
                transition,
                source.Position,
                target.Position,
                transition.SourceAngle ?? 0f,
                transition.TargetAngle ?? 0f,
                selfLoop ? 0f : AngleFromTo(source.Position, target.Position),
                selfLoop ? 0f : AngleFromTo(target.Position, source.Position),
                transition.ControlPoint1.HasValue,
                transition.ControlPoint2.HasValue,
                control1 - source.Position,
                control2 - target.Position,
                transition.Waypoints.Select(waypoint => waypoint - node.Position).ToList(),
                transition.Waypoints.ToList(),
                transition.SegmentControls
                    .Select(controls => new TransitionSegmentControlDragSnapshot(
                        controls.ControlPoint1.HasValue,
                        controls.ControlPoint2.HasValue,
                        controls.ControlPoint1 ?? Vector2.Zero,
                        controls.ControlPoint2 ?? Vector2.Zero))
                    .ToList()));
        }
    }

    private void UpdateDraggedNodeTransitions(DiagramNode draggedNode)
    {
        foreach (var snapshot in _draggedNodeTransitionSnapshots)
        {
            var source = FindNode(snapshot.Transition.SourceId);
            var target = FindNode(snapshot.Transition.TargetId);
            if (source is null || target is null)
            {
                continue;
            }

            if (snapshot.Transition.SourceId == snapshot.Transition.TargetId)
            {
                var delta = draggedNode.Position - snapshot.SourcePosition;
                snapshot.Transition.SourceAngle = snapshot.SourceAngle;
                snapshot.Transition.TargetAngle = snapshot.TargetAngle;
                if (snapshot.HasControlPoint1)
                {
                    snapshot.Transition.ControlPoint1 = snapshot.SourcePosition + snapshot.ControlPoint1Offset + delta;
                }
                if (snapshot.HasControlPoint2)
                {
                    snapshot.Transition.ControlPoint2 = snapshot.TargetPosition + snapshot.ControlPoint2Offset + delta;
                }
                for (var i = 0; i < snapshot.Transition.Waypoints.Count && i < snapshot.WaypointOffsets.Count; i++)
                {
                    snapshot.Transition.Waypoints[i] = draggedNode.Position + snapshot.WaypointOffsets[i];
                }
                MoveTransitionSegmentControlPoints(snapshot, delta, delta);
                continue;
            }

            var sourceDelta = MathHelper.WrapAngle(AngleFromTo(source.Position, target.Position) - snapshot.SourceRelationAngle);
            var targetDelta = MathHelper.WrapAngle(AngleFromTo(target.Position, source.Position) - snapshot.TargetRelationAngle);
            snapshot.Transition.SourceAngle = MathHelper.WrapAngle(snapshot.SourceAngle + sourceDelta);
            snapshot.Transition.TargetAngle = MathHelper.WrapAngle(snapshot.TargetAngle + targetDelta);
            if (snapshot.HasControlPoint1)
            {
                snapshot.Transition.ControlPoint1 = source.Position + Rotate(snapshot.ControlPoint1Offset, sourceDelta);
            }
            if (snapshot.HasControlPoint2)
            {
                snapshot.Transition.ControlPoint2 = target.Position + Rotate(snapshot.ControlPoint2Offset, targetDelta);
            }
            var sourceMove = source.Position - snapshot.SourcePosition;
            var targetMove = target.Position - snapshot.TargetPosition;
            MoveTransitionWaypoints(snapshot, sourceMove, targetMove);
            MoveTransitionSegmentControlPoints(snapshot, sourceMove, targetMove);
        }
    }

    private static void MoveTransitionWaypoints(TransitionNodeDragSnapshot snapshot, Vector2 sourceMove, Vector2 targetMove)
    {
        var transition = snapshot.Transition;
        var waypointCount = Math.Min(transition.Waypoints.Count, snapshot.WaypointPositions.Count);
        for (var i = 0; i < waypointCount; i++)
        {
            var t = (i + 1f) / (waypointCount + 1f);
            transition.Waypoints[i] = snapshot.WaypointPositions[i] + Vector2.Lerp(sourceMove, targetMove, t);
        }
    }

    private static void MoveTransitionSegmentControlPoints(TransitionNodeDragSnapshot snapshot, Vector2 sourceMove, Vector2 targetMove)
    {
        var transition = snapshot.Transition;
        var segmentCount = Math.Min(transition.SegmentControls.Count, snapshot.SegmentControlSnapshots.Count);
        if (segmentCount == 0)
        {
            return;
        }

        for (var i = 0; i < segmentCount; i++)
        {
            var controls = transition.SegmentControls[i];
            var snapshotControls = snapshot.SegmentControlSnapshots[i];
            if (snapshotControls.HasControlPoint1)
            {
                var t = (i + 1f / 3f) / segmentCount;
                controls.ControlPoint1 = snapshotControls.ControlPoint1 + Vector2.Lerp(sourceMove, targetMove, t);
            }
            if (snapshotControls.HasControlPoint2)
            {
                var t = (i + 2f / 3f) / segmentCount;
                controls.ControlPoint2 = snapshotControls.ControlPoint2 + Vector2.Lerp(sourceMove, targetMove, t);
            }
        }
    }
    private void UpdateMouseCursor(KeyboardState keyboard, MouseState mouse)
    {
        var cursor = GetMouseCursor(keyboard, mouse);
        if (ReferenceEquals(_currentMouseCursor, cursor))
        {
            return;
        }

        Mouse.SetCursor(cursor);
        _currentMouseCursor = cursor;
    }

    private MouseCursor GetMouseCursor(KeyboardState keyboard, MouseState mouse)
    {
        if (_isPanning || _isMiniMapDragging)
        {
            return MouseCursor.SizeAll;
        }

        if (_isFileMenuOpen || _isThemeMenuOpen || _isColorPaletteOpen || _isExportSelecting || IsEditingLabel || _isEditingFileName)
        {
            return MouseCursor.Arrow;
        }

        if (IsMouseOverMiniMap(mouse) || CanPanFromMousePosition(keyboard, mouse))
        {
            return MouseCursor.Hand;
        }

        return MouseCursor.Arrow;
    }

    private bool IsMouseOverMiniMap(MouseState mouse)
        => TryGetMiniMapBounds(GraphicsDevice.Viewport, out var bounds)
            && bounds.Contains(mouse.Position);

    private bool CanPanFromMousePosition(KeyboardState keyboard, MouseState mouse)
    {
        if (mouse.LeftButton == ButtonState.Pressed
            || _draggedNode is not null
            || _resizedNode is not null
            || _draggedHandleTransition is not null
            || _draggedLabelTransition is not null
            || _linkSource is not null
            || IsShiftDown(keyboard))
        {
            return false;
        }

        var mousePosition = ScreenToWorld(mouse.Position.ToVector2());
        return FindNodeAt(mousePosition) is null
            && FindTransitionAt(mousePosition) is null
            && FindTransitionHandleAt(mousePosition).Transition is null;
    }

    private DiagramNode? FindNodeAt(Vector2 position)
    {
        for (var i = _nodes.Count - 1; i >= 0; i--)
        {
            if (Vector2.Distance(_nodes[i].Position, position) <= _nodes[i].Radius)
            {
                return _nodes[i];
            }
        }
        return null;
    }
    private DiagramNode? FindNodeResizeHandleAt(Vector2 position)
    {
        if (_selectedNode is null)
        {
            return null;
        }
        return Vector2.Distance(position, GetNodeResizeHandleCenter(_selectedNode)) <= 14f ? _selectedNode : null;
    }

    private static Vector2 GetNodeResizeHandleCenter(DiagramNode node)
        => node.Position + new Vector2(node.Radius, node.Radius);

    private void UpdateNodeRadius(DiagramNode node, Vector2 mousePosition)
    {
        var offset = mousePosition - node.Position;
        var radius = MathF.Max(MathF.Abs(offset.X), MathF.Abs(offset.Y));
        node.RadiusUnits = (int)MathF.Round(radius / DiagramNode.RadiusUnit);
    }

    private DiagramTransition? FindTransitionLabelAt(Vector2 position)
    {
        foreach (var transition in Enumerable.Reverse(_transitions))
        {
            if (!CanTransitionHaveEvent(transition) || string.IsNullOrWhiteSpace(transition.Label))
            {
                continue;
            }

            if (!TryGetTransitionLabelBounds(transition, out var topLeft, out var size))
            {
                continue;
            }

            const float padding = 6f;
            if (position.X >= topLeft.X - padding
                && position.X <= topLeft.X + size.X + padding
                && position.Y >= topLeft.Y - padding
                && position.Y <= topLeft.Y + size.Y + padding)
            {
                return transition;
            }
        }

        return null;
    }

    private bool TryGetTransitionLabelBounds(DiagramTransition transition, out Vector2 topLeft, out Vector2 size)
    {
        topLeft = Vector2.Zero;
        size = Vector2.Zero;
        if (string.IsNullOrWhiteSpace(transition.Label))
        {
            return false;
        }

        var texture = GetLabelTexture(transition.Label, false);
        Vector2 center;
        if (transition.Waypoints.Count > 0)
        {
            if (!TryGetTransitionPath(transition, out var points))
            {
                return false;
            }
            center = GetTransitionPathLabelCenter(points, transition, texture.Width, texture.Height);
        }
        else
        {
            if (!TryGetTransitionGeometry(transition, out var start, out var control1, out var control2, out var end))
            {
                return false;
            }
            center = EdgeRenderer.GetTransitionLabelCenter(start, control1, control2, end, transition, texture.Width, texture.Height);
        }
        size = new Vector2(texture.Width, texture.Height);
        topLeft = center - size / 2f;
        return true;
    }

    private void BeginTransitionLabelDrag(DiagramTransition transition, Vector2 mousePosition)
    {
        if (!TryGetTransitionLabelBounds(transition, out var topLeft, out var size))
        {
            return;
        }

        _selectedNode = null;
        _selectedTransition = transition;
        _draggedNode = null;
        _draggedHandleTransition = null;
        _draggedHandleKind = TransitionHandleKind.None;
        _draggedLabelTransition = transition;
        _labelDragOffset = mousePosition - (topLeft + size / 2f);
        _labelDragStartMouse = mousePosition;
        _labelDragPlacementChanged = false;
        BeginPendingHistory();
        _status = "遷移ラベルを移動中です。線上の最寄り位置へ細い接続線を付けます。";
    }

    private void UpdateTransitionLabelPlacement(DiagramTransition transition, Vector2 labelCenter)
    {
        float anchorT;
        Vector2 anchor;
        if (transition.Waypoints.Count > 0)
        {
            if (!TryGetTransitionPath(transition, out var points))
            {
                return;
            }
            anchorT = FindNearestTransitionPathT(labelCenter, points, transition.SegmentControls);
            anchor = GetTransitionPathPoint(points, anchorT, transition.SegmentControls);
        }
        else
        {
            if (!TryGetTransitionGeometry(transition, out var start, out var control1, out var control2, out var end))
            {
                return;
            }
            anchorT = FindNearestTransitionT(labelCenter, start, control1, control2, end);
            anchor = EdgeRenderer.GetTransitionPoint(start, control1, control2, end, anchorT);
        }
        transition.LabelAnchorT = anchorT;
        transition.LabelOffset = labelCenter - anchor;
    }

    private DiagramTransition? FindTransitionAt(Vector2 position)
    {
        foreach (var transition in _transitions)
        {
            if (transition.Waypoints.Count > 0)
            {
                if (TryGetTransitionPath(transition, out var points) && DistanceToTransitionPath(position, points, transition.SegmentControls) <= 8f)
                {
                    return transition;
                }
                continue;
            }

            if (TryGetTransitionGeometry(transition, out var start, out var control1, out var control2, out var end)
                && DistanceToBezier(position, start, control1, control2, end) <= 8f)
            {
                return transition;
            }
        }
        return null;
    }

    private TransitionHandleHit FindTransitionHandleAt(Vector2 position)
    {
        if (_selectedTransition is not null && TryGetTransitionHandleAt(_selectedTransition, position, out var selectedHit))
        {
            return selectedHit;
        }

        foreach (var transition in Enumerable.Reverse(_transitions))
        {
            if (TryGetTransitionHandleAt(transition, position, out var hit))
            {
                return hit;
            }
        }

        return new TransitionHandleHit(null, TransitionHandleKind.None);
    }

    private bool TryGetTransitionHandleAt(DiagramTransition transition, Vector2 position, out TransitionHandleHit hit)
    {
        hit = new TransitionHandleHit(null, TransitionHandleKind.None);
        if (!TryGetTransitionGeometry(transition, out var start, out var control1, out var control2, out var end))
        {
            return false;
        }

        if (Vector2.Distance(position, start) <= 14f)
        {
            hit = new TransitionHandleHit(transition, TransitionHandleKind.SourceEndpoint);
            return true;
        }

        if (Vector2.Distance(position, end) <= 14f)
        {
            hit = new TransitionHandleHit(transition, TransitionHandleKind.TargetEndpoint);
            return true;
        }

        if (transition.Waypoints.Count > 0 && TryGetTransitionPath(transition, out var points))
        {
            for (var segmentIndex = 0; segmentIndex < points.Count - 1; segmentIndex++)
            {
                GetTransitionPathSegmentControlPoints(points, segmentIndex, transition.SegmentControls, out var segmentControl1, out var segmentControl2);
                if (Vector2.Distance(position, segmentControl1) <= 14f)
                {
                    hit = new TransitionHandleHit(transition, TransitionHandleKind.SegmentControlPoint1, segmentIndex);
                    return true;
                }
                if (Vector2.Distance(position, segmentControl2) <= 14f)
                {
                    hit = new TransitionHandleHit(transition, TransitionHandleKind.SegmentControlPoint2, segmentIndex);
                    return true;
                }
            }

            for (var i = 0; i < transition.Waypoints.Count; i++)
            {
                if (Vector2.Distance(position, transition.Waypoints[i]) <= 14f)
                {
                    hit = new TransitionHandleHit(transition, TransitionHandleKind.Waypoint, i);
                    return true;
                }
            }

            return false;
        }

        if (Vector2.Distance(position, start) <= 14f)
        {
            hit = new TransitionHandleHit(transition, TransitionHandleKind.SourceEndpoint);
            return true;
        }

        if (Vector2.Distance(position, end) <= 14f)
        {
            hit = new TransitionHandleHit(transition, TransitionHandleKind.TargetEndpoint);
            return true;
        }

        if (transition.SourceId != transition.TargetId)
        {
            return false;
        }

        if (Vector2.Distance(position, control1) <= 14f)
        {
            hit = new TransitionHandleHit(transition, TransitionHandleKind.ControlPoint1);
            return true;
        }

        if (Vector2.Distance(position, control2) <= 14f)
        {
            hit = new TransitionHandleHit(transition, TransitionHandleKind.ControlPoint2);
            return true;
        }

        return false;
    }
    private static Vector2 GetTransitionPathLabelCenter(IReadOnlyList<Vector2> points, DiagramTransition transition, int labelWidth, int labelHeight)
    {
        var anchorT = MathHelper.Clamp(transition.LabelAnchorT, 0f, 1f);
        var anchor = GetTransitionPathPoint(points, anchorT, transition.SegmentControls);
        if (transition.LabelOffset.HasValue)
        {
            return anchor + transition.LabelOffset.Value;
        }

        var tangent = GetTransitionPathTangent(points, anchorT, transition.SegmentControls);
        var side = transition.LabelSide == 0 ? -1f : 1f;
        if (MathF.Abs(tangent.X) >= MathF.Abs(tangent.Y))
        {
            return anchor + new Vector2(0, side * (labelHeight / 2f + 18f));
        }

        return anchor + new Vector2(side * (labelWidth / 2f + 22f), 0);
    }

}
