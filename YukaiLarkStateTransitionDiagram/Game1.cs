namespace YukaiLarkStateTransitionDiagram;

using YukaiLarkStateTransitionDiagram.Theme;
using YukaiLarkStateTransitionDiagram.Assistants;
using YukaiLarkStateTransitionDiagram.Navigation;
using YukaiLarkStateTransitionDiagram.MiniMap;
using YukaiLarkStateTransitionDiagram.Persistence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
public partial class Game1 : Game
{
    private const string AppTitle = "YukaiLark State Transition Diagram";
    private const int MiniMapWidth = 266;
    private const int MiniMapHeight = 118;
    private const int MiniMapRightMargin = 12;
    private const int MiniMapBottomMargin = 64;
    private const int MaxFileNameLength = 255;
    private const float MinCameraZoom = 0.25f;
    private const float MaxCameraZoom = 3.0f;
    private const string YukaiLarkMascotTexturePath = "Assets/BrandLogo/yukai-lark-logo.png";
    private readonly GraphicsDeviceManager _graphics;
    private readonly List<DiagramNode> _nodes = new();
    private readonly List<DiagramTransition> _transitions = new();
    private readonly AssistSuppressionSection _assistSuppression = new();
    private readonly Dictionary<string, Texture2D> _labelTextureCache = new();
    private readonly Dictionary<string, Texture2D> _uiTextTextureCache = new();
    private PrimitiveRenderer _primitiveRenderer = null!;
    private EdgeRenderer _edgeRenderer = null!;
    private NodeRenderer _nodeRenderer = null!;
    private HeaderRenderer _headerRenderer = null!;
    private InspectorPanelRenderer _inspectorPanelRenderer = null!;
    private MiniMapRenderer _miniMapRenderer = null!;
    private ShortcutKeyRenderer _shortcutKeyRenderer = null!;
    private SpriteBatch _spriteBatch = null!;
    private Texture2D _pixel = null!;
    private Texture2D? _yukaiLarkMascotTexture;
    private readonly YukaiLarkAssistant _yukaiLarkAssistant = new();
    private readonly TextBoxController _textBoxController = new(24);
    private readonly TextBoxController _fileNameTextBoxController = new(MaxFileNameLength);
    private AppConfig _appConfig = new();

    private DiagramNode? _selectedNode;
    private DiagramTransition? _selectedTransition;
    private DiagramNode? _linkSource;
    private DiagramNode? _invalidLinkSource;
    private DiagramNode? _editingNode;
    private DiagramTransition? _editingTransition;
    private string? _currentFilePath;
    private Vector2 _cameraOffset;
    private float _cameraZoom = 1f;
    private bool _isEditingFileName;
    private int _nextNodeId = 1;
    private string _status = DefaultStatus;
    private string _fileNameEditWarning = string.Empty;
    private const string DefaultStatus = "N: 状態追加 / S: 開始マーク / ホイール: 拡大縮小 / T: テーマ選択 / Shift+ドラッグ: 遷移作成 / F2・Enter: ラベル編集 / Ctrl+Z/Y: 元に戻す/やり直し / Ctrl+S: 保存";
    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 800,
            SynchronizeWithVerticalRetrace = true
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.Title = AppTitle;
        Window.TextInput += OnTextInput;
    }
    protected override void Initialize()
    {
        _appConfig = AppConfigStore.Load();
        ClearDiagram();
        ClearHistory();
        OpenFileMenu(isStartup: true);
        base.Initialize();
    }
    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _primitiveRenderer = new PrimitiveRenderer(_spriteBatch, _pixel);
        _edgeRenderer = new EdgeRenderer(_primitiveRenderer, _spriteBatch, GetLabelTexture, _boardTheme);
        _headerRenderer = new HeaderRenderer(GraphicsDevice, _spriteBatch, _pixel);
        _inspectorPanelRenderer = new InspectorPanelRenderer(GraphicsDevice, _spriteBatch, _pixel, _primitiveRenderer);
        _miniMapRenderer = new MiniMapRenderer(_spriteBatch, _pixel, _primitiveRenderer);
        _shortcutKeyRenderer = new ShortcutKeyRenderer(GraphicsDevice, _spriteBatch, _pixel, _keyCapTheme, _boardTheme);
        _nodeRenderer = new NodeRenderer(_primitiveRenderer, _spriteBatch, Palette, GetLabelTexture, _boardTheme);
        _yukaiLarkMascotTexture = LoadTextureWithTransparentWhite(YukaiLarkMascotTexturePath);
    }
    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        var mouse = Mouse.GetState();
        UpdateExportPhotoEffect(gameTime);

        if (_isFileMenuOpen)
        {
            HandleFileMenuKeyboard(keyboard);
            HandleFileMenuMouse(mouse);
            UpdateMouseCursor(keyboard, mouse);
            _previousKeyboard = keyboard;
            _previousMouse = mouse;
            base.Update(gameTime);
            return;
        }

        if (_isThemeMenuOpen)
        {
            HandleThemeMenuKeyboard(keyboard);
            HandleThemeMenuMouse(mouse);
            UpdateMouseCursor(keyboard, mouse);
            _previousKeyboard = keyboard;
            _previousMouse = mouse;
            base.Update(gameTime);
            return;
        }

        // ［開始マーク作成アシスト］の起動判定
        _status = _yukaiLarkAssistant.Update(gameTime, CreateAssistantContext(), _status, DefaultStatus);

        if (_isExportSelecting)
        {
            HandleExportSelectionKeyboard(keyboard);
            if (_isExportSelecting)
            {
                HandleExportSelectionMouse(keyboard, mouse);
            }
        }
        else if (_isEditingFileName)
        {
            _fileNameTextBoxController.UpdateImeComposition();
            HandleFileNameEditingKeyboard(keyboard);
        }
        else if (IsEditingLabel)
        {
            _textBoxController.UpdateImeComposition();
            HandleLabelEditingKeyboard(keyboard);
        }
        else
        {
            _textBoxController.Clear();
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
            {
                Exit();
            }
            HandleKeyboard(keyboard, mouse);
            HandleMouse(keyboard, mouse);
        }
        UpdateMouseCursor(keyboard, mouse);
        _previousKeyboard = keyboard;
        _previousMouse = mouse;
        base.Update(gameTime);
    }
    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(_boardTheme.BackgroundColor);
        DrawDiagramScene(GetViewMatrix(), includeInteraction: true, gameTime.TotalGameTime);

        _spriteBatch.Begin(samplerState: SamplerState.LinearClamp);
        _headerRenderer.DrawHeader(
            GraphicsDevice.Viewport,
            GetHeaderTitle(),
            _status,
            _boardTheme,
            _isEditingFileName,
            _fileNameTextBoxController.GetDisplayText(),
            _fileNameTextBoxController.GetDisplayCaretIndex(),
            ((int)(gameTime.TotalGameTime.TotalSeconds * 2)) % 2 == 0,
            _fileNameEditWarning);
        DrawThemeButton(GraphicsDevice.Viewport);

        // ［開始マーク作成アシスト］の描画
        DrawYukaiLarkMascot(GraphicsDevice.Viewport, gameTime.TotalGameTime);

        DrawInspectorPanel();
        DrawMiniMapOverlay();
        if (!_isThemeMenuOpen)
        {
            DrawBottomShortcutHelp(gameTime);
        }

        DrawExportSelectionOverlay();
        DrawExportPhotoEffectOverlay();
        DrawThemeMenuOverlay();
        DrawFileMenuOverlay();
        if (_isThemeMenuOpen)
        {
            DrawBottomShortcutHelp(gameTime);
        }
        _spriteBatch.End();
        base.Draw(gameTime);
    }
    protected override void UnloadContent()
    {
        foreach (var texture in _labelTextureCache.Values)
        {
            texture.Dispose();
        }
        _labelTextureCache.Clear();
        foreach (var texture in _uiTextTextureCache.Values)
        {
            texture.Dispose();
        }
        _uiTextTextureCache.Clear();
        _yukaiLarkMascotTexture?.Dispose();
        _yukaiLarkMascotTexture = null;
        _headerRenderer?.Dispose();
        _inspectorPanelRenderer?.Dispose();
        _shortcutKeyRenderer?.Dispose();
        base.UnloadContent();
    }
    private bool IsEditingLabel => _editingNode is not null || _editingTransition is not null;

    private YukaiLarkAssistantContext CreateAssistantContext()
    {
        var missingTransitionEventSummary = GetMissingTransitionEventSummary();
        var normalNodes = _nodes
            .Where(node => node.Kind == NodeKind.Normal)
            .OrderBy(node => node.Id)
            .ToList();
        var startMarker = _nodes.FirstOrDefault(node => node.Kind == NodeKind.StartMarker);
        var endMarker = _nodes.FirstOrDefault(node => node.Kind == NodeKind.EndMarker);
        var normalToEndSource = startMarker is null
            ? normalNodes.LastOrDefault()
            : normalNodes
                .OrderByDescending(node => (node.Position - startMarker.Position).LengthSquared())
                .ThenBy(node => node.Id)
                .FirstOrDefault();
        var hasStartToNormalTransition = startMarker is not null
            && normalNodes.Count >= 1
            && _transitions.Any(t => t.SourceId == startMarker.Id && t.TargetId == normalNodes[0].Id);
        var hasNormalToNormalTransition = normalNodes.Count >= 2
            && _transitions.Any(t => t.SourceId == normalNodes[0].Id && t.TargetId == normalNodes[1].Id);
        var hasNormalToEndTransition = endMarker is not null
            && normalToEndSource is not null
            && (_transitions.Any(t => t.SourceId == normalToEndSource.Id && t.TargetId == endMarker.Id)
                || IsAssistSuggestionSuppressed(
                    AssistSuggestionKind.NormalToEndTransition,
                    normalToEndSource.Id,
                    endMarker.Id));
        var isNormalToEndTransitionSuggestion = endMarker is not null
            && normalToEndSource is not null
            && hasStartToNormalTransition
            && (normalNodes.Count < 2 || hasNormalToNormalTransition)
            && !hasNormalToEndTransition;

        var shouldSuggestShiftDiagramLeft = TryGetDiagramShiftLeftDistance(out var shiftDiagramLeftDistance);
        var hasUnreachedNormalNode = TryGetUnreachedNormalTransitionEndpoints(out _, out _);

        return new YukaiLarkAssistantContext(
            startMarker is not null,
            endMarker is not null,
            normalNodes.Count,
            hasStartToNormalTransition,
            hasNormalToNormalTransition,
            hasNormalToEndTransition,
            isNormalToEndTransitionSuggestion,
            !string.IsNullOrEmpty(missingTransitionEventSummary),
            missingTransitionEventSummary,
            shouldSuggestShiftDiagramLeft,
            shiftDiagramLeftDistance,
            hasUnreachedNormalNode,
            !IsEditingLabel
                && !_isEditingFileName
                && !_isExportSelecting
                && !_isPanning
                && _draggedNode is null
                && _linkSource is null
                && _draggedHandleTransition is null
                && _resizedNode is null);
    }
    private bool TryGetDiagramShiftLeftDistance(out float distance)
    {
        distance = 0f;
        var viewport = GraphicsDevice.Viewport;
        if (_nodes.Count < 2 || viewport.Width < 760)
        {
            return false;
        }

        var minX = float.MaxValue;
        var maxX = float.MinValue;
        foreach (var node in _nodes)
        {
            var screenPosition = WorldToScreen(node.Position);
            var screenRadius = node.Radius * _cameraZoom;
            minX = MathF.Min(minX, screenPosition.X - screenRadius);
            maxX = MathF.Max(maxX, screenPosition.X + screenRadius);
        }

        const float leftComfortPadding = 140f;
        const float rightTriggerPadding = 180f;
        const float desiredRightPadding = 320f;
        const float minShift = 120f;
        const float maxShift = 360f;
        if (maxX < viewport.Width - rightTriggerPadding || minX < leftComfortPadding + minShift)
        {
            return false;
        }

        var shiftForRightSpace = maxX - (viewport.Width - desiredRightPadding);
        var shiftBeforeLeftCrowding = minX - leftComfortPadding;
        var rawShift = MathF.Min(maxShift, MathF.Min(shiftForRightSpace, shiftBeforeLeftCrowding));
        if (rawShift < minShift)
        {
            return false;
        }

        distance = MathF.Floor(rawShift / DiagramNode.RadiusUnit) * DiagramNode.RadiusUnit;
        return distance >= minShift;
    }
    private string GetMissingTransitionEventSummary()
    {
        var transition = _transitions.FirstOrDefault(t => CanTransitionHaveEvent(t) && string.IsNullOrWhiteSpace(t.Label));
        if (transition is null)
        {
            return string.Empty;
        }

        var sourceLabel = GetNodeLabel(transition.SourceId);
        var targetLabel = GetNodeLabel(transition.TargetId);
        return $"{sourceLabel} と {targetLabel}";
    }

    private string GetNodeLabel(int nodeId)
    {
        var node = FindNode(nodeId);
        return node is null || string.IsNullOrWhiteSpace(node.Label) ? $"状態{nodeId}" : node.Label;
    }

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
                ColorIndex = (_nextNodeId - 2) % Palette.Length
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

    private void RunYukaiLarkAssist(YukaiLarkAssistKind kind)
    {
        var context = CreateAssistantContext();
        var result = YukaiLarkAssistOperations.Run(new YukaiLarkAssistOperation
        {
            Kind = kind,
            Viewport = GraphicsDevice.Viewport,
            Nodes = _nodes,
            Transitions = _transitions,
            NextNodeId = _nextNodeId,
            PaletteLength = Palette.Length,
            ScreenToWorld = ScreenToWorld,
            SnapToHalfGrid = SnapToHalfGrid,
            ExecuteUndoableChange = ExecuteUndoableChange,
            InitializeTransitionEndpoints = InitializeTransitionEndpoints,
            GetNodeScreenPosition = _yukaiLarkAssistant.GetNodeScreenPosition,
            ShiftDiagramLeftDistance = context.ShiftDiagramLeftDistance
        });

        _nextNodeId = result.NextNodeId;
        if (kind != YukaiLarkAssistKind.ShiftDiagramLeft)
        {
            _selectedNode = result.SelectedNode;
            _selectedTransition = result.SelectedTransition;
        }
        if (kind == YukaiLarkAssistKind.AddTransitionEvent && result.SelectedTransition is not null)
        {
            BeginTransitionLabelEdit(result.SelectedTransition);
        }

        if (result.Completed)
        {
            _yukaiLarkAssistant.Reset();
            _yukaiLarkAssistant.NotifyAssistCompleted(kind);
        }

        if (!string.IsNullOrEmpty(result.Status))
        {
            _status = result.Status;
        }
    }

    private void SuppressYukaiLarkAssist(YukaiLarkAssistKind kind)
    {
        if (kind != YukaiLarkAssistKind.CreateTransition
            || !TryGetNormalToEndTransitionSuggestion(out var source, out var target))
        {
            _status = "このアシストは抑制できません。";
            return;
        }

        ExecuteUndoableChange(() =>
        {
            if (!IsAssistSuggestionSuppressed(AssistSuggestionKind.NormalToEndTransition, source.Id, target.Id))
            {
                _assistSuppression.SuppressedSuggestions.Add(new AssistSuggestionSuppression
                {
                    Kind = AssistSuggestionKind.NormalToEndTransition,
                    SourceId = source.Id,
                    TargetId = target.Id
                });
            }
        });

        _yukaiLarkAssistant.Reset();
        _status = $"{GetNodeLabel(source.Id)} から終了マークへつなぐ提案を、この図では抑制しました。Ctrl+Sで保存できます。";
    }
    private void AddTransition(int sourceId, int targetId)
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

        if (_transitions.Any(t => t.SourceId == sourceId && t.TargetId == targetId))
        {
            _status = "同じ向きの遷移は既にあります。";
            return;
        }
        ExecuteUndoableChange(() =>
        {
            var transition = new DiagramTransition { SourceId = sourceId, TargetId = targetId };
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
    private void InitializeTransitionEndpoints(DiagramTransition transition)
    {
        var source = FindNode(transition.SourceId);
        var target = FindNode(transition.TargetId);
        if (source is null || target is null)
        {
            return;
        }

        if (transition.SourceId == transition.TargetId)
        {
            transition.SourceAngle ??= -MathHelper.PiOver4;
            transition.TargetAngle ??= MathHelper.PiOver4;
            return;
        }

        transition.SourceAngle ??= AngleFromTo(source.Position, target.Position);
        transition.TargetAngle ??= AngleFromTo(target.Position, source.Position);
    }

    private void UpdateTransitionHandle(DiagramTransition transition, TransitionHandleKind kind, Vector2 mousePosition)
    {
        switch (kind)
        {
            case TransitionHandleKind.SourceEndpoint:
                UpdateTransitionEndpoint(transition, true, mousePosition);
                break;
            case TransitionHandleKind.TargetEndpoint:
                UpdateTransitionEndpoint(transition, false, mousePosition);
                break;
            case TransitionHandleKind.ControlPoint1:
                transition.ControlPoint1 = mousePosition;
                break;
            case TransitionHandleKind.ControlPoint2:
                transition.ControlPoint2 = mousePosition;
                break;
        }
    }

    private void UpdateTransitionEndpoint(DiagramTransition transition, bool isSource, Vector2 mousePosition)
    {
        var node = FindNode(isSource ? transition.SourceId : transition.TargetId);
        if (node is null)
        {
            return;
        }

        var angle = AngleFromTo(node.Position, mousePosition);
        if (isSource)
        {
            transition.SourceAngle = angle;
        }
        else
        {
            transition.TargetAngle = angle;
        }
    }

    /// <summary>
    /// ［遷移］エッジの尻
    /// </summary>
    /// <param name="transition"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    private bool TryGetTransitionEndpoints(DiagramTransition transition, out Vector2 start, out Vector2 end)
    {
        var source = FindNode(transition.SourceId);
        var target = FindNode(transition.TargetId);
        if (source is null || target is null)
        {
            start = Vector2.Zero;
            end = Vector2.Zero;
            return false;
        }

        var sourceAngle = transition.SourceAngle ?? AngleFromTo(source.Position, target.Position);
        var targetAngle = transition.TargetAngle ?? AngleFromTo(target.Position, source.Position);

        // 頭
        start = PointOnCircle(source.Position, source.Radius, sourceAngle);

        // 尻
        end = PointOnCircle(target.Position, target.Radius + TransitionHeadPadding, targetAngle);
        return true;
    }

    private const float TransitionHeadPadding = 12f;

    private bool TryGetTransitionGeometry(DiagramTransition transition, out Vector2 start, out Vector2 control1, out Vector2 control2, out Vector2 end)
    {
        if (!TryGetTransitionEndpoints(transition, out start, out end))
        {
            control1 = Vector2.Zero;
            control2 = Vector2.Zero;
            return false;
        }

        if (transition.SourceId == transition.TargetId)
        {
            var node = FindNode(transition.SourceId);
            if (node is null)
            {
                control1 = Vector2.Zero;
                control2 = Vector2.Zero;
                return false;
            }

            control1 = transition.ControlPoint1 ?? node.Position + new Vector2(node.Radius * 2.5f, -node.Radius * 2.2f);
            control2 = transition.ControlPoint2 ?? node.Position + new Vector2(node.Radius * 2.5f, node.Radius * 2.2f);
            return true;
        }

        var delta = end - start;
        control1 = transition.ControlPoint1 ?? start + delta / 3f;
        control2 = transition.ControlPoint2 ?? start + delta * 2f / 3f;
        return true;
    }

    private static Vector2 PointOnCircle(Vector2 center, float radius, float angle)
        => center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;

    private static float AngleFromTo(Vector2 from, Vector2 to)
        => MathF.Atan2(to.Y - from.Y, to.X - from.X);

    private static Vector2 Rotate(Vector2 vector, float angle)
    {
        var cos = MathF.Cos(angle);
        var sin = MathF.Sin(angle);
        return new Vector2(vector.X * cos - vector.Y * sin, vector.X * sin + vector.Y * cos);
    }
    private DiagramNode? FindNode(int id) => _nodes.FirstOrDefault(n => n.Id == id);
    private Matrix GetViewMatrix()
        => Matrix.CreateScale(_cameraZoom) * Matrix.CreateTranslation(_cameraOffset.X, _cameraOffset.Y, 0f);
    private Vector2 ScreenToWorld(Vector2 screenPosition)
        => (screenPosition - _cameraOffset) / _cameraZoom;
    private Vector2 WorldToScreen(Vector2 worldPosition)
        => worldPosition * _cameraZoom + _cameraOffset;
    private static Vector2 SnapToHalfGrid(Vector2 position)
    {
        const float unit = DiagramNode.RadiusUnit;
        return new Vector2(
            MathF.Round(position.X / unit) * unit,
            MathF.Round(position.Y / unit) * unit);
    }
    private void DrawGrid(int spacing, Color color)
    {
        var topLeft = ScreenToWorld(Vector2.Zero);
        var bottomRight = ScreenToWorld(new Vector2(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height));
        DrawGrid(spacing, color, topLeft, bottomRight);
    }

    private void DrawGrid(int spacing, Color color, Vector2 topLeft, Vector2 bottomRight)
    {
        var startX = (int)MathF.Floor(topLeft.X / spacing) * spacing;
        var endX = (int)MathF.Ceiling(bottomRight.X / spacing) * spacing;
        var startY = (int)MathF.Floor(topLeft.Y / spacing) * spacing;
        var endY = (int)MathF.Ceiling(bottomRight.Y / spacing) * spacing;
        for (var x = startX; x <= endX; x += spacing)
        {
            _primitiveRenderer.DrawLine(new Vector2(x, topLeft.Y), new Vector2(x, bottomRight.Y), color, 1f);
        }
        for (var y = startY; y <= endY; y += spacing)
        {
            _primitiveRenderer.DrawLine(new Vector2(topLeft.X, y), new Vector2(bottomRight.X, y), color, 1f);
        }
    }

    private DiagramNode? GetNodeHoverCueTarget()
    {
        if (IsEditingLabel || _isEditingFileName || _draggedNode is not null || _resizedNode is not null || _draggedHandleTransition is not null || _isPanning || _linkSource is not null)
        {
            return null;
        }

        var mouse = Mouse.GetState();
        var mouseWorld = ScreenToWorld(mouse.Position.ToVector2());
        var handle = FindTransitionHandleAt(mouseWorld);
        if (handle.Transition is not null)
        {
            return null;
        }

        var node = FindNodeAt(mouseWorld);
        return node is not null && node != _selectedNode
            ? node
            : null;
    }

    private void DrawHoverCue(TimeSpan totalGameTime)
    {
        if (IsEditingLabel || _isEditingFileName || _draggedNode is not null || _resizedNode is not null || _draggedHandleTransition is not null || _isPanning || _linkSource is not null)
        {
            return;
        }

        var mouse = Mouse.GetState();
        var mouseWorld = ScreenToWorld(mouse.Position.ToVector2());
        var handle = FindTransitionHandleAt(mouseWorld);
        if (handle.Transition is not null && TryGetTransitionGeometry(handle.Transition, out var start, out var control1, out var control2, out var end))
        {
            var center = handle.Kind switch
            {
                TransitionHandleKind.SourceEndpoint => start,
                TransitionHandleKind.TargetEndpoint => end,
                TransitionHandleKind.ControlPoint1 => control1,
                TransitionHandleKind.ControlPoint2 => control2,
                _ => Vector2.Zero
            };
            if (center != Vector2.Zero)
            {
                _edgeRenderer.DrawTransitionHandleCue(center);
            }
            return;
        }

        var node = FindNodeAt(mouseWorld);
        if (node is not null)
        {
            return;
        }

        var transition = FindTransitionAt(mouseWorld);
        if (transition is not null && transition != _selectedTransition && TryGetTransitionGeometry(transition, out start, out control1, out control2, out end))
        {
            _edgeRenderer.DrawTransitionHoverCue(start, control1, control2, end, totalGameTime);
        }
    }

    private void DrawDiagramScene(Matrix transformMatrix, bool includeInteraction, TimeSpan totalGameTime)
    {
        _spriteBatch.Begin(samplerState: SamplerState.LinearClamp, transformMatrix: transformMatrix);
        DrawGrid(40, _boardTheme.GridColor);
        DrawDiagramContent(includeInteraction, totalGameTime);
        _spriteBatch.End();
    }

    private void DrawDiagramContent(bool includeInteraction, TimeSpan totalGameTime)
    {
        var editingDisplayLabel = GetEditingDisplayLabel();
        var editingDisplayCaretIndex = GetEditingDisplayCaretIndex();
        var showEditingCaret = ((int)(totalGameTime.TotalSeconds * 2)) % 2 == 0;
        if (includeInteraction)
        {
            DrawTransitionGhost(totalGameTime);
        }

        foreach (var transition in _transitions)
        {
            if (TryGetTransitionGeometry(transition, out var start, out var control1, out var control2, out var end))
            {
                var displayTransition = CanTransitionHaveEvent(transition)
                    ? transition
                    : new DiagramTransition
                    {
                        SourceId = transition.SourceId,
                        TargetId = transition.TargetId,
                        LabelSide = transition.LabelSide,
                        ControlPoint1 = transition.ControlPoint1,
                        ControlPoint2 = transition.ControlPoint2
                    };
                _edgeRenderer.DrawTransition(
                    displayTransition,
                    start,
                    control1,
                    control2,
                    end,
                    includeInteraction && transition == _selectedTransition,
                    transition == _editingTransition,
                    editingDisplayLabel,
                    editingDisplayCaretIndex,
                    showEditingCaret,
                    !CanTransitionHaveEvent(transition));
            }
        }
        if (includeInteraction)
        {
            DrawTransitionEventGhost(totalGameTime);
        }
        if (includeInteraction)
        {
            DrawHoverCue(totalGameTime);
            if (_linkSource is not null)
            {
                var mouse = Mouse.GetState();
                _edgeRenderer.DrawLinkPreview(_linkSource.Position, ScreenToWorld(mouse.Position.ToVector2()));
            }
        }
        if (includeInteraction)
        {
            DrawStartMarkerGhost(totalGameTime);
            DrawStateNodeGhost(totalGameTime);
        }
        var hoveredNode = includeInteraction ? GetNodeHoverCueTarget() : null;
        foreach (var node in _nodes)
        {
            var inactive = includeInteraction && IsInactiveDuringTransitionLink(node);
            _nodeRenderer.DrawNode(node, includeInteraction && node == _selectedNode, _editingNode, editingDisplayLabel, editingDisplayCaretIndex, showEditingCaret, totalGameTime, inactive, node == hoveredNode);
        }
        if (includeInteraction && _selectedNode is not null && !IsInactiveDuringTransitionLink(_selectedNode))
        {
            _nodeRenderer.DrawNodeResizeHandle(_selectedNode);
        }
        if (includeInteraction && _selectedTransition is not null)
        {
            if (TryGetTransitionGeometry(_selectedTransition, out var start, out var control1, out var control2, out var end))
            {
                _edgeRenderer.DrawTransitionHandles(start, control1, control2, end);
            }
        }
    }

    private void DrawStartMarkerGhost(TimeSpan totalGameTime)
    {
        var context = CreateAssistantContext();
        if (!_yukaiLarkAssistant.ShouldDrawStartMarkerGhost(context))
        {
            return;
        }

        var screenPosition = _yukaiLarkAssistant.GetNodeScreenPosition(GraphicsDevice.Viewport, YukaiLarkAssistKind.CreateStartMarker);
        var worldPosition = SnapToHalfGrid(ScreenToWorld(screenPosition));
        var bob = YukaiLarkAssistant.GetAssistBobOffset(totalGameTime);
        var ghostNode = new DiagramNode
        {
            Label = "開始",
            Position = worldPosition + new Vector2(0f, bob),
            RadiusUnits = DiagramNode.TerminalRadiusUnits,
            ColorIndex = 0,
            Kind = NodeKind.StartMarker
        };
        _nodeRenderer.DrawStartMarkerGhost(ghostNode, 1f);
    }

    private void DrawStateNodeGhost(TimeSpan totalGameTime)
    {
        var context = CreateAssistantContext();
        if (!_yukaiLarkAssistant.ShouldDrawStateNodeGhost(context))
        {
            return;
        }

        var nodeId = _nextNodeId;
        var assistKind = context.NormalNodeCount == 1 && context.HasStartToNormalTransition
            ? YukaiLarkAssistKind.CreateSecondStateNode
            : YukaiLarkAssistKind.CreateStateNode;
        var screenPosition = _yukaiLarkAssistant.GetNodeScreenPosition(GraphicsDevice.Viewport, assistKind);
        var worldPosition = SnapToHalfGrid(ScreenToWorld(screenPosition));
        var bob = YukaiLarkAssistant.GetAssistBobOffset(totalGameTime);
        var ghostNode = new DiagramNode
        {
            Label = $"状態{nodeId}",
            Position = worldPosition + new Vector2(0f, bob),
            RadiusUnits = DiagramNode.DefaultRadiusUnits,
            ColorIndex = (nodeId - 1) % Palette.Length,
            Kind = NodeKind.Normal
        };
        _nodeRenderer.DrawStateNodeGhost(ghostNode, 1f);
    }

    private void DrawTransitionGhost(TimeSpan totalGameTime)
    {
        var context = CreateAssistantContext();
        if (!_yukaiLarkAssistant.ShouldDrawTransitionGhost(context))
        {
            return;
        }

        if (!TryGetAssistantTransitionEndpoints(out var source, out var target))
        {
            return;
        }

        var transition = new DiagramTransition { SourceId = source.Id, TargetId = target.Id };
        InitializeTransitionEndpoints(transition);
        if (!TryGetTransitionGeometry(transition, out var start, out var control1, out var control2, out var end))
        {
            return;
        }

        var offset = new Vector2(0f, YukaiLarkAssistant.GetAssistBobOffset(totalGameTime));
        _edgeRenderer.DrawTransitionGhost(start + offset, control1 + offset, control2 + offset, end + offset, 1f);
    }

    private bool TryGetUnreachedNormalTransitionEndpoints(out DiagramNode source, out DiagramNode target)
    {
        var bestDistance = float.MaxValue;
        source = null!;
        target = null!;

        foreach (var candidateTarget in _nodes.Where(node => node.Kind == NodeKind.Normal && !_transitions.Any(t => t.TargetId == node.Id)))
        {
            foreach (var candidateSource in _nodes.Where(node => CanConnectUnreachedNormalFrom(node, candidateTarget)))
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

    private bool CanConnectUnreachedNormalFrom(DiagramNode source, DiagramNode target)
        => source.Id != target.Id
            && source.Kind != NodeKind.EndMarker
            && (source.Kind != NodeKind.StartMarker || !HasOutgoingTransition(source))
            && !_transitions.Any(t => t.SourceId == source.Id && t.TargetId == target.Id);
    private bool TryGetAssistantTransitionEndpoints(out DiagramNode source, out DiagramNode target)
    {
        var startMarker = _nodes.FirstOrDefault(node => node.Kind == NodeKind.StartMarker);
        var endMarker = _nodes.FirstOrDefault(node => node.Kind == NodeKind.EndMarker);
        var normalNodes = _nodes
            .Where(node => node.Kind == NodeKind.Normal)
            .OrderBy(node => node.Id)
            .ToList();

        if (startMarker is not null
            && normalNodes.Count >= 1
            && !HasOutgoingTransition(startMarker))
        {
            source = startMarker;
            target = normalNodes[0];
            return true;
        }

        if (normalNodes.Count >= 2
            && !_transitions.Any(t => t.SourceId == normalNodes[0].Id && t.TargetId == normalNodes[1].Id))
        {
            source = normalNodes[0];
            target = normalNodes[1];
            return true;
        }

        if (TryGetNormalToEndTransitionSuggestion(out source, out target))
        {
            return true;
        }

        if (TryGetUnreachedNormalTransitionEndpoints(out source, out target))
        {
            return true;
        }

        source = null!;
        target = null!;
        return false;
    }

    private bool TryGetNormalToEndTransitionSuggestion(out DiagramNode source, out DiagramNode target)
    {
        var startMarker = _nodes.FirstOrDefault(node => node.Kind == NodeKind.StartMarker);
        var endMarker = _nodes.FirstOrDefault(node => node.Kind == NodeKind.EndMarker);
        var normalNodes = _nodes
            .Where(node => node.Kind == NodeKind.Normal)
            .OrderBy(node => node.Id)
            .ToList();
        var normalToEndSource = startMarker is null
            ? normalNodes.LastOrDefault()
            : normalNodes
                .OrderByDescending(node => (node.Position - startMarker.Position).LengthSquared())
                .ThenBy(node => node.Id)
                .FirstOrDefault();

        if (endMarker is not null
            && normalToEndSource is not null
            && !_transitions.Any(t => t.SourceId == normalToEndSource.Id && t.TargetId == endMarker.Id)
            && !IsAssistSuggestionSuppressed(AssistSuggestionKind.NormalToEndTransition, normalToEndSource.Id, endMarker.Id))
        {
            source = normalToEndSource;
            target = endMarker;
            return true;
        }

        source = null!;
        target = null!;
        return false;
    }

    private bool IsAssistSuggestionSuppressed(AssistSuggestionKind kind, int sourceId, int targetId)
        => _assistSuppression.SuppressedSuggestions.Any(suppression =>
            suppression.Kind == kind
            && suppression.SourceId == sourceId
            && suppression.TargetId == targetId);

    private void DrawTransitionEventGhost(TimeSpan totalGameTime)
    {
        var context = CreateAssistantContext();
        if (!_yukaiLarkAssistant.ShouldDrawTransitionEventGhost(context))
        {
            return;
        }

        var transition = _transitions.FirstOrDefault(t => CanTransitionHaveEvent(t) && string.IsNullOrWhiteSpace(t.Label));
        if (transition is null || !TryGetTransitionGeometry(transition, out var start, out var control1, out var control2, out var end))
        {
            return;
        }

        var offset = new Vector2(0f, YukaiLarkAssistant.GetAssistBobOffset(totalGameTime));
        _edgeRenderer.DrawTransitionEventGhost(transition, start + offset, control1 + offset, control2 + offset, end + offset, 1f);
    }

    private void DrawScreenRectangleOutline(Rectangle rectangle, Color color, int thickness)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, thickness), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Bottom - thickness, rectangle.Width, thickness), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Y, thickness, rectangle.Height), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.Right - thickness, rectangle.Y, thickness, rectangle.Height), color);
    }
    private Texture2D? LoadTextureWithTransparentWhite(string relativePath)
    {
        var path = Path.Combine(AppContext.BaseDirectory, relativePath);
        if (!File.Exists(path))
        {
            path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", relativePath);
        }
        if (!File.Exists(path))
        {
            _status = "ユカイラーク画像が見つかりません。";
            return null;
        }

        using var stream = File.OpenRead(path);
        var texture = Texture2D.FromStream(GraphicsDevice, stream);
        var pixels = new Color[texture.Width * texture.Height];
        texture.GetData(pixels);

        var backgroundPixels = FindConnectedWhiteBackgroundPixels(pixels, texture.Width, texture.Height);
        for (var i = 0; i < pixels.Length; i++)
        {
            if (backgroundPixels[i])
            {
                pixels[i] = Color.Transparent;
            }
        }
        texture.SetData(pixels);
        return texture;
    }

    private static bool[] FindConnectedWhiteBackgroundPixels(Color[] pixels, int width, int height)
    {
        var backgroundPixels = new bool[pixels.Length];
        var queue = new Queue<int>();

        for (var x = 0; x < width; x++)
        {
            EnqueueWhiteBackgroundPixel(x, 0);
            EnqueueWhiteBackgroundPixel(x, height - 1);
        }

        for (var y = 1; y < height - 1; y++)
        {
            EnqueueWhiteBackgroundPixel(0, y);
            EnqueueWhiteBackgroundPixel(width - 1, y);
        }

        while (queue.Count > 0)
        {
            var index = queue.Dequeue();
            var x = index % width;
            var y = index / width;

            if (x > 0)
            {
                EnqueueWhiteBackgroundPixel(x - 1, y);
            }
            if (x < width - 1)
            {
                EnqueueWhiteBackgroundPixel(x + 1, y);
            }
            if (y > 0)
            {
                EnqueueWhiteBackgroundPixel(x, y - 1);
            }
            if (y < height - 1)
            {
                EnqueueWhiteBackgroundPixel(x, y + 1);
            }
        }

        return backgroundPixels;

        void EnqueueWhiteBackgroundPixel(int x, int y)
        {
            var index = y * width + x;
            if (backgroundPixels[index] || !IsWhiteBackgroundPixel(pixels[index]))
            {
                return;
            }

            backgroundPixels[index] = true;
            queue.Enqueue(index);
        }
    }

    private static bool IsWhiteBackgroundPixel(Color pixel)
        => pixel.A > 0 && pixel.R > 232 && pixel.G > 232 && pixel.B > 232;

    private void DrawYukaiLarkMascot(Viewport viewport, TimeSpan totalGameTime)
    {
        if (_yukaiLarkMascotTexture is null)
        {
            return;
        }

        _yukaiLarkAssistant.Draw(
            _spriteBatch,
            _yukaiLarkMascotTexture,
            _pixel,
            viewport,
            totalGameTime,
            CreateAssistantContext(),
            _boardTheme,
            GetAssistantAvoidBounds(viewport),
            DrawScreenRectangleOutline,
            DrawUiText);
    }

    private Rectangle GetAssistantAvoidBounds(Viewport viewport)
    {
        var hasInspectorBounds = InspectorPanelRenderer.TryGetPanelBounds(viewport, out var inspectorBounds);
        var hasMiniMapBounds = TryGetMiniMapBounds(viewport, out var miniMapBounds);
        return (hasInspectorBounds, hasMiniMapBounds) switch
        {
            (true, true) => Rectangle.Union(inspectorBounds, miniMapBounds),
            (true, false) => inspectorBounds,
            (false, true) => miniMapBounds,
            _ => Rectangle.Empty
        };
    }

    private void DrawBottomShortcutHelp(GameTime gameTime)
    {
        _shortcutKeyRenderer.DrawBottomHelp(
            GraphicsDevice.Viewport,
            gameTime.TotalGameTime,
            IsEditingLabel,
            _isExportSelecting,
            _isThemeMenuOpen,
            _hasExportSelection,
            _nodes.Any(node => node.Kind == NodeKind.StartMarker),
            _selectedNode,
            _selectedTransition);
    }
    private void DrawInspectorPanel()
    {
        _inspectorPanelRenderer.DrawInspectorPanel(
            GraphicsDevice.Viewport,
            _nodes.Count,
            _transitions.Count,
            GetSelectionSummary(),
            _nodes,
            _cameraOffset,
            _boardTheme);
    }

    private void DrawMiniMapOverlay()
    {
        if (!TryGetMiniMapBounds(GraphicsDevice.Viewport, out var bounds))
        {
            return;
        }

        var dimmed = _yukaiLarkAssistant.CutInBandBounds != Rectangle.Empty
            && bounds.Intersects(_yukaiLarkAssistant.CutInBandBounds);
        _miniMapRenderer.Draw(bounds, _nodes, GraphicsDevice.Viewport, _cameraOffset, _cameraZoom, _boardTheme, dimmed);
    }

    private static bool TryGetMiniMapBounds(Viewport viewport, out Rectangle bounds)
    {
        if (viewport.Width < 560 || viewport.Height < 420)
        {
            bounds = Rectangle.Empty;
            return false;
        }

        var x = viewport.Width - MiniMapWidth - MiniMapRightMargin;
        var y = viewport.Height - MiniMapHeight - MiniMapBottomMargin;
        bounds = new Rectangle(x, Math.Max(86, y), MiniMapWidth, MiniMapHeight);
        return true;
    }

    private string GetSelectionSummary()
    {
        if (_selectedNode is not null)
        {
            return _selectedNode.Kind switch
            {
                NodeKind.StartMarker => $"選択: 開始マーク {_selectedNode.Id} / サイズ {_selectedNode.RadiusUnits}",
                NodeKind.EndMarker => $"選択: 終了マーク {_selectedNode.Id} / サイズ {_selectedNode.RadiusUnits}",
                _ => $"選択: 状態 {_selectedNode.Id} / 通常 / サイズ {_selectedNode.RadiusUnits}"
            };
        }

        if (_selectedTransition is not null)
        {
            return $"選択: 遷移 {_selectedTransition.SourceId} -> {_selectedTransition.TargetId}";
        }

        return "選択: なし";
    }
    private string GetHeaderTitle()
        => _currentFilePath is null ? "未保存のダイアグラム" : Path.GetFileName(_currentFilePath);

    private float DrawUiText(string text, Vector2 position, Color color, float size, bool bold)
    {
        var texture = GetUiTextTexture(text, size, bold);
        _spriteBatch.Draw(texture, position, color);
        return position.X + texture.Width;
    }

    private Texture2D GetUiTextTexture(string text, float size, bool bold)
    {
        var cacheKey = $"ui|{size}|{bold}|{text}";
        if (_uiTextTextureCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var texture = TextRenderer.CreateUiTextTexture(GraphicsDevice, text, size, bold);
        _uiTextTextureCache[cacheKey] = texture;
        return texture;
    }

    private bool IsInactiveDuringTransitionLink(DiagramNode node)
    {
        if (_invalidLinkSource is not null)
        {
            return node == _invalidLinkSource;
        }

        return _linkSource is not null && node != _linkSource && !CanEndTransitionAt(node);
    }

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

    private Texture2D GetLabelTexture(string label, bool editing)
    {
        var cacheKey = $"{(editing ? "edit" : "label")}|{label}";
        if (_labelTextureCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var texture = TextRenderer.CreateLabelTexture(GraphicsDevice, label, editing);
        _labelTextureCache[cacheKey] = texture;
        return texture;
    }
    private static Vector2 CubicBezier(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end, float t)
    {
        var u = 1f - t;
        return u * u * u * start
            + 3f * u * u * t * control1
            + 3f * u * t * t * control2
            + t * t * t * end;
    }

    private static Vector2 CubicBezierTangent(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end, float t)
    {
        var u = 1f - t;
        return 3f * u * u * (control1 - start)
            + 6f * u * t * (control2 - control1)
            + 3f * t * t * (end - control2);
    }
    private void DrawText(string text, Vector2 position, Color color, int scale)
    {
        PrimitiveText.Draw(_spriteBatch, _pixel, text, position, color, scale);
    }
    private static Color WithAlpha(Color color, byte alpha)
        => new(color.R, color.G, color.B, alpha);

    private static Color Blend(Color from, Color to, float amount)
    {
        var clamped = MathHelper.Clamp(amount, 0f, 1f);
        return new Color(
            (byte)MathF.Round(MathHelper.Lerp(from.R, to.R, clamped)),
            (byte)MathF.Round(MathHelper.Lerp(from.G, to.G, clamped)),
            (byte)MathF.Round(MathHelper.Lerp(from.B, to.B, clamped)),
            (byte)MathF.Round(MathHelper.Lerp(from.A, to.A, clamped)));
    }
    private static float DistanceToBezier(Vector2 point, Vector2 start, Vector2 control1, Vector2 control2, Vector2 end)
    {
        const int segments = 32;
        var best = float.MaxValue;
        var previous = start;
        for (var i = 1; i <= segments; i++)
        {
            var t = i / (float)segments;
            var current = CubicBezier(start, control1, control2, end, t);
            best = MathF.Min(best, DistanceToSegment(point, previous, current));
            previous = current;
        }

        return best;
    }
    private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var lengthSquared = ab.LengthSquared();
        if (lengthSquared == 0)
        {
            return Vector2.Distance(point, a);
        }
        var t = MathHelper.Clamp(Vector2.Dot(point - a, ab) / lengthSquared, 0f, 1f);
        var projection = a + t * ab;
        return Vector2.Distance(point, projection);
    }
    private static readonly Color[] Palette =
    {
        new(60, 130, 220),
        new(40, 165, 120),
        new(205, 90, 95),
        new(180, 130, 55),
        new(130, 105, 200),
        new(55, 150, 170)
    };
}
public sealed class DiagramDocument
{
    public const int CurrentFormatVersion = 2;

    public int FormatVersion { get; set; } = CurrentFormatVersion;
    public DiagramDataSection Data { get; set; } = new();
    public AssistSuppressionSection AssistSuppression { get; set; } = new();

    [JsonIgnore]
    public List<DiagramNode> Nodes
    {
        get => Data.Nodes;
        set => Data.Nodes = value ?? new List<DiagramNode>();
    }

    [JsonIgnore]
    public List<DiagramTransition> Transitions
    {
        get => Data.Transitions;
        set => Data.Transitions = value ?? new List<DiagramTransition>();
    }
}

public sealed class DiagramDataSection
{
    public List<DiagramNode> Nodes { get; set; } = new();
    public List<DiagramTransition> Transitions { get; set; } = new();
}

public sealed class AssistSuppressionSection
{
    public List<AssistSuggestionSuppression> SuppressedSuggestions { get; set; } = new();
}

public sealed class AssistSuggestionSuppression
{
    public AssistSuggestionKind Kind { get; set; }
    public int SourceId { get; set; }
    public int TargetId { get; set; }
}

public enum AssistSuggestionKind
{
    NormalToEndTransition = 1
}
public sealed class DiagramNode
{
    public const float RadiusUnit = 20f;
    public const int DefaultRadiusUnits = 3;
    public const int TerminalRadiusUnits = 2;
    public const int MinRadiusUnits = 1;
    public const int MaxRadiusUnits = 12;
    private int _radiusUnits = DefaultRadiusUnits;
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public Vector2 Position { get; set; }
    public int RadiusUnits
    {
        get => Math.Clamp(_radiusUnits, MinRadiusUnits, MaxRadiusUnits);
        set => _radiusUnits = Math.Clamp(value, MinRadiusUnits, MaxRadiusUnits);
    }
    [JsonIgnore]
    public float Radius => RadiusUnits * RadiusUnit;
    public int ColorIndex { get; set; }
    public NodeKind Kind { get; set; }
}
public enum NodeKind
{
    Normal = 0,
    StartMarker = 1,
    EndMarker = 2
}
public sealed class DiagramTransition
{
    public int SourceId { get; set; }
    public int TargetId { get; set; }
    public string Label { get; set; } = string.Empty;
    public int LabelSide { get; set; }
    public float LabelAnchorT { get; set; } = 0.5f;
    public Vector2? LabelOffset { get; set; }
    public float? SourceAngle { get; set; }
    public float? TargetAngle { get; set; }
    public Vector2? ControlPoint1 { get; set; }
    public Vector2? ControlPoint2 { get; set; }
}
public sealed record TransitionNodeDragSnapshot(
    DiagramTransition Transition,
    Vector2 SourcePosition,
    Vector2 TargetPosition,
    float SourceAngle,
    float TargetAngle,
    float SourceRelationAngle,
    float TargetRelationAngle,
    bool HasControlPoint1,
    bool HasControlPoint2,
    Vector2 ControlPoint1Offset,
    Vector2 ControlPoint2Offset);
public sealed record TransitionHandleHit(DiagramTransition? Transition, TransitionHandleKind Kind);
public enum TransitionHandleKind
{
    None,
    SourceEndpoint,
    TargetEndpoint,
    ControlPoint1,
    ControlPoint2
}
public static class PrimitiveText
{
    private static readonly Dictionary<char, string[]> Glyphs = new()
    {
        [' '] = new[] { "000", "000", "000", "000", "000" },
        ['0'] = new[] { "111", "101", "101", "101", "111" },
        ['1'] = new[] { "010", "110", "010", "010", "111" },
        ['2'] = new[] { "111", "001", "111", "100", "111" },
        ['3'] = new[] { "111", "001", "111", "001", "111" },
        ['4'] = new[] { "101", "101", "111", "001", "001" },
        ['5'] = new[] { "111", "100", "111", "001", "111" },
        ['6'] = new[] { "111", "100", "111", "101", "111" },
        ['7'] = new[] { "111", "001", "010", "010", "010" },
        ['8'] = new[] { "111", "101", "111", "101", "111" },
        ['9'] = new[] { "111", "101", "111", "001", "111" },
        ['A'] = new[] { "010", "101", "111", "101", "101" },
        ['B'] = new[] { "110", "101", "110", "101", "110" },
        ['C'] = new[] { "111", "100", "100", "100", "111" },
        ['D'] = new[] { "110", "101", "101", "101", "110" },
        ['E'] = new[] { "111", "100", "110", "100", "111" },
        ['F'] = new[] { "111", "100", "110", "100", "100" },
        ['G'] = new[] { "111", "100", "101", "101", "111" },
        ['H'] = new[] { "101", "101", "111", "101", "101" },
        ['I'] = new[] { "111", "010", "010", "010", "111" },
        ['J'] = new[] { "001", "001", "001", "101", "111" },
        ['K'] = new[] { "101", "101", "110", "101", "101" },
        ['L'] = new[] { "100", "100", "100", "100", "111" },
        ['M'] = new[] { "101", "111", "111", "101", "101" },
        ['N'] = new[] { "101", "111", "111", "111", "101" },
        ['O'] = new[] { "111", "101", "101", "101", "111" },
        ['P'] = new[] { "111", "101", "111", "100", "100" },
        ['Q'] = new[] { "111", "101", "101", "111", "001" },
        ['R'] = new[] { "111", "101", "111", "110", "101" },
        ['S'] = new[] { "111", "100", "111", "001", "111" },
        ['T'] = new[] { "111", "010", "010", "010", "010" },
        ['U'] = new[] { "101", "101", "101", "101", "111" },
        ['V'] = new[] { "101", "101", "101", "101", "010" },
        ['W'] = new[] { "101", "101", "111", "111", "101" },
        ['X'] = new[] { "101", "101", "010", "101", "101" },
        ['Y'] = new[] { "101", "101", "010", "010", "010" },
        ['Z'] = new[] { "111", "001", "010", "100", "111" },
        [':'] = new[] { "000", "010", "000", "010", "000" },
        ['.'] = new[] { "000", "000", "000", "000", "010" },
        ['+'] = new[] { "000", "010", "111", "010", "000" },
        ['/'] = new[] { "001", "001", "010", "100", "100" },
        ['-'] = new[] { "000", "000", "111", "000", "000" },
        ['>'] = new[] { "100", "010", "001", "010", "100" }
    };
    public static Vector2 Measure(string text, int scale)
        => new(text.Length * 4 * scale, 5 * scale);
    public static void Draw(SpriteBatch spriteBatch, Texture2D pixel, string text, Vector2 position, Color color, int scale)
    {
        var x = (int)position.X;
        var y = (int)position.Y;
        foreach (var character in text.ToUpperInvariant())
        {
            var glyph = Glyphs.TryGetValue(character, out var value) ? value : Glyphs[' '];
            for (var row = 0; row < glyph.Length; row++)
            {
                for (var column = 0; column < glyph[row].Length; column++)
                {
                    if (glyph[row][column] == '1')
                    {
                        spriteBatch.Draw(pixel, new Rectangle(x + column * scale, y + row * scale, scale, scale), color);
                    }
                }
            }
            x += 4 * scale;
        }
    }
}

