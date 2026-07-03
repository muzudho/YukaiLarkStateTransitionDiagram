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
    private const string YukaiLarkMascotLightThemeTexturePath = "Assets/BrandLogo/yukai-lark-logo-light-theme.png";
    private const string YukaiLarkMascotDarkThemeTexturePath = "Assets/BrandLogo/yukai-lark-logo-dark-theme.png";
    private readonly GraphicsDeviceManager _graphics;
    private readonly List<DiagramInstance> _diagrams = new() { DiagramInstance.CreateDefault() };
    private readonly Dictionary<string, Texture2D> _labelTextureCache = new();
    private readonly Dictionary<string, Texture2D> _uiTextTextureCache = new();
    private PrimitiveRenderer _primitiveRenderer = null!;
    private EdgeRenderer _edgeRenderer = null!;
    private NodeRenderer _nodeRenderer = null!;
    private HeaderRenderer _headerRenderer = null!;
    private DiagramTabRenderer _diagramTabRenderer = null!;
    private InspectorPanelRenderer _inspectorPanelRenderer = null!;
    private MiniMapRenderer _miniMapRenderer = null!;
    private ShortcutKeyRenderer _shortcutKeyRenderer = null!;
    private SpriteBatch _spriteBatch = null!;
    private Texture2D _pixel = null!;
    private Texture2D? _yukaiLarkMascotLightThemeTexture;
    private Texture2D? _yukaiLarkMascotDarkThemeTexture;
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
    private bool _isImeEnabledForTextInput = true;
    private int _currentDiagramIndex;
    private string _status = DefaultStatus;
    private string _fileNameEditWarning = string.Empty;
    private const string DefaultStatus = "N: 状態追加 / S: 開始マーク / ホイール: 拡大縮小 / T: テーマ選択 / Shift+ドラッグ: 遷移作成 / F2・Enter: ラベル編集 / Ctrl+Z/Y: 元に戻す/やり直し / Ctrl+S: 保存";
    private DiagramInstance CurrentDiagram => _diagrams[_currentDiagramIndex];
    private List<DiagramNode> _nodes => CurrentDiagram.Nodes;
    private List<DiagramTransition> _transitions => CurrentDiagram.Transitions;
    private AssistSuppressionSection _assistSuppression => CurrentDiagram.AssistSuppression;
    private int _nextNodeId
    {
        get => CurrentDiagram.NextNodeId;
        set => CurrentDiagram.NextNodeId = Math.Max(1, value);
    }

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
        ApplyConfiguredTheme();
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
        _diagramTabRenderer = new DiagramTabRenderer(GraphicsDevice, _spriteBatch, _pixel);
        _inspectorPanelRenderer = new InspectorPanelRenderer(GraphicsDevice, _spriteBatch, _pixel, _primitiveRenderer);
        _miniMapRenderer = new MiniMapRenderer(_spriteBatch, _pixel, _primitiveRenderer);
        _shortcutKeyRenderer = new ShortcutKeyRenderer(GraphicsDevice, _spriteBatch, _pixel, _keyCapTheme, _boardTheme);
        _nodeRenderer = new NodeRenderer(_primitiveRenderer, _spriteBatch, GetLabelTexture, _boardTheme);
        _yukaiLarkMascotLightThemeTexture = LoadTextureWithTransparentWhite(YukaiLarkMascotLightThemeTexturePath);
        _yukaiLarkMascotDarkThemeTexture = LoadTextureWithTransparentWhite(YukaiLarkMascotDarkThemeTexturePath);
    }
    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        var mouse = Mouse.GetState();
        if (!_isEditingFileName && !IsEditingLabel)
        {
            EnsureImeClosedForShortcutInput();
        }

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

        if (_isColorPaletteOpen)
        {
            HandleColorPaletteKeyboard(keyboard);
            var paletteConsumedMouse = HandleColorPaletteMouse(mouse);
            if (_isColorPaletteOpen && !paletteConsumedMouse)
            {
                HandleMouse(keyboard, mouse);
            }
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
        _diagramTabRenderer.DrawTabs(GraphicsDevice.Viewport, _diagrams, _currentDiagramIndex, _boardTheme);

        // ［開始マーク作成アシスト］の描画
        DrawYukaiLarkMascot(GraphicsDevice.Viewport, gameTime.TotalGameTime);

        DrawInspectorPanel();
        DrawMiniMapOverlay();
        if (!_isThemeMenuOpen)
        {
            DrawBottomShortcutHelp(gameTime);
        }

        DrawColorPaletteOverlay();
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
        _yukaiLarkMascotLightThemeTexture?.Dispose();
        _yukaiLarkMascotDarkThemeTexture?.Dispose();
        _yukaiLarkMascotLightThemeTexture = null;
        _yukaiLarkMascotDarkThemeTexture = null;
        _headerRenderer?.Dispose();
        _diagramTabRenderer?.Dispose();
        _inspectorPanelRenderer?.Dispose();
        _shortcutKeyRenderer?.Dispose();
        base.UnloadContent();
    }
    private bool IsEditingLabel => _editingNode is not null || _editingTransition is not null;

    private void BeginTextInputIme()
    {
        WindowsImeCompositionReader.SetOpen(_isImeEnabledForTextInput);
    }

    private void EndTextInputIme()
    {
        _isImeEnabledForTextInput = WindowsImeCompositionReader.IsOpen();
        EnsureImeClosedForShortcutInput();
    }

    private static void EnsureImeClosedForShortcutInput()
    {
        WindowsImeCompositionReader.SetOpen(false);
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
    private void DrawText(string text, Vector2 position, Color color, int scale)
    {
        PrimitiveText.Draw(_spriteBatch, _pixel, text, position, color, scale);
    }
    private static Color WithAlpha(Color color, byte alpha)
        => new(color.R, color.G, color.B, alpha);

    private Texture2D? GetYukaiLarkMascotTexture()
        => IsLightBoardTheme()
            ? _yukaiLarkMascotLightThemeTexture ?? _yukaiLarkMascotDarkThemeTexture
            : _yukaiLarkMascotDarkThemeTexture ?? _yukaiLarkMascotLightThemeTexture;

    private bool IsLightBoardTheme()
        => GetLuminance(_boardTheme.BackgroundColor) >= 0.58f;

    private static float GetLuminance(Color color)
        => ((0.2126f * color.R) + (0.7152f * color.G) + (0.0722f * color.B)) / 255f;

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
public sealed class DiagramDocument
{
    public const int CurrentFormatVersion = 3;

    public int FormatVersion { get; set; } = CurrentFormatVersion;
    public int ActiveDiagramId { get; set; } = 1;
    public List<DiagramInstance> Diagrams { get; set; } = new();
    public DiagramDataSection Data { get; set; } = new();
    public AssistSuppressionSection AssistSuppression { get; set; } = new();

    [JsonIgnore]
    public DiagramInstance ActiveDiagram
    {
        get
        {
            EnsureDiagrams();
            return Diagrams.FirstOrDefault(diagram => diagram.Id == ActiveDiagramId) ?? Diagrams[0];
        }
    }

    [JsonIgnore]
    public List<DiagramNode> Nodes
    {
        get => ActiveDiagram.Nodes;
        set => ActiveDiagram.Nodes = value ?? new List<DiagramNode>();
    }

    [JsonIgnore]
    public List<DiagramTransition> Transitions
    {
        get => ActiveDiagram.Transitions;
        set => ActiveDiagram.Transitions = value ?? new List<DiagramTransition>();
    }

    public void EnsureDiagrams()
    {
        if (Diagrams.Count == 0)
        {
            Diagrams.Add(new DiagramInstance
            {
                Id = ActiveDiagramId <= 0 ? 1 : ActiveDiagramId,
                Name = "ダイアグラム1",
                Data = Data,
                AssistSuppression = AssistSuppression
            });
        }

        for (var index = 0; index < Diagrams.Count; index++)
        {
            if (Diagrams[index].Id <= 0)
            {
                Diagrams[index].Id = index + 1;
            }
            if (string.IsNullOrWhiteSpace(Diagrams[index].Name))
            {
                Diagrams[index].Name = $"ダイアグラム{index + 1}";
            }
            Diagrams[index].RefreshNextNodeId();
        }

        if (!Diagrams.Any(diagram => diagram.Id == ActiveDiagramId))
        {
            ActiveDiagramId = Diagrams[0].Id;
        }

        var activeDiagram = Diagrams.FirstOrDefault(diagram => diagram.Id == ActiveDiagramId) ?? Diagrams[0];
        Data = activeDiagram.Data;
        AssistSuppression = activeDiagram.AssistSuppression;
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
    public List<Vector2> Waypoints { get; set; } = new();
    public List<TransitionSegmentControls> SegmentControls { get; set; } = new();
}
public sealed class TransitionSegmentControls
{
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
    Vector2 ControlPoint2Offset,
    List<Vector2> WaypointOffsets,
    List<Vector2> WaypointPositions,
    List<TransitionSegmentControlDragSnapshot> SegmentControlSnapshots);
public sealed record TransitionSegmentControlDragSnapshot(
    bool HasControlPoint1,
    bool HasControlPoint2,
    Vector2 ControlPoint1,
    Vector2 ControlPoint2);
public sealed record TransitionHandleHit(DiagramTransition? Transition, TransitionHandleKind Kind, int WaypointIndex = -1);
public enum TransitionHandleKind
{
    None,
    SourceEndpoint,
    TargetEndpoint,
    ControlPoint1,
    ControlPoint2,
    Waypoint,
    SegmentControlPoint1,
    SegmentControlPoint2
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
