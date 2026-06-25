namespace YukaiLarkStateTransitionDiagram;

using YukaiLarkStateTransitionDiagram.Theme;
using YukaiLarkStateTransitionDiagram.Assistants;
using YukaiLarkStateTransitionDiagram.Navigation;
using YukaiLarkStateTransitionDiagram.Persistence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
public class Game1 : Game
{
    private const string AppTitle = "YukaiLark State Transition Diagram";
    private const int MaxHistoryCount = 100;
    private const int ExportPhotoImageMargin = 34;
    private const int ExportPhotoPaperSidePadding = 16;
    private const int ExportPhotoPaperTopPadding = 18;
    private const int ExportPhotoPaperBottomPadding = 54;
    private const int ExportPhotoOuterBottomPadding = 10;
    private const int ExportSelectionPadding = 40;
    private const int ExportSelectionDefaultWidth = 640;
    private const int ExportSelectionDefaultHeight = 360;
    private const float ExportFlashDurationSeconds = 0.18f;
    private const float ExportPhotoPreviewDurationSeconds = 1.35f;
    private const int RecentFileMenuMaxItems = AppConfig.MaxRecentFiles;
    private const string YukaiLarkMascotTexturePath = "Assets/BrandLogo/yukai-lark-logo.png";
    private static readonly Keys[] ThemeDigitKeys =
    [
        Keys.D0,
        Keys.D1,
        Keys.D2,
        Keys.D3,
        Keys.D4,
        Keys.D5,
        Keys.D6,
        Keys.D7,
        Keys.D8,
        Keys.D9
    ];
    private static readonly Keys[] ThemeNumPadKeys =
    [
        Keys.NumPad0,
        Keys.NumPad1,
        Keys.NumPad2,
        Keys.NumPad3,
        Keys.NumPad4,
        Keys.NumPad5,
        Keys.NumPad6,
        Keys.NumPad7,
        Keys.NumPad8,
        Keys.NumPad9
    ];
    private readonly GraphicsDeviceManager _graphics;
    private readonly List<DiagramNode> _nodes = new();
    private readonly List<DiagramTransition> _transitions = new();
    private readonly Stack<DiagramDocument> _undoHistory = new();
    private readonly Stack<DiagramDocument> _redoHistory = new();
    private readonly Dictionary<string, Texture2D> _labelTextureCache = new();
    private readonly Dictionary<string, Texture2D> _uiTextTextureCache = new();
    private PrimitiveRenderer _primitiveRenderer = null!;
    private EdgeRenderer _edgeRenderer = null!;
    private NodeRenderer _nodeRenderer = null!;
    private HeaderRenderer _headerRenderer = null!;
    private InspectorPanelRenderer _inspectorPanelRenderer = null!;
    private ShortcutKeyRenderer _shortcutKeyRenderer = null!;
    private IKeyCapTheme _keyCapTheme = KeyCapThemes.Current;
    private BoardTheme _boardTheme = BoardThemes.ForKeyCapTheme(KeyCapThemes.Current);
    private SpriteBatch _spriteBatch = null!;
    private Texture2D _pixel = null!;
    private Texture2D? _yukaiLarkMascotTexture;
    private readonly YukaiLarkAssistant _yukaiLarkAssistant = new();
    private readonly TextBoxController _textBoxController = new(24);
    private AppConfig _appConfig = new();

    private MouseState _previousMouse;
    private KeyboardState _previousKeyboard;
    private DiagramNode? _selectedNode;
    private DiagramTransition? _selectedTransition;
    private DiagramNode? _draggedNode;
    private DiagramNode? _linkSource;
    private DiagramNode? _editingNode;
    private DiagramTransition? _editingTransition;
    private DiagramTransition? _draggedHandleTransition;
    private TransitionHandleKind _draggedHandleKind;
    private DiagramNode? _resizedNode;
    private string? _currentFilePath;
    private DiagramDocument? _pendingHistorySnapshot;
    private Vector2 _dragOffset;
    private Vector2 _cameraOffset;
    private Vector2 _panStartMouse;
    private Vector2 _panStartCamera;
    private MouseCursor? _currentMouseCursor;
    private bool _isPanning;
    private bool _isExportSelecting;
    private bool _isFileMenuOpen;
    private bool _isStartupFileMenu;
    private bool _exportSelectionDragging;
    private bool _hasExportSelection;
    private Rectangle _exportSelectionRectangle;
    private Rectangle _exportPhotoPreviewRectangle;
    private Rectangle _exportDragStartRectangle;
    private Vector2 _exportDragStart;
    private ExportSelectionDragMode _exportDragMode;
    private float _exportFlashSecondsRemaining;
    private float _exportPhotoPreviewSecondsRemaining;
    private int _nextNodeId = 1;
    private string _status = DefaultStatus;
    private const string DefaultStatus = "N: 状態追加 / S: 開始マーク / Shift+ドラッグ: 遷移作成 / F2・Enter: ラベル編集 / Ctrl+Z/Y: 元に戻す/やり直し / Ctrl+S: 保存";
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
        _inspectorPanelRenderer = new InspectorPanelRenderer(GraphicsDevice, _spriteBatch, _pixel);
        _shortcutKeyRenderer = new ShortcutKeyRenderer(GraphicsDevice, _spriteBatch, _pixel, _keyCapTheme, _boardTheme);
        _nodeRenderer = new NodeRenderer(_primitiveRenderer, _spriteBatch, Palette, GetLabelTexture);
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
        _headerRenderer.DrawHeader(GraphicsDevice.Viewport, GetHeaderTitle(), _status, _boardTheme);

        // ［開始マーク作成アシスト］の描画
        DrawYukaiLarkMascot(GraphicsDevice.Viewport, gameTime.TotalGameTime);

        DrawInspectorPanel();
        _shortcutKeyRenderer.DrawBottomHelp(
            GraphicsDevice.Viewport,
            gameTime.TotalGameTime,
            IsEditingLabel,
            _isExportSelecting,
            _hasExportSelection,
            _nodes.Any(node => node.Kind == NodeKind.StartMarker),
            _selectedNode,
            _selectedTransition);
        DrawExportSelectionOverlay();
        DrawExportPhotoEffectOverlay();
        DrawFileMenuOverlay();
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
            && _transitions.Any(t => t.SourceId == normalToEndSource.Id && t.TargetId == endMarker.Id);

        var shouldSuggestShiftDiagramLeft = TryGetDiagramShiftLeftDistance(out var shiftDiagramLeftDistance);

        return new YukaiLarkAssistantContext(
            startMarker is not null,
            endMarker is not null,
            normalNodes.Count,
            hasStartToNormalTransition,
            hasNormalToNormalTransition,
            hasNormalToEndTransition,
            !string.IsNullOrEmpty(missingTransitionEventSummary),
            missingTransitionEventSummary,
            shouldSuggestShiftDiagramLeft,
            shiftDiagramLeftDistance,
            !IsEditingLabel
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
            var screenPosition = node.Position + _cameraOffset;
            minX = MathF.Min(minX, screenPosition.X - node.Radius);
            maxX = MathF.Max(maxX, screenPosition.X + node.Radius);
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

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (!IsEditingLabel)
        {
            return;
        }

        if (!_textBoxController.TryInputCharacter(e.Character))
        {
            _status = "ラベルは24文字までです。";
        }
    }

    private string GetEditingDisplayLabel()
        => _textBoxController.GetDisplayText();

    private int GetEditingDisplayCaretIndex()
        => _textBoxController.GetDisplayCaretIndex();

    private void HandleKeyboard(KeyboardState keyboard, MouseState mouse)
    {
        if (IsControlDown(keyboard) && IsNewKeyPress(keyboard, Keys.Z))
        {
            if (IsShiftDown(keyboard))
            {
                RedoDiagramChange();
            }
            else
            {
                UndoDiagramChange();
            }
            return;
        }
        if (IsControlDown(keyboard) && IsNewKeyPress(keyboard, Keys.Y))
        {
            RedoDiagramChange();
            return;
        }
        if (IsControlDown(keyboard) && IsNewKeyPress(keyboard, Keys.N))
        {
            CreateNewDiagram();
            return;
        }
        if (IsControlDown(keyboard) && IsNewKeyPress(keyboard, Keys.S))
        {
            if (IsShiftDown(keyboard))
            {
                SaveDiagramAs();
            }
            else
            {
                SaveDiagram();
            }
            return;
        }
        if (IsControlDown(keyboard) && IsNewKeyPress(keyboard, Keys.O))
        {
            LoadDiagramFromDialog();
            return;
        }
        if (IsControlDown(keyboard) && IsNewKeyPress(keyboard, Keys.R))
        {
            OpenFileMenu(isStartup: false);
            return;
        }
        if (IsControlDown(keyboard) && IsNewKeyPress(keyboard, Keys.P))
        {
            BeginPngExportSelection();
            return;
        }
        if (TryGetThemeShortcutIndex(keyboard, out var themeIndex))
        {
            ApplyKeyCapTheme(themeIndex);
            return;
        }
        if (_yukaiLarkAssistant.ShouldRunFromKeyboard(CreateAssistantContext(), keyboard, _previousKeyboard, out var assistKind))
        {
            RunYukaiLarkAssist(assistKind);
            return;
        }
        if (IsNewKeyPress(keyboard, Keys.F2) || IsNewKeyPress(keyboard, Keys.Enter))
        {
            if (_selectedNode is not null)
            {
                BeginLabelEdit(_selectedNode);
                return;
            }
            if (_selectedTransition is not null)
            {
                BeginTransitionLabelEdit(_selectedTransition);
                return;
            }
        }
        if (_selectedTransition is not null && IsNewKeyPress(keyboard, Keys.Tab))
        {
            ToggleTransitionLabelSide(_selectedTransition);
            return;
        }
        if (IsNewKeyPress(keyboard, Keys.N))
        {
            AddNode(ScreenToWorld(mouse.Position.ToVector2()));
            return;
        }
        if (IsNewKeyPress(keyboard, Keys.E))
        {
            AddEndMarker(ScreenToWorld(mouse.Position.ToVector2()));
            return;
        }
        if (IsNewKeyPress(keyboard, Keys.S))
        {
            HandleStartMarkerShortcut(ScreenToWorld(mouse.Position.ToVector2()));
            return;
        }
        if (IsNewKeyPress(keyboard, Keys.Delete) || IsNewKeyPress(keyboard, Keys.Back))
        {
            DeleteSelection();
        }
        if (IsNewKeyPress(keyboard, Keys.C) && _selectedNode is not null)
        {
            if (_selectedNode.Kind == NodeKind.Normal)
            {
                ExecuteUndoableChange(() =>
                {
                    _selectedNode.ColorIndex = (_selectedNode.ColorIndex + 1) % Palette.Length;
                });
                _status = "選択中の状態色を切り替えました。";
            }
            else
            {
                _status = "開始・終了マークは黒固定です。状態の色は通常ノードで変更できます。";
            }
        }
    }
    private void OpenFileMenu(bool isStartup)
    {
        _isFileMenuOpen = true;
        _isStartupFileMenu = isStartup;
        _status = "ユカイラーク: 最近保存したファイルを開く？ Nで新規、Oで読込、数字で最近ファイル。";
    }

    private void CloseFileMenu()
    {
        _isFileMenuOpen = false;
        _isStartupFileMenu = false;
    }

    private void HandleFileMenuKeyboard(KeyboardState keyboard)
    {
        if (IsNewKeyPress(keyboard, Keys.N))
        {
            CloseFileMenu();
            CreateNewDiagram();
            return;
        }

        if (IsNewKeyPress(keyboard, Keys.O))
        {
            CloseFileMenu();
            LoadDiagramFromDialog();
            return;
        }

        if (IsNewKeyPress(keyboard, Keys.Escape))
        {
            var wasStartup = _isStartupFileMenu;
            CloseFileMenu();
            if (wasStartup && _currentFilePath is null && _nodes.Count == 0 && _transitions.Count == 0)
            {
                _status = "空の状態遷移図を開きました。Ctrl+Nで開始・終了マーク付きの新規作成もできます。";
            }
            else
            {
                _status = "最近ファイルメニューを閉じました。";
            }
            return;
        }

        if (TryGetRecentFileShortcutIndex(keyboard, out var recentIndex))
        {
            LoadRecentFileByIndex(recentIndex);
        }
    }

    private void HandleFileMenuMouse(MouseState mouse)
    {
        if (mouse.LeftButton != ButtonState.Pressed || _previousMouse.LeftButton != ButtonState.Released)
        {
            return;
        }

        var point = mouse.Position;
        var (newButton, openButton) = GetFileMenuActionRectangles();
        if (newButton.Contains(point))
        {
            CloseFileMenu();
            CreateNewDiagram();
            return;
        }

        if (openButton.Contains(point))
        {
            CloseFileMenu();
            LoadDiagramFromDialog();
            return;
        }

        var recentFiles = GetRecentFiles();
        for (var i = 0; i < recentFiles.Count; i++)
        {
            if (GetRecentFileMenuItemRectangle(i).Contains(point))
            {
                LoadRecentFileByIndex(i);
                return;
            }
        }
    }

    private void LoadRecentFileByIndex(int index)
    {
        var recentFiles = GetRecentFiles();
        if (index < 0 || index >= recentFiles.Count)
        {
            return;
        }

        var path = recentFiles[index];
        if (!File.Exists(path))
        {
            _appConfig.RecentFiles.RemoveAll(file => string.Equals(file, path, StringComparison.OrdinalIgnoreCase));
            AppConfigStore.Save(_appConfig);
            _status = $"{Path.GetFileName(path)} が見つからないため、最近ファイルから外しました。";
            return;
        }

        CloseFileMenu();
        LoadDiagramFromPath(path);
    }

    private bool TryGetRecentFileShortcutIndex(KeyboardState keyboard, out int index)
    {
        for (var i = 0; i < RecentFileMenuMaxItems; i++)
        {
            var key = i == 9 ? Keys.D0 : Keys.D1 + i;
            var numPadKey = i == 9 ? Keys.NumPad0 : Keys.NumPad1 + i;
            if (IsNewKeyPress(keyboard, key) || IsNewKeyPress(keyboard, numPadKey))
            {
                index = i;
                return true;
            }
        }

        index = -1;
        return false;
    }

    private List<string> GetRecentFiles()
        => (_appConfig.RecentFiles ?? new List<string>())
            .Where(file => !string.IsNullOrWhiteSpace(file) && File.Exists(file))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(RecentFileMenuMaxItems)
            .ToList();

    private void DrawFileMenuOverlay()
    {
        if (!_isFileMenuOpen)
        {
            return;
        }

        var viewport = GraphicsDevice.Viewport;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), new Color(18, 26, 28, 150));

        var panel = GetFileMenuPanelRectangle();
        _spriteBatch.Draw(_pixel, new Rectangle(panel.X + 6, panel.Y + 8, panel.Width, panel.Height), new Color(16, 22, 24, 105));
        _spriteBatch.Draw(_pixel, panel, new Color(255, 253, 239, 245));
        DrawScreenRectangleOutline(panel, new Color(83, 178, 176), 2);

        if (_yukaiLarkMascotTexture is not null)
        {
            var mascotSize = 88;
            var mascot = new Rectangle(panel.X + 24, panel.Y + 24, mascotSize, mascotSize);
            _spriteBatch.Draw(_yukaiLarkMascotTexture, mascot, Color.White);
        }

        var textX = panel.X + 130;
        var textY = panel.Y + 24;
        var prompt = _isStartupFileMenu ? "最近保存したファイルを開く？" : "最近保存したファイル";
        DrawUiText("ユカイラーク", new Vector2(textX, textY), new Color(51, 84, 102), 18, true);
        DrawUiText(prompt, new Vector2(textX, textY + 28), new Color(38, 55, 62), 24, true);
        DrawUiText("新しく始めるか、ファイルを読込むこともできます。", new Vector2(textX, textY + 64), new Color(88, 105, 112), 15, false);

        var (newButton, openButton) = GetFileMenuActionRectangles();
        DrawFileMenuButton(newButton, "N", "新規作成", "空の状態遷移図で始める", enabled: true);
        DrawFileMenuButton(openButton, "O", "読込", "JSONファイルを選ぶ", enabled: true);

        var recentFiles = GetRecentFiles();
        var recentTitleY = GetRecentFileMenuItemRectangle(0).Y - 28;
        DrawUiText("最近保存したファイル", new Vector2(panel.X + 24, recentTitleY), new Color(51, 84, 102), 16, true);
        if (recentFiles.Count == 0)
        {
            DrawUiText("まだ最近ファイルはありません。", new Vector2(panel.X + 24, recentTitleY + 34), new Color(110, 126, 132), 15, false);
            return;
        }

        for (var i = 0; i < recentFiles.Count; i++)
        {
            DrawRecentFileMenuItem(i, recentFiles[i]);
        }
    }

    private void DrawFileMenuButton(Rectangle bounds, string key, string title, string description, bool enabled)
    {
        var fill = enabled ? new Color(238, 250, 239) : new Color(230, 232, 232);
        var edge = enabled ? new Color(83, 178, 176) : new Color(160, 166, 168);
        _spriteBatch.Draw(_pixel, bounds, fill);
        DrawScreenRectangleOutline(bounds, edge, 2);
        DrawUiText(key, new Vector2(bounds.X + 14, bounds.Y + 11), new Color(51, 84, 102), 18, true);
        DrawUiText(title, new Vector2(bounds.X + 48, bounds.Y + 9), new Color(38, 55, 62), 17, true);
        DrawUiText(description, new Vector2(bounds.X + 48, bounds.Y + 34), new Color(88, 105, 112), 13, false);
    }

    private void DrawRecentFileMenuItem(int index, string path)
    {
        var bounds = GetRecentFileMenuItemRectangle(index);
        _spriteBatch.Draw(_pixel, bounds, index % 2 == 0 ? new Color(250, 252, 246, 245) : new Color(238, 250, 239, 245));
        DrawScreenRectangleOutline(bounds, new Color(178, 219, 203), 1);

        var shortcut = index == 9 ? "0" : (index + 1).ToString();
        DrawUiText(shortcut, new Vector2(bounds.X + 12, bounds.Y + 11), new Color(51, 84, 102), 16, true);
        DrawUiText(GetRecentFileDisplayText(path), new Vector2(bounds.X + 44, bounds.Y + 3), new Color(38, 55, 62), 15, true);
        DrawUiText(Path.GetDirectoryName(path) ?? string.Empty, new Vector2(bounds.X + 44, bounds.Y + 24), new Color(98, 116, 122), 12, false);
    }

    private (Rectangle NewButton, Rectangle OpenButton) GetFileMenuActionRectangles()
    {
        var panel = GetFileMenuPanelRectangle();
        var buttonWidth = Math.Max(220, (panel.Width - 60) / 2);
        var y = panel.Y + 116;
        return (
            new Rectangle(panel.X + 24, y, buttonWidth, 62),
            new Rectangle(panel.X + panel.Width - 24 - buttonWidth, y, buttonWidth, 62));
    }

    private Rectangle GetRecentFileMenuItemRectangle(int index)
    {
        var panel = GetFileMenuPanelRectangle();
        return new Rectangle(panel.X + 24, panel.Y + 224 + index * 44, panel.Width - 48, 40);
    }

    private Rectangle GetFileMenuPanelRectangle()
    {
        var viewport = GraphicsDevice.Viewport;
        var recentCount = Math.Max(1, GetRecentFiles().Count);
        var width = Math.Clamp(viewport.Width - 64, 560, 780);
        var height = Math.Min(viewport.Height - 64, 248 + recentCount * 44);
        var x = (viewport.Width - width) / 2;
        var y = Math.Max(24, (viewport.Height - height) / 2);
        return new Rectangle(x, y, width, height);
    }

    private static string GetRecentFileDisplayText(string path)
    {
        var fileName = Path.GetFileName(path);
        if (fileName.Length <= 54)
        {
            return fileName;
        }

        return fileName[..51] + "...";
    }
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
        => CloneDiagramDocument(new DiagramDocument { Nodes = _nodes, Transitions = _transitions });

    private void ApplyDiagramDocument(DiagramDocument document)
    {
        var snapshot = CloneDiagramDocument(document);
        _nodes.Clear();
        _transitions.Clear();
        _nodes.AddRange(snapshot.Nodes);
        _transitions.AddRange(snapshot.Transitions);
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
        _draggedHandleTransition = null;
        _draggedHandleKind = TransitionHandleKind.None;
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
            Nodes = document.Nodes.Select(CloneDiagramNode).ToList(),
            Transitions = document.Transitions.Select(CloneDiagramTransition).ToList()
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
            SourceAngle = transition.SourceAngle,
            TargetAngle = transition.TargetAngle,
            ControlPoint1 = transition.ControlPoint1,
            ControlPoint2 = transition.ControlPoint2
        };

    private static bool AreDiagramDocumentsEqual(DiagramDocument left, DiagramDocument right)
        => JsonSerializer.Serialize(left, YukaiDialogJsonSerializer.Options) == JsonSerializer.Serialize(right, YukaiDialogJsonSerializer.Options);
    private bool TryGetThemeShortcutIndex(KeyboardState keyboard, out int themeIndex)
    {
        for (var i = 0; i < ThemeDigitKeys.Length; i++)
        {
            if (IsNewKeyPress(keyboard, ThemeDigitKeys[i]) || IsNewKeyPress(keyboard, ThemeNumPadKeys[i]))
            {
                themeIndex = i;
                return true;
            }
        }

        themeIndex = -1;
        return false;
    }

    private void ApplyKeyCapTheme(int themeIndex)
    {
        if (themeIndex < 0 || themeIndex >= KeyCapThemes.ShortcutThemes.Count)
        {
            return;
        }

        _keyCapTheme = KeyCapThemes.ShortcutThemes[themeIndex];
        _boardTheme = BoardThemes.ForKeyCapTheme(_keyCapTheme);
        _edgeRenderer.Theme = _boardTheme;
        _shortcutKeyRenderer.KeyCapTheme = _keyCapTheme;
        _shortcutKeyRenderer.BoardTheme = _boardTheme;
        _status = $"テーマを {themeIndex}: {_keyCapTheme.Name} に切り替えました。背景とPNG出力にも反映します。";
    }

    private void HandleExportSelectionKeyboard(KeyboardState keyboard)
    {
        if (IsNewKeyPress(keyboard, Keys.Escape))
        {
            CancelPngExportSelection("PNG出力をキャンセルしました。");
            return;
        }

        if (IsNewKeyPress(keyboard, Keys.Enter))
        {
            if (!_hasExportSelection || _exportSelectionRectangle.Width < 16 || _exportSelectionRectangle.Height < 16)
            {
                _status = "PNG出力範囲が小さすぎます。左ドラッグで範囲を作ってください。";
                return;
            }

            if (SavePngSelection(_exportSelectionRectangle))
            {
                StartExportPhotoEffect(_exportSelectionRectangle);
                _isExportSelecting = false;
                _exportSelectionDragging = false;
                _hasExportSelection = false;
                _exportDragMode = ExportSelectionDragMode.None;
            }
        }
    }

    private void HandleExportSelectionMouse(KeyboardState keyboard, MouseState mouse)
    {
        var screenPosition = mouse.Position.ToVector2();
        var leftPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;
        var leftReleased = mouse.LeftButton == ButtonState.Released && _previousMouse.LeftButton == ButtonState.Pressed;
        var rightPressed = mouse.RightButton == ButtonState.Pressed && _previousMouse.RightButton == ButtonState.Released;
        var snapSelection = !IsAltDown(keyboard);

        if (rightPressed)
        {
            CancelPngExportSelection("PNG出力をキャンセルしました。");
            return;
        }

        if (leftPressed)
        {
            _exportDragStart = screenPosition;
            _exportDragStartRectangle = _exportSelectionRectangle;
            _exportDragMode = _hasExportSelection
                ? HitTestExportSelection(screenPosition, _exportSelectionRectangle)
                : ExportSelectionDragMode.New;
            if (_exportDragMode == ExportSelectionDragMode.None)
            {
                _exportDragMode = ExportSelectionDragMode.New;
                _hasExportSelection = false;
            }

            _exportSelectionDragging = true;
            if (_exportDragMode == ExportSelectionDragMode.New)
            {
                _exportSelectionRectangle = new Rectangle((int)screenPosition.X, (int)screenPosition.Y, 0, 0);
                _status = "PNG範囲を作成中です。左ドラッグで範囲、Alt中は吸着なし、右クリックでキャンセル。";
            }
            else
            {
                _status = "PNG範囲を調整中です。左ドラッグで調整、Alt中は吸着なし、右クリックでキャンセル。";
            }
            return;
        }

        if (_exportSelectionDragging && mouse.LeftButton == ButtonState.Pressed)
        {
            UpdateExportSelectionDrag(screenPosition, snapSelection);
        }

        if (leftReleased && _exportSelectionDragging)
        {
            UpdateExportSelectionDrag(screenPosition, snapSelection);
            _exportSelectionDragging = false;
            _exportDragMode = ExportSelectionDragMode.None;
            if (_exportSelectionRectangle.Width < 16 || _exportSelectionRectangle.Height < 16)
            {
                _hasExportSelection = false;
                _status = "PNG出力範囲が小さすぎます。左ドラッグで範囲を作り直してください。";
                return;
            }

            _hasExportSelection = true;
            _status = snapSelection
                ? "PNG範囲を半グリッドに吸着しました。左ドラッグで調整、Enterで撮影、右クリックでキャンセル。"
                : "PNG範囲を自由位置にしました。Altを離すと吸着、Enterで撮影、右クリックでキャンセル。";
        }
    }

    private void BeginPngExportSelection()
    {
        _isExportSelecting = true;
        _exportSelectionDragging = false;
        _exportSelectionRectangle = CreateInitialExportSelectionRectangle();
        _hasExportSelection = _exportSelectionRectangle.Width >= 16 && _exportSelectionRectangle.Height >= 16;
        _exportDragStartRectangle = Rectangle.Empty;
        _exportDragMode = ExportSelectionDragMode.None;
        _draggedNode = null;
        _resizedNode = null;
        _draggedHandleTransition = null;
        _draggedHandleKind = TransitionHandleKind.None;
        _linkSource = null;
        _isPanning = false;
        _status = "PNG出力モードです。枠をドラッグで調整、Enterで撮影、右クリック/Escでキャンセル。";
    }

    private Rectangle CreateInitialExportSelectionRectangle()
    {
        if (!TryGetDiagramScreenBounds(out var bounds))
        {
            return CreateDefaultExportSelectionRectangle();
        }

        var rectangle = RectangleFromEdges(
            (int)MathF.Floor(bounds.Left) - ExportSelectionPadding,
            (int)MathF.Floor(bounds.Top) - ExportSelectionPadding,
            (int)MathF.Ceiling(bounds.Right) + ExportSelectionPadding,
            (int)MathF.Ceiling(bounds.Bottom) + ExportSelectionPadding);
        rectangle = EnsureMinimumExportSelectionSize(rectangle);
        var clamped = ClampExportSelectionRectangle(rectangle);
        return clamped.Width >= 16 && clamped.Height >= 16
            ? clamped
            : CreateDefaultExportSelectionRectangle();
    }

    private bool TryGetDiagramScreenBounds(out Rectangle bounds)
    {
        var hasBounds = false;
        var left = 0f;
        var top = 0f;
        var right = 0f;
        var bottom = 0f;

        void Include(Vector2 point)
        {
            var screenPoint = point + _cameraOffset;
            if (!hasBounds)
            {
                left = right = screenPoint.X;
                top = bottom = screenPoint.Y;
                hasBounds = true;
                return;
            }

            left = MathF.Min(left, screenPoint.X);
            top = MathF.Min(top, screenPoint.Y);
            right = MathF.Max(right, screenPoint.X);
            bottom = MathF.Max(bottom, screenPoint.Y);
        }

        foreach (var node in _nodes)
        {
            Include(node.Position - new Vector2(node.Radius));
            Include(node.Position + new Vector2(node.Radius));
        }

        foreach (var transition in _transitions)
        {
            if (!TryGetTransitionGeometry(transition, out var start, out var control1, out var control2, out var end))
            {
                continue;
            }

            Include(start);
            Include(control1);
            Include(control2);
            Include(end);
        }

        bounds = hasBounds
            ? RectangleFromEdges((int)MathF.Floor(left), (int)MathF.Floor(top), (int)MathF.Ceiling(right), (int)MathF.Ceiling(bottom))
            : Rectangle.Empty;
        return hasBounds;
    }

    private Rectangle CreateDefaultExportSelectionRectangle()
    {
        var viewport = GraphicsDevice.Viewport;
        var width = Math.Min(ExportSelectionDefaultWidth, Math.Max(160, viewport.Width - ExportSelectionPadding * 2));
        var height = Math.Min(ExportSelectionDefaultHeight, Math.Max(120, viewport.Height - ExportSelectionPadding * 2));
        return new Rectangle((viewport.Width - width) / 2, (viewport.Height - height) / 2, width, height);
    }

    private Rectangle EnsureMinimumExportSelectionSize(Rectangle rectangle)
    {
        var width = Math.Max(rectangle.Width, Math.Min(ExportSelectionDefaultWidth, GraphicsDevice.Viewport.Width));
        var height = Math.Max(rectangle.Height, Math.Min(ExportSelectionDefaultHeight, GraphicsDevice.Viewport.Height));
        var centerX = rectangle.X + rectangle.Width / 2;
        var centerY = rectangle.Y + rectangle.Height / 2;
        return new Rectangle(centerX - width / 2, centerY - height / 2, width, height);
    }
    private void CancelPngExportSelection(string status)
    {
        _isExportSelecting = false;
        _exportSelectionDragging = false;
        _hasExportSelection = false;
        _exportDragMode = ExportSelectionDragMode.None;
        _status = status;
    }

    private Rectangle GetExportSelectionRectangle()
        => _exportSelectionRectangle;

    private void UpdateExportSelectionDrag(Vector2 screenPosition, bool snapSelection)
    {
        var rectangle = _exportDragMode == ExportSelectionDragMode.New
            ? RectangleFromPoints(snapSelection ? SnapScreenToHalfGrid(_exportDragStart) : _exportDragStart, snapSelection ? SnapScreenToHalfGrid(screenPosition) : screenPosition)
            : ResizeExportSelection(_exportDragStartRectangle, _exportDragMode, screenPosition - _exportDragStart, snapSelection);
        _exportSelectionRectangle = ClampExportSelectionRectangle(rectangle);
        _hasExportSelection = _exportSelectionRectangle.Width >= 16 && _exportSelectionRectangle.Height >= 16;
    }

    private Rectangle ResizeExportSelection(Rectangle rectangle, ExportSelectionDragMode mode, Vector2 delta, bool snapSelection)
    {
        var left = rectangle.Left;
        var top = rectangle.Top;
        var right = rectangle.Right;
        var bottom = rectangle.Bottom;
        var dx = (int)MathF.Round(delta.X);
        var dy = (int)MathF.Round(delta.Y);

        if (mode == ExportSelectionDragMode.Move)
        {
            var x = rectangle.X + dx;
            var y = rectangle.Y + dy;
            if (snapSelection)
            {
                var snapped = SnapScreenToHalfGrid(new Vector2(x, y));
                x = (int)MathF.Round(snapped.X);
                y = (int)MathF.Round(snapped.Y);
            }
            return ClampMovedExportSelection(new Rectangle(x, y, rectangle.Width, rectangle.Height));
        }

        if (mode is ExportSelectionDragMode.Left or ExportSelectionDragMode.TopLeft or ExportSelectionDragMode.BottomLeft)
        {
            left += dx;
        }
        if (mode is ExportSelectionDragMode.Right or ExportSelectionDragMode.TopRight or ExportSelectionDragMode.BottomRight)
        {
            right += dx;
        }
        if (mode is ExportSelectionDragMode.Top or ExportSelectionDragMode.TopLeft or ExportSelectionDragMode.TopRight)
        {
            top += dy;
        }
        if (mode is ExportSelectionDragMode.Bottom or ExportSelectionDragMode.BottomLeft or ExportSelectionDragMode.BottomRight)
        {
            bottom += dy;
        }

        if (snapSelection)
        {
            if (mode is ExportSelectionDragMode.Left or ExportSelectionDragMode.TopLeft or ExportSelectionDragMode.BottomLeft)
            {
                left = SnapScreenX(left);
            }
            if (mode is ExportSelectionDragMode.Right or ExportSelectionDragMode.TopRight or ExportSelectionDragMode.BottomRight)
            {
                right = SnapScreenX(right);
            }
            if (mode is ExportSelectionDragMode.Top or ExportSelectionDragMode.TopLeft or ExportSelectionDragMode.TopRight)
            {
                top = SnapScreenY(top);
            }
            if (mode is ExportSelectionDragMode.Bottom or ExportSelectionDragMode.BottomLeft or ExportSelectionDragMode.BottomRight)
            {
                bottom = SnapScreenY(bottom);
            }
        }

        return RectangleFromEdges(left, top, right, bottom);
    }

    private Vector2 SnapScreenToHalfGrid(Vector2 screenPosition)
        => SnapToHalfGrid(ScreenToWorld(screenPosition)) + _cameraOffset;

    private int SnapScreenX(int x)
        => (int)MathF.Round(SnapScreenToHalfGrid(new Vector2(x, 0)).X);

    private int SnapScreenY(int y)
        => (int)MathF.Round(SnapScreenToHalfGrid(new Vector2(0, y)).Y);
    private Rectangle ClampMovedExportSelection(Rectangle rectangle)
    {
        var viewport = GraphicsDevice.Viewport;
        var width = Math.Min(rectangle.Width, viewport.Width);
        var height = Math.Min(rectangle.Height, viewport.Height);
        var x = Math.Clamp(rectangle.X, 0, viewport.Width - width);
        var y = Math.Clamp(rectangle.Y, 0, viewport.Height - height);
        return new Rectangle(x, y, width, height);
    }
    private Rectangle ClampExportSelectionRectangle(Rectangle rectangle)
    {
        var viewport = GraphicsDevice.Viewport;
        if (rectangle.Width < 0 || rectangle.Height < 0)
        {
            rectangle = RectangleFromEdges(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom);
        }

        var left = Math.Clamp(rectangle.Left, 0, viewport.Width);
        var top = Math.Clamp(rectangle.Top, 0, viewport.Height);
        var right = Math.Clamp(rectangle.Right, 0, viewport.Width);
        var bottom = Math.Clamp(rectangle.Bottom, 0, viewport.Height);
        return RectangleFromEdges(left, top, right, bottom);
    }

    private static Rectangle RectangleFromPoints(Vector2 a, Vector2 b)
        => RectangleFromEdges((int)MathF.Round(a.X), (int)MathF.Round(a.Y), (int)MathF.Round(b.X), (int)MathF.Round(b.Y));

    private static Rectangle RectangleFromEdges(int left, int top, int right, int bottom)
    {
        var x = Math.Min(left, right);
        var y = Math.Min(top, bottom);
        return new Rectangle(x, y, Math.Abs(right - left), Math.Abs(bottom - top));
    }

    private static ExportSelectionDragMode HitTestExportSelection(Vector2 position, Rectangle rectangle)
    {
        const int edge = 12;
        var x = (int)MathF.Round(position.X);
        var y = (int)MathF.Round(position.Y);
        var nearLeft = Math.Abs(x - rectangle.Left) <= edge && y >= rectangle.Top - edge && y <= rectangle.Bottom + edge;
        var nearRight = Math.Abs(x - rectangle.Right) <= edge && y >= rectangle.Top - edge && y <= rectangle.Bottom + edge;
        var nearTop = Math.Abs(y - rectangle.Top) <= edge && x >= rectangle.Left - edge && x <= rectangle.Right + edge;
        var nearBottom = Math.Abs(y - rectangle.Bottom) <= edge && x >= rectangle.Left - edge && x <= rectangle.Right + edge;

        if (nearLeft && nearTop) return ExportSelectionDragMode.TopLeft;
        if (nearRight && nearTop) return ExportSelectionDragMode.TopRight;
        if (nearLeft && nearBottom) return ExportSelectionDragMode.BottomLeft;
        if (nearRight && nearBottom) return ExportSelectionDragMode.BottomRight;
        if (nearLeft) return ExportSelectionDragMode.Left;
        if (nearRight) return ExportSelectionDragMode.Right;
        if (nearTop) return ExportSelectionDragMode.Top;
        if (nearBottom) return ExportSelectionDragMode.Bottom;
        return rectangle.Contains(x, y) ? ExportSelectionDragMode.Move : ExportSelectionDragMode.None;
    }
    private bool SavePngSelection(Rectangle selection)
    {
        var dialog = new SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = "png",
            FileName = CreateDefaultPngFileName(),
            Filter = "PNG files (*.png)|*.png|All files (*.*)|*.*",
            InitialDirectory = GetInitialDirectory(),
            OverwritePrompt = true,
            Title = "PNG画像の保存先を指定"
        };
        if (dialog.ShowDialog() != true)
        {
            _status = "PNG出力をキャンセルしました。範囲調整に戻ります。";
            return false;
        }

        ExportSelectionToPng(selection, dialog.FileName);
        _status = $"{Path.GetFileName(dialog.FileName)} をPNG出力しました。";
        return true;
    }

    private void ExportSelectionToPng(Rectangle selection, string path)
    {
        var imageWidth = selection.Width + ExportPhotoImageMargin * 2;
        var imageHeight = selection.Height + ExportPhotoImageMargin + ExportPhotoPaperBottomPadding + ExportPhotoOuterBottomPadding;
        var imageArea = new Rectangle(ExportPhotoImageMargin, ExportPhotoImageMargin, selection.Width, selection.Height);
        var previousTargets = GraphicsDevice.GetRenderTargets();
        using var renderTarget = new RenderTarget2D(GraphicsDevice, imageWidth, imageHeight, false, SurfaceFormat.Color, DepthFormat.None);

        GraphicsDevice.SetRenderTarget(renderTarget);
        GraphicsDevice.Clear(_boardTheme.ExportBackdropColor);

        _spriteBatch.Begin(samplerState: SamplerState.LinearClamp);
        DrawExportPhotoFrame(new Rectangle(0, 0, imageWidth, imageHeight), imageArea, fillBackdrop: true, fillImageArea: true);
        _spriteBatch.End();

        var worldTopLeft = ScreenToWorld(new Vector2(selection.X, selection.Y));
        var worldBottomRight = ScreenToWorld(new Vector2(selection.Right, selection.Bottom));
        var exportTransform = Matrix.CreateTranslation(-worldTopLeft.X + imageArea.X, -worldTopLeft.Y + imageArea.Y, 0f);
        var previousScissor = GraphicsDevice.ScissorRectangle;
        using var scissorRasterizer = new RasterizerState { ScissorTestEnable = true };
        GraphicsDevice.ScissorRectangle = imageArea;
        _spriteBatch.Begin(samplerState: SamplerState.LinearClamp, rasterizerState: scissorRasterizer, transformMatrix: exportTransform);
        DrawGrid(40, _boardTheme.GridColor, worldTopLeft, worldBottomRight);
        DrawDiagramContent(includeInteraction: false, TimeSpan.Zero);
        _spriteBatch.End();
        GraphicsDevice.ScissorRectangle = previousScissor;

        _spriteBatch.Begin(samplerState: SamplerState.LinearClamp);
        DrawExportPhotoTop(imageArea);
        _spriteBatch.End();

        GraphicsDevice.SetRenderTargets(previousTargets);
        using var stream = File.Create(path);
        renderTarget.SaveAsPng(stream, imageWidth, imageHeight);
    }

    private static string CreateDefaultPngFileName()
        => $"{DateTime.Now:yyyyMMddHHmmss}_diagram.png";
    private void HandleLabelEditingKeyboard(KeyboardState keyboard)
    {
        switch (_textBoxController.HandleKeyboard(keyboard, _previousKeyboard))
        {
            case TextBoxKeyboardAction.Commit:
                CommitLabelEdit();
                break;
            case TextBoxKeyboardAction.Cancel:
                CancelLabelEdit();
                break;
        }
    }
    private void BeginLabelEdit(DiagramNode node)
    {
        _editingNode = node;
        _editingTransition = null;
        _textBoxController.Begin(node.Label);
        _draggedNode = null;
        _resizedNode = null;
        _linkSource = null;
        _status = "状態ラベルを編集中です。Enterで確定、Escでキャンセルします。";
    }
    private void BeginLabelEdit(DiagramTransition transition)
    {
        BeginTransitionLabelEdit(transition);
    }

    private void BeginTransitionLabelEdit(DiagramTransition transition)
    {
        if (!CanTransitionHaveEvent(transition))
        {
            _status = "開始マークから最初の状態へ入る遷移にはイベントを付けられません。";
            return;
        }

        _editingNode = null;
        _editingTransition = transition;
        _textBoxController.Begin(transition.Label);
        _draggedNode = null;
        _resizedNode = null;
        _linkSource = null;
        _status = "遷移ラベルを編集中です。Enterで確定、Escでキャンセルします。";
    }
    private void CommitLabelEdit()
    {
        var label = _textBoxController.Text.Trim();
        if (_editingNode is not null)
        {
            var newLabel = string.IsNullOrWhiteSpace(label) ? $"状態{_editingNode.Id}" : label;
            if (_editingNode.Label != newLabel)
            {
                ExecuteUndoableChange(() => _editingNode.Label = newLabel);
            }
            _status = "状態ラベルを更新しました。Ctrl+Sで保存できます。";
        }
        else if (_editingTransition is not null)
        {
            if (!CanTransitionHaveEvent(_editingTransition))
            {
                label = string.Empty;
            }

            if (_editingTransition.Label != label)
            {
                ExecuteUndoableChange(() => _editingTransition.Label = label);
            }
            _status = CanTransitionHaveEvent(_editingTransition)
                ? "遷移ラベルを更新しました。Tabでラベル左右を切り替えられます。"
                : "開始マークから最初の状態へ入る遷移にはイベントを付けられません。";
        }
        _editingNode = null;
        _editingTransition = null;
        _textBoxController.Clear();
    }
    private void CancelLabelEdit()
    {
        _editingNode = null;
        _editingTransition = null;
        _textBoxController.Clear();
        _status = "ラベル編集をキャンセルしました。";
    }
    private void ToggleTransitionLabelSide(DiagramTransition transition)
    {
        ExecuteUndoableChange(() =>
        {
            transition.LabelSide = transition.LabelSide == 0 ? 1 : 0;
        });
        _status = "遷移ラベルを左右で切り替えました。";
    }
    private void HandleMouse(KeyboardState keyboard, MouseState mouse)
    {
        var screenMousePosition = mouse.Position.ToVector2();
        var mousePosition = ScreenToWorld(screenMousePosition);
        var leftPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;
        var leftReleased = mouse.LeftButton == ButtonState.Released && _previousMouse.LeftButton == ButtonState.Pressed;
        var shiftDown = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
        var snapNodes = !IsAltDown(keyboard);
        if (leftPressed)
        {
            if (_yukaiLarkAssistant.ShouldRunFromMouse(CreateAssistantContext(), mouse.Position, out var assistKind))
            {
                RunYukaiLarkAssist(assistKind);
                return;
            }

            _isPanning = false;
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
                BeginPendingHistory();
                UpdateTransitionHandle(handle.Transition, handle.Kind, mousePosition);
                _status = handle.Kind is TransitionHandleKind.SourceEndpoint or TransitionHandleKind.TargetEndpoint
                    ? "遷移の接点を円周上で移動中です。"
                    : "遷移の曲がり方を調整中です。";
                return;
            }

            var node = FindNodeAt(mousePosition);
            _selectedNode = node;
            _selectedTransition = node is null ? FindTransitionAt(mousePosition) : null;
            if (_selectedTransition is not null)
            {
                _status = CanTransitionHaveEvent(_selectedTransition)
                    ? "遷移を選択しました。F2・Enterでラベル編集、Tabでラベル左右切替、Deleteで削除できます。"
                    : "開始マークから最初の状態へ入る遷移にはイベントを付けられません。";
            }
            if (shiftDown && node is not null)
            {
                _linkSource = node;
                _status = "遷移を作成中です。接続先の状態でマウスを離してください。";
                return;
            }
            if (node is not null)
            {
                _draggedNode = node;
                _dragOffset = mousePosition - node.Position;
                BeginPendingHistory();
                _status = "状態を選択しました。F2・Enterでラベル編集、Tで開始マーク切替。";
            }
            else if (_selectedTransition is null)
            {
                _isPanning = true;
                _panStartMouse = screenMousePosition;
                _panStartCamera = _cameraOffset;
                _linkSource = null;
                _status = "表示位置を移動中です。マウスを離すと停止します。";
            }
        }
        if (_draggedHandleTransition is not null && mouse.LeftButton == ButtonState.Pressed)
        {
            UpdateTransitionHandle(_draggedHandleTransition, _draggedHandleKind, mousePosition);
        }
        if (_draggedNode is not null && mouse.LeftButton == ButtonState.Pressed)
        {
            var position = mousePosition - _dragOffset;
            _draggedNode.Position = snapNodes ? SnapToHalfGrid(position) : position;
        }
        if (_resizedNode is not null && mouse.LeftButton == ButtonState.Pressed)
        {
            UpdateNodeRadius(_resizedNode, mousePosition);
        }
        if (_isPanning && mouse.LeftButton == ButtonState.Pressed)
        {
            _cameraOffset = _panStartCamera + screenMousePosition - _panStartMouse;
        }
        if (leftReleased)
        {
            if (_linkSource is not null)
            {
                var target = FindNodeAt(mousePosition);
                if (target is not null)
                {
                    var transitionCount = _transitions.Count;
                    AddTransition(_linkSource.Id, target.Id);
                    if (_transitions.Count > transitionCount)
                    {
                        _selectedNode = null;
                        _selectedTransition = _transitions.LastOrDefault();
                        _status = _selectedTransition is not null && CanTransitionHaveEvent(_selectedTransition)
                            ? "遷移を作成しました。ハンドルで形を調整、F2・Enterでラベル編集。"
                            : "開始マークから最初の状態へ入る遷移を作成しました。この遷移にはイベントを付けられません。";
                    }
                }
                _linkSource = null;
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
            _resizedNode = null;
            _draggedHandleTransition = null;
            _draggedHandleKind = TransitionHandleKind.None;
            if (_isPanning)
            {
                _isPanning = false;
                _status = "表示位置を移動しました。空白をドラッグするとまた移動できます。";
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
        if (_isPanning)
        {
            return MouseCursor.SizeAll;
        }

        if (_isFileMenuOpen || _isExportSelecting || IsEditingLabel)
        {
            return MouseCursor.Arrow;
        }

        if (CanPanFromMousePosition(keyboard, mouse))
        {
            return MouseCursor.Hand;
        }

        return MouseCursor.Arrow;
    }

    private bool CanPanFromMousePosition(KeyboardState keyboard, MouseState mouse)
    {
        if (mouse.LeftButton == ButtonState.Pressed
            || _draggedNode is not null
            || _resizedNode is not null
            || _draggedHandleTransition is not null
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
        _cameraOffset = screenCenter - worldPosition;
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
    private void AddTransition(int sourceId, int targetId)
    {
        var source = FindNode(sourceId);
        var target = FindNode(targetId);
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
    private void SaveDiagram()
    {
        if (_currentFilePath is null)
        {
            SaveDiagramAs();
            return;
        }
        SaveDiagramToPath(_currentFilePath);
    }
    private void SaveDiagramAs()
    {
        var dialog = new SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = "json",
            FileName = _currentFilePath is null ? CreateDefaultDiagramFileName() : Path.GetFileName(_currentFilePath),
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = GetInitialDirectory(),
            OverwritePrompt = true,
            Title = "保存先を指定"
        };
        if (dialog.ShowDialog() != true)
        {
            _status = "保存をキャンセルしました。";
            return;
        }
        SaveDiagramToPath(dialog.FileName);
    }
    private void SaveDiagramToPath(string path)
    {
        var document = new DiagramDocument { Nodes = _nodes, Transitions = _transitions };
        YukaiDialogJsonWriter.Write(path, document);
        _currentFilePath = path;
        RememberDiagramFile(path);
        _status = $"{Path.GetFileName(path)} を保存しました。";
    }
    private void LoadDiagramFromDialog()
    {
        var dialog = new OpenFileDialog
        {
            AddExtension = true,
            DefaultExt = "json",
            FileName = _currentFilePath is null ? string.Empty : Path.GetFileName(_currentFilePath),
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = GetInitialDirectory(),
            Multiselect = false,
            Title = "状態遷移図を読み込む"
        };
        if (dialog.ShowDialog() != true)
        {
            _status = "読込をキャンセルしました。";
            return;
        }
        LoadDiagramFromPath(dialog.FileName);
    }
    private void LoadDiagramFromPath(string path)
    {
        if (!File.Exists(path))
        {
            _status = $"{Path.GetFileName(path)} が見つかりません。";
            return;
        }
        var document = YukaiDialogJsonReader.Read(path);
        if (document is null)
        {
            _status = "状態遷移図を読み込めませんでした。";
            return;
        }
        _nodes.Clear();
        _transitions.Clear();
        _nodes.AddRange(document.Nodes);
        _transitions.AddRange(document.Transitions);
        foreach (var transition in _transitions)
        {
            InitializeTransitionEndpoints(transition);
        }
        _nextNodeId = _nodes.Count == 0 ? 1 : _nodes.Max(n => n.Id) + 1;
        _selectedNode = null;
        _selectedTransition = null;
        _currentFilePath = path;
        ClearHistory();
        RememberDiagramFile(path);
        _status = $"{Path.GetFileName(path)} を読み込みました。";
    }
    /// <summary>
    /// 新規ファイル作成
    /// </summary>
    private void CreateNewDiagram()
    {
        ExecuteUndoableChange(() =>
        {
            ClearDiagram();
            AddInitialMarkers();
        });
        _currentFilePath = null;
        _status = "開始マークと終了マーク付きの状態遷移図を新規作成しました。Ctrl+Sで保存先を指定できます。";
    }

    private void AddInitialMarkers()
    {
        var viewport = GraphicsDevice.Viewport;
        var centerX = viewport.Width / 2f;
        var centerY = viewport.Height / 2f;
        var verticalOffset = 120f;

        _nodes.Add(new DiagramNode
        {
            Id = _nextNodeId++,
            Label = "開始",
            Position = SnapToHalfGrid(new Vector2(centerX, centerY - verticalOffset)),
            RadiusUnits = DiagramNode.TerminalRadiusUnits,
            ColorIndex = 0,
            Kind = NodeKind.StartMarker
        });

        _nodes.Add(new DiagramNode
        {
            Id = _nextNodeId++,
            Label = "終了",
            Position = SnapToHalfGrid(new Vector2(centerX, centerY + verticalOffset)),
            RadiusUnits = DiagramNode.TerminalRadiusUnits,
            ColorIndex = 0,
            Kind = NodeKind.EndMarker
        });

        _selectedNode = null;
        _selectedTransition = null;
    }

    /// <summary>
    /// ダイアグラムのクリアー
    /// </summary>
    private void ClearDiagram()
    {
        _nodes.Clear();
        _transitions.Clear();
        _nextNodeId = 1;
        _selectedNode = null;
        _selectedTransition = null;
        _draggedNode = null;
        _linkSource = null;
        _editingNode = null;
        _editingTransition = null;
        _draggedHandleTransition = null;
        _draggedHandleKind = TransitionHandleKind.None;
        _resizedNode = null;
        _textBoxController.Clear();
        _pendingHistorySnapshot = null;
        _cameraOffset = Vector2.Zero;
        _isPanning = false;
        _isExportSelecting = false;
        _exportSelectionDragging = false;
        _hasExportSelection = false;
        _exportSelectionRectangle = Rectangle.Empty;
        _exportDragStartRectangle = Rectangle.Empty;
        _exportDragMode = ExportSelectionDragMode.New;
        _status = DefaultStatus;
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

    private DiagramTransition? FindTransitionAt(Vector2 position)
    {
        foreach (var transition in _transitions)
        {
            if (!TryGetTransitionGeometry(transition, out var start, out var control1, out var control2, out var end))
            {
                continue;
            }

            if (DistanceToBezier(position, start, control1, control2, end) <= 8f)
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
    private DiagramNode? FindNode(int id) => _nodes.FirstOrDefault(n => n.Id == id);
    private Matrix GetViewMatrix()
        => Matrix.CreateTranslation(_cameraOffset.X, _cameraOffset.Y, 0f);
    private Vector2 ScreenToWorld(Vector2 screenPosition)
        => screenPosition - _cameraOffset;
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
    private void DrawHoverCue()
    {
        if (IsEditingLabel || _draggedNode is not null || _resizedNode is not null || _draggedHandleTransition is not null || _isPanning || _linkSource is not null)
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
            if (node != _selectedNode)
            {
                _primitiveRenderer.DrawCircleOutline(node.Position, node.Radius + 8f, new Color(130, 185, 230), 3f);
            }
            return;
        }

        var transition = FindTransitionAt(mouseWorld);
        if (transition is not null && transition != _selectedTransition && TryGetTransitionGeometry(transition, out start, out control1, out control2, out end))
        {
            _edgeRenderer.DrawTransitionHoverCue(start, control1, control2, end);
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
                    showEditingCaret);
            }
        }
        if (includeInteraction)
        {
            DrawTransitionEventGhost(totalGameTime);
        }
        if (includeInteraction)
        {
            DrawHoverCue();
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
        foreach (var node in _nodes)
        {
            _nodeRenderer.DrawNode(node, includeInteraction && node == _selectedNode, _editingNode, editingDisplayLabel, editingDisplayCaretIndex, showEditingCaret);
        }
        if (includeInteraction && _selectedNode is not null)
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
            && !_transitions.Any(t => t.SourceId == startMarker.Id && t.TargetId == normalNodes[0].Id))
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

        var normalToEndSource = startMarker is null
            ? normalNodes.LastOrDefault()
            : normalNodes
                .OrderByDescending(node => (node.Position - startMarker.Position).LengthSquared())
                .ThenBy(node => node.Id)
                .FirstOrDefault();
        if (endMarker is not null
            && normalToEndSource is not null
            && !_transitions.Any(t => t.SourceId == normalToEndSource.Id && t.TargetId == endMarker.Id))
        {
            source = normalToEndSource;
            target = endMarker;
            return true;
        }

        source = null!;
        target = null!;
        return false;
    }
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

    private void DrawExportSelectionOverlay()
    {
        if (!_isExportSelecting || (!_hasExportSelection && !_exportSelectionDragging))
        {
            return;
        }

        var rectangle = GetExportSelectionRectangle();
        if (rectangle.Width <= 0 || rectangle.Height <= 0)
        {
            return;
        }

        DrawExportPhotoFrame(new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), rectangle, fillBackdrop: false, fillImageArea: false);
        DrawExportPhotoTop(rectangle);
        DrawScreenRectangleOutline(rectangle, new Color(255, 236, 150), 2);
        DrawExportSelectionHandles(rectangle);
        DrawExportSelectionInstruction(rectangle);
    }

    private void UpdateExportPhotoEffect(GameTime gameTime)
    {
        var elapsedSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _exportFlashSecondsRemaining = MathF.Max(0f, _exportFlashSecondsRemaining - elapsedSeconds);
        _exportPhotoPreviewSecondsRemaining = MathF.Max(0f, _exportPhotoPreviewSecondsRemaining - elapsedSeconds);
    }

    private void StartExportPhotoEffect(Rectangle selection)
    {
        _exportPhotoPreviewRectangle = selection;
        _exportFlashSecondsRemaining = ExportFlashDurationSeconds;
        _exportPhotoPreviewSecondsRemaining = ExportPhotoPreviewDurationSeconds;
    }

    private void DrawExportPhotoEffectOverlay()
    {
        if (_exportPhotoPreviewSecondsRemaining > 0f && _exportPhotoPreviewRectangle.Width > 0 && _exportPhotoPreviewRectangle.Height > 0)
        {
            var previewAlpha = MathHelper.Clamp(_exportPhotoPreviewSecondsRemaining / ExportPhotoPreviewDurationSeconds, 0f, 1f);
            DrawExportPhotoFrame(new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), _exportPhotoPreviewRectangle, fillBackdrop: false, fillImageArea: false);
            DrawExportPhotoTop(_exportPhotoPreviewRectangle);
            DrawScreenRectangleOutline(_exportPhotoPreviewRectangle, new Color((byte)255, (byte)236, (byte)150, (byte)(190 * previewAlpha)), 2);
        }

        if (_exportFlashSecondsRemaining <= 0f)
        {
            return;
        }

        var flashAlpha = MathHelper.Clamp(_exportFlashSecondsRemaining / ExportFlashDurationSeconds, 0f, 1f);
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), new Color((byte)255, (byte)255, (byte)255, (byte)(210 * flashAlpha)));
    }

    private void DrawExportSelectionInstruction(Rectangle rectangle)
    {
        const string instruction = "ドラッグで調整 / Enterで撮影 / Escでキャンセル";
        var textWidth = GetUiTextTexture(instruction, 15, true).Width;
        var width = (int)MathF.Ceiling(textWidth) + 28;
        var height = 34;
        var x = Math.Clamp(rectangle.X + rectangle.Width / 2 - width / 2, 12, GraphicsDevice.Viewport.Width - width - 12);
        var y = rectangle.Bottom + ExportPhotoPaperBottomPadding + 14;
        if (y + height > GraphicsDevice.Viewport.Height - 58)
        {
            y = rectangle.Y - ExportPhotoPaperTopPadding - height - 12;
        }

        y = Math.Clamp(y, 62, GraphicsDevice.Viewport.Height - height - 58);
        var panel = new Rectangle(x, y, width, height);
        _spriteBatch.Draw(_pixel, panel, new Color(42, 36, 30, 220));
        DrawScreenRectangleOutline(panel, new Color(255, 236, 150, 180), 1);
        DrawUiText(instruction, new Vector2(panel.X + 14, panel.Y + 7), new Color(255, 246, 210), 15, true);
    }
    private void DrawExportSelectionHandles(Rectangle rectangle)
    {
        var points = new[]
        {
            new Vector2(rectangle.Left, rectangle.Top),
            new Vector2(rectangle.Left + rectangle.Width / 2f, rectangle.Top),
            new Vector2(rectangle.Right, rectangle.Top),
            new Vector2(rectangle.Right, rectangle.Top + rectangle.Height / 2f),
            new Vector2(rectangle.Right, rectangle.Bottom),
            new Vector2(rectangle.Left + rectangle.Width / 2f, rectangle.Bottom),
            new Vector2(rectangle.Left, rectangle.Bottom),
            new Vector2(rectangle.Left, rectangle.Top + rectangle.Height / 2f)
        };

        foreach (var point in points)
        {
            DrawScreenHandle(point);
        }
    }

    private void DrawScreenHandle(Vector2 center)
    {
        var bounds = new Rectangle((int)MathF.Round(center.X) - 5, (int)MathF.Round(center.Y) - 5, 10, 10);
        _spriteBatch.Draw(_pixel, bounds, new Color(255, 236, 150));
        DrawScreenRectangleOutline(bounds, new Color(48, 38, 28), 1);
    }
    private void DrawExportPhotoFrame(Rectangle canvas, Rectangle imageArea, bool fillBackdrop, bool fillImageArea)
    {
        if (fillBackdrop)
        {
            _spriteBatch.Draw(_pixel, canvas, _boardTheme.ExportBackdropColor);
        }

        var shadow = GetExportPhotoShadowRectangle(imageArea);
        _spriteBatch.Draw(_pixel, shadow, new Color(20, 14, 10, 88));
        DrawExportPhotoPaper(imageArea, fillImageArea);
        if (fillImageArea)
        {
            _spriteBatch.Draw(_pixel, imageArea, _boardTheme.BackgroundColor);
        }
    }

    private void DrawExportPhotoPaper(Rectangle imageArea, bool fillImageArea)
    {
        var paper = GetExportPhotoPaperRectangle(imageArea);
        if (fillImageArea)
        {
            _spriteBatch.Draw(_pixel, paper, _boardTheme.PhotoPaperColor);
            return;
        }

        DrawRectangleIfPositive(new Rectangle(paper.X, paper.Y, paper.Width, imageArea.Y - paper.Y), _boardTheme.PhotoPaperColor);
        DrawRectangleIfPositive(new Rectangle(paper.X, imageArea.Y, imageArea.X - paper.X, imageArea.Height), _boardTheme.PhotoPaperColor);
        DrawRectangleIfPositive(new Rectangle(imageArea.Right, imageArea.Y, paper.Right - imageArea.Right, imageArea.Height), _boardTheme.PhotoPaperColor);
        DrawRectangleIfPositive(new Rectangle(paper.X, imageArea.Bottom, paper.Width, paper.Bottom - imageArea.Bottom), _boardTheme.PhotoPaperColor);
    }

    private void DrawRectangleIfPositive(Rectangle rectangle, Color color)
    {
        if (rectangle.Width > 0 && rectangle.Height > 0)
        {
            _spriteBatch.Draw(_pixel, rectangle, color);
        }
    }
    private void DrawExportPhotoTop(Rectangle imageArea)
    {
        var paper = GetExportPhotoPaperRectangle(imageArea);
        DrawScreenRectangleOutline(paper, _boardTheme.PhotoEdgeColor, 2);
        DrawScreenRectangleOutline(imageArea, new Color(130, 120, 108, 120), 1);
        DrawPin(new Vector2(paper.X + 26, paper.Y + 20), _boardTheme.PinColor);
        DrawPin(new Vector2(paper.Right - 26, paper.Y + 20), _boardTheme.PinColor);
    }

    private static Rectangle GetExportPhotoPaperRectangle(Rectangle imageArea)
        => new(
            imageArea.X - ExportPhotoPaperSidePadding,
            imageArea.Y - ExportPhotoPaperTopPadding,
            imageArea.Width + ExportPhotoPaperSidePadding * 2,
            imageArea.Height + ExportPhotoPaperTopPadding + ExportPhotoPaperBottomPadding);

    private static Rectangle GetExportPhotoShadowRectangle(Rectangle imageArea)
    {
        var paper = GetExportPhotoPaperRectangle(imageArea);
        return new Rectangle(paper.X + 4, paper.Y + 8, paper.Width + 8, paper.Height + 8);
    }

    private void DrawPin(Vector2 center, Color color)
    {
        _primitiveRenderer.DrawCircle(center + new Vector2(2, 3), 10f, new Color(20, 14, 12, 95));
        _primitiveRenderer.DrawCircle(center, 9f, color);
        _primitiveRenderer.DrawCircleOutline(center, 9f, new Color(80, 42, 36, 170), 2f);
        _primitiveRenderer.DrawCircle(center - new Vector2(3, 3), 3f, new Color(255, 244, 220, 185));
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
            DrawScreenRectangleOutline,
            DrawUiText);
    }

    private void DrawInspectorPanel()
    {
        _inspectorPanelRenderer.DrawInspectorPanel(
            GraphicsDevice.Viewport,
            _nodes.Count,
            _transitions.Count,
            GetSelectionSummary(),
            _boardTheme);
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

    private bool CanTransitionHaveEvent(DiagramTransition transition)
    {
        var source = FindNode(transition.SourceId);
        var target = FindNode(transition.TargetId);
        return source?.Kind != NodeKind.StartMarker || target?.Kind != NodeKind.Normal;
    }
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
    private bool IsNewKeyPress(KeyboardState keyboard, Keys key)
        => keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
    private static bool IsControlDown(KeyboardState keyboard)
        => keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl);
    private static bool IsShiftDown(KeyboardState keyboard)
        => keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
    private static bool IsAltDown(KeyboardState keyboard)
        => keyboard.IsKeyDown(Keys.LeftAlt) || keyboard.IsKeyDown(Keys.RightAlt);
    private string GetInitialDirectory()
    {
        if (TryGetExistingDirectory(_currentFilePath, out var currentDirectory))
        {
            return currentDirectory;
        }

        foreach (var recentFile in _appConfig.RecentFiles)
        {
            if (TryGetExistingDirectory(recentFile, out var recentDirectory))
            {
                return recentDirectory;
            }
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    private void RememberDiagramFile(string path)
    {
        _appConfig.AddRecentFile(path);
        AppConfigStore.Save(_appConfig);
    }

    private static bool TryGetExistingDirectory(string? filePath, out string directory)
    {
        directory = string.Empty;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        var candidate = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(candidate) || !Directory.Exists(candidate))
        {
            return false;
        }

        directory = candidate;
        return true;
    }
    private static string CreateDefaultDiagramFileName()
        => $"{DateTime.Now:yyyyMMddHHmmss}_dialog.json";
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
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; set; } = CurrentFormatVersion;
    public List<DiagramNode> Nodes { get; set; } = new();
    public List<DiagramTransition> Transitions { get; set; } = new();
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
    public float? SourceAngle { get; set; }
    public float? TargetAngle { get; set; }
    public Vector2? ControlPoint1 { get; set; }
    public Vector2? ControlPoint2 { get; set; }
}
public enum ExportSelectionDragMode
{
    None,
    New,
    Move,
    Left,
    Right,
    Top,
    Bottom,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}
public sealed record TransitionHandleHit(DiagramTransition? Transition, TransitionHandleKind Kind);
public enum TransitionHandleKind
{
    None,
    SourceEndpoint,
    TargetEndpoint,
    ControlPoint1,
    ControlPoint2
}
public sealed record BoardTheme(
    Color BackgroundColor,
    Color GridColor,
    Color ExportBackdropColor,
    Color PhotoPaperColor,
    Color PhotoEdgeColor,
    Color PinColor,
    Color TransitionLineColor,
    Color TransitionLabelColor,
    Color SelectedTransitionLineColor,
    Color SelectedTransitionLabelColor,
    Color TransitionHandleColor,
    Color TransitionControlHandleColor,
    Color TransitionGuideColor)
{
    private bool IsLightBackground => GetLuminance(BackgroundColor) >= 0.58f;

    public Color HeaderBackgroundColor => IsLightBackground
        ? WithAlpha(PhotoPaperColor, 238)
        : WithAlpha(Blend(BackgroundColor, Color.Black, 0.18f), 238);
    public Color HeaderBorderColor => WithAlpha(IsLightBackground ? PhotoEdgeColor : GridColor, 220);
    public Color HeaderTitleTextColor => IsLightBackground ? TransitionLabelColor : PhotoPaperColor;
    public Color HeaderStatusTextColor => IsLightBackground
        ? Blend(TransitionLabelColor, BackgroundColor, 0.18f)
        : TransitionLabelColor;

    public Color PanelBackgroundColor => IsLightBackground
        ? WithAlpha(PhotoPaperColor, 226)
        : WithAlpha(Blend(BackgroundColor, Color.Black, 0.10f), 224);
    public Color PanelTopEdgeColor => WithAlpha(IsLightBackground ? PinColor : GridColor, 210);
    public Color PanelBottomEdgeColor => WithAlpha(Blend(BackgroundColor, Color.Black, 0.36f), 220);
    public Color PanelPrimaryTextColor => HeaderTitleTextColor;
    public Color PanelSecondaryTextColor => HeaderStatusTextColor;
    public Color PanelMutedTextColor => IsLightBackground
        ? Blend(TransitionLabelColor, BackgroundColor, 0.34f)
        : Blend(TransitionLabelColor, BackgroundColor, 0.18f);

    public Color BottomBarBackgroundColor => IsLightBackground
        ? WithAlpha(PhotoPaperColor, 220)
        : WithAlpha(Blend(BackgroundColor, Color.Black, 0.14f), 218);

    public Color AssistantBubbleColor => IsLightBackground
        ? WithAlpha(PhotoPaperColor, 238)
        : WithAlpha(Blend(BackgroundColor, GridColor, 0.58f), 238);
    public Color AssistantBubbleBorderColor => WithAlpha(IsLightBackground ? PinColor : TransitionLineColor, 224);
    public Color AssistantCompletedBubbleBorderColor => WithAlpha(IsLightBackground ? PhotoEdgeColor : SelectedTransitionLineColor, 232);
    public Color AssistantTitleTextColor => IsLightBackground ? TransitionLabelColor : SelectedTransitionLabelColor;
    public Color AssistantBodyTextColor => IsLightBackground ? PanelSecondaryTextColor : Blend(SelectedTransitionLabelColor, TransitionLabelColor, 0.28f);
    public Color AssistantHintTextColor => IsLightBackground ? PanelMutedTextColor : Blend(SelectedTransitionLabelColor, BackgroundColor, 0.26f);

    private static float GetLuminance(Color color)
        => ((0.2126f * color.R) + (0.7152f * color.G) + (0.0722f * color.B)) / 255f;

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
}

public static class BoardThemes
{
    public static BoardTheme ForKeyCapTheme(IKeyCapTheme keyCapTheme)
        => keyCapTheme.Name switch
        {
            "YukaiLark" => new BoardTheme(new Color(238, 250, 239), new Color(178, 219, 203), new Color(222, 244, 233), new Color(255, 253, 239), new Color(233, 188, 96), new Color(83, 178, 176), new Color(63, 98, 116), new Color(51, 84, 102), new Color(255, 230, 120), new Color(255, 242, 201), new Color(83, 178, 176), new Color(80, 190, 230), new Color(116, 138, 152)),
            "Gaming" => new BoardTheme(new Color(18, 20, 28), new Color(42, 88, 96), new Color(15, 18, 24), new Color(230, 236, 232), new Color(78, 104, 108), new Color(88, 232, 206), new Color(102, 245, 255), new Color(210, 255, 251), new Color(255, 230, 120), new Color(255, 246, 214), new Color(88, 232, 206), new Color(80, 190, 230), new Color(60, 120, 130)),
            "Retro" => new BoardTheme(new Color(116, 82, 52), new Color(149, 109, 70), new Color(98, 65, 40), new Color(241, 229, 198), new Color(155, 125, 82), new Color(190, 54, 44), new Color(244, 222, 184), new Color(255, 238, 206), new Color(255, 230, 120), new Color(255, 245, 224), new Color(190, 54, 44), new Color(80, 190, 230), new Color(165, 132, 98)),
            "CopyPaper" => new BoardTheme(new Color(226, 229, 224), new Color(198, 205, 202), new Color(190, 185, 174), new Color(252, 250, 242), new Color(190, 184, 172), new Color(60, 112, 178), new Color(52, 82, 128), new Color(28, 54, 92), new Color(255, 160, 96), new Color(42, 76, 124), new Color(60, 112, 178), new Color(80, 190, 230), new Color(104, 128, 148)),
            "Girly" => new BoardTheme(new Color(67, 47, 62), new Color(119, 78, 104), new Color(92, 62, 78), new Color(255, 236, 240), new Color(205, 142, 162), new Color(232, 92, 132), new Color(255, 176, 214), new Color(255, 229, 240), new Color(255, 230, 120), new Color(255, 244, 248), new Color(232, 92, 132), new Color(80, 190, 230), new Color(170, 112, 150)),
            "Edo" => new BoardTheme(new Color(36, 45, 50), new Color(70, 86, 82), new Color(41, 35, 30), new Color(238, 231, 207), new Color(123, 88, 54), new Color(178, 48, 44), new Color(222, 196, 149), new Color(248, 229, 187), new Color(255, 230, 120), new Color(250, 239, 212), new Color(178, 48, 44), new Color(80, 190, 230), new Color(135, 118, 84)),
            "Hokusai" => new BoardTheme(new Color(239, 234, 211), new Color(190, 205, 211), new Color(224, 231, 224), new Color(252, 247, 224), new Color(39, 75, 136), new Color(196, 64, 48), new Color(31, 67, 132), new Color(18, 48, 102), new Color(202, 58, 48), new Color(117, 36, 42), new Color(196, 64, 48), new Color(64, 132, 184), new Color(111, 133, 148)),
            "Monochrome" => new BoardTheme(new Color(34, 34, 34), new Color(68, 68, 68), new Color(24, 24, 24), new Color(235, 235, 228), new Color(120, 120, 114), new Color(210, 210, 210), new Color(230, 230, 230), new Color(250, 250, 250), new Color(255, 220, 90), new Color(255, 252, 236), new Color(210, 210, 210), new Color(80, 190, 230), new Color(160, 160, 160)),
            "Mint" => new BoardTheme(new Color(29, 61, 57), new Color(63, 104, 96), new Color(46, 82, 74), new Color(235, 250, 239), new Color(126, 170, 152), new Color(76, 198, 157), new Color(152, 223, 199), new Color(211, 251, 238), new Color(255, 230, 120), new Color(234, 255, 245), new Color(76, 198, 157), new Color(80, 190, 230), new Color(114, 176, 156)),
            "Amber" => new BoardTheme(new Color(66, 49, 34), new Color(112, 83, 48), new Color(86, 61, 35), new Color(248, 229, 188), new Color(176, 126, 56), new Color(225, 151, 48), new Color(247, 221, 156), new Color(255, 242, 200), new Color(255, 230, 120), new Color(255, 247, 221), new Color(225, 151, 48), new Color(80, 190, 230), new Color(177, 134, 84)),
            "Midnight" => new BoardTheme(new Color(17, 24, 34), new Color(37, 52, 68), new Color(14, 19, 28), new Color(230, 234, 232), new Color(90, 102, 116), new Color(96, 154, 232), new Color(118, 181, 255), new Color(214, 233, 255), new Color(255, 230, 120), new Color(245, 250, 255), new Color(96, 154, 232), new Color(80, 190, 230), new Color(92, 118, 146)),
            _ => new BoardTheme(new Color(28, 31, 36), new Color(42, 46, 52), new Color(104, 73, 48), new Color(244, 236, 218), new Color(150, 132, 106), new Color(190, 54, 44), new Color(234, 181, 128), new Color(255, 233, 209), new Color(255, 230, 120), new Color(255, 244, 224), new Color(190, 54, 44), new Color(80, 190, 230), new Color(146, 126, 104))
        };
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
