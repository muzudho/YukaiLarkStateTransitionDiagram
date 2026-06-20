namespace SkylarkStateTransitionDiagram;
using SkylarkStateTransitionDiagram.Theme;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingBrushes = System.Drawing.Brushes;
using DrawingColor = System.Drawing.Color;
using DrawingFont = System.Drawing.Font;
using DrawingFontFamily = System.Drawing.FontFamily;
using DrawingFontStyle = System.Drawing.FontStyle;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingGraphicsUnit = System.Drawing.GraphicsUnit;
using DrawingRectangleF = System.Drawing.RectangleF;
using DrawingSizeF = System.Drawing.SizeF;
using DrawingStringFormat = System.Drawing.StringFormat;
using DrawingStringFormatFlags = System.Drawing.StringFormatFlags;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
public class Game1 : Game
{
    private const string SaveFileName = "diagram.json";
    private const int ExportPhotoImageMargin = 34;
    private const int ExportPhotoPaperSidePadding = 16;
    private const int ExportPhotoPaperTopPadding = 18;
    private const int ExportPhotoPaperBottomPadding = 54;
    private const int ExportPhotoOuterBottomPadding = 10;
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
    private static readonly UTF8Encoding Utf8NoBom = new(false);
    private static readonly JsonSerializerOptions DiagramJsonOptions = new()
    {
        WriteIndented = true,
        IncludeFields = true
    };
    private readonly GraphicsDeviceManager _graphics;
    private readonly List<DiagramNode> _nodes = new();
    private readonly List<DiagramTransition> _transitions = new();
    private readonly Dictionary<string, Texture2D> _labelTextureCache = new();
    private readonly Dictionary<string, Texture2D> _uiTextTextureCache = new();
    private IKeyCapTheme _keyCapTheme = KeyCapThemes.Current;
    private BoardTheme _boardTheme = BoardThemes.ForKeyCapTheme(KeyCapThemes.Current);
    private SpriteBatch _spriteBatch = null!;
    private Texture2D _pixel = null!;
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
    private Vector2 _dragOffset;
    private Vector2 _cameraOffset;
    private Vector2 _panStartMouse;
    private Vector2 _panStartCamera;
    private bool _isPanning;
    private bool _isExportSelecting;
    private bool _exportSelectionDragging;
    private bool _hasExportSelection;
    private Rectangle _exportSelectionRectangle;
    private Rectangle _exportDragStartRectangle;
    private Vector2 _exportDragStart;
    private ExportSelectionDragMode _exportDragMode;
    private int _nextNodeId = 1;
    private string _editingLabel = string.Empty;
    private string _status = DefaultStatus;
    private const string DefaultStatus = "N: 状態追加 / Shift+ドラッグ: 遷移作成 / F2・Enter: ラベル編集 / Ctrl+S: 保存 / Ctrl+P: PNG";
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
        Window.Title = "Skylark State Transition Diagram";
        Window.TextInput += OnTextInput;
    }
    protected override void Initialize()
    {
        LoadOrCreateSample();
        base.Initialize();
    }
    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }
    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        var mouse = Mouse.GetState();
        if (_isExportSelecting)
        {
            HandleExportSelectionKeyboard(keyboard);
            HandleExportSelectionMouse(mouse);
        }
        else if (IsEditingLabel)
        {
            HandleLabelEditingKeyboard(keyboard);
        }
        else
        {
            if (keyboard.IsKeyDown(Keys.Escape) || GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
            {
                Exit();
            }
            HandleKeyboard(keyboard, mouse);
            HandleMouse(keyboard, mouse);
        }
        _previousKeyboard = keyboard;
        _previousMouse = mouse;
        base.Update(gameTime);
    }
    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(_boardTheme.BackgroundColor);
        DrawDiagramScene(GetViewMatrix(), includeInteraction: true);

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        DrawToolbar();
        DrawInspectorPanel();
        DrawBottomHelp();
        DrawExportSelectionOverlay();
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
        base.UnloadContent();
    }
    private bool IsEditingLabel => _editingNode is not null || _editingTransition is not null;
    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (!IsEditingLabel || char.IsControl(e.Character))
        {
            return;
        }
        if (_editingLabel.Length >= 24)
        {
            _status = "ラベルは24文字までです。";
            return;
        }
        _editingLabel += e.Character;
    }
    private void HandleKeyboard(KeyboardState keyboard, MouseState mouse)
    {
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
        if (IsNewKeyPress(keyboard, Keys.F2) || IsNewKeyPress(keyboard, Keys.Enter))
        {
            if (_selectedNode is not null)
            {
                BeginLabelEdit(_selectedNode);
                return;
            }
            if (_selectedTransition is not null)
            {
                BeginLabelEdit(_selectedTransition);
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
        }
        if (IsNewKeyPress(keyboard, Keys.Delete) || IsNewKeyPress(keyboard, Keys.Back))
        {
            DeleteSelection();
        }
        if (IsNewKeyPress(keyboard, Keys.R))
        {
            CreateSample();
            _status = "サンプル図に戻しました。";
        }
        if (IsNewKeyPress(keyboard, Keys.T) && _selectedNode is not null)
        {
            ToggleNodeKind(_selectedNode);
        }
        if (IsNewKeyPress(keyboard, Keys.C) && _selectedNode is not null)
        {
            if (_selectedNode.Kind == NodeKind.Normal)
            {
                _selectedNode.ColorIndex = (_selectedNode.ColorIndex + 1) % Palette.Length;
                _status = "選択中の状態色を切り替えました。";
            }
            else
            {
                _status = "開始・終了ノードは黒固定です。Tで通常ノードに戻せます。";
            }
        }
    }
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
                _isExportSelecting = false;
                _exportSelectionDragging = false;
                _hasExportSelection = false;
                _exportDragMode = ExportSelectionDragMode.None;
            }
        }
    }

    private void HandleExportSelectionMouse(MouseState mouse)
    {
        var screenPosition = mouse.Position.ToVector2();
        var leftPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;
        var leftReleased = mouse.LeftButton == ButtonState.Released && _previousMouse.LeftButton == ButtonState.Pressed;

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
                _status = "PNG範囲を作成中です。離した後に四辺をドラッグで調整、Enterで撮影。";
            }
            else
            {
                _status = "PNG範囲を調整中です。四辺・角・内側ドラッグで調整、Enterで撮影。";
            }
            return;
        }

        if (_exportSelectionDragging && mouse.LeftButton == ButtonState.Pressed)
        {
            UpdateExportSelectionDrag(screenPosition);
        }

        if (leftReleased && _exportSelectionDragging)
        {
            UpdateExportSelectionDrag(screenPosition);
            _exportSelectionDragging = false;
            _exportDragMode = ExportSelectionDragMode.None;
            if (_exportSelectionRectangle.Width < 16 || _exportSelectionRectangle.Height < 16)
            {
                _hasExportSelection = false;
                _status = "PNG出力範囲が小さすぎます。左ドラッグで範囲を作り直してください。";
                return;
            }

            _hasExportSelection = true;
            _status = "PNG範囲を調整できます。四辺・角・内側をドラッグ、Enterで撮影、Escでキャンセル。";
        }
    }

    private void BeginPngExportSelection()
    {
        _isExportSelecting = true;
        _exportSelectionDragging = false;
        _hasExportSelection = false;
        _exportSelectionRectangle = Rectangle.Empty;
        _exportDragStartRectangle = Rectangle.Empty;
        _exportDragMode = ExportSelectionDragMode.None;
        _draggedNode = null;
        _resizedNode = null;
        _draggedHandleTransition = null;
        _draggedHandleKind = TransitionHandleKind.None;
        _linkSource = null;
        _isPanning = false;
        _status = "PNG出力モードです。左ドラッグで範囲作成、四辺を調整、Enterで撮影。Escでキャンセル。";
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

    private void UpdateExportSelectionDrag(Vector2 screenPosition)
    {
        var rectangle = _exportDragMode == ExportSelectionDragMode.New
            ? RectangleFromPoints(_exportDragStart, screenPosition)
            : ResizeExportSelection(_exportDragStartRectangle, _exportDragMode, screenPosition - _exportDragStart);
        _exportSelectionRectangle = ClampExportSelectionRectangle(rectangle);
        _hasExportSelection = _exportSelectionRectangle.Width >= 16 && _exportSelectionRectangle.Height >= 16;
    }

    private Rectangle ResizeExportSelection(Rectangle rectangle, ExportSelectionDragMode mode, Vector2 delta)
    {
        var left = rectangle.Left;
        var top = rectangle.Top;
        var right = rectangle.Right;
        var bottom = rectangle.Bottom;
        var dx = (int)MathF.Round(delta.X);
        var dy = (int)MathF.Round(delta.Y);

        if (mode == ExportSelectionDragMode.Move)
        {
            return ClampMovedExportSelection(new Rectangle(rectangle.X + dx, rectangle.Y + dy, rectangle.Width, rectangle.Height));
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

        return RectangleFromEdges(left, top, right, bottom);
    }

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

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        DrawExportPhotoFrame(new Rectangle(0, 0, imageWidth, imageHeight), imageArea, fillBackdrop: true, fillImageArea: true);
        _spriteBatch.End();

        var worldTopLeft = ScreenToWorld(new Vector2(selection.X, selection.Y));
        var worldBottomRight = ScreenToWorld(new Vector2(selection.Right, selection.Bottom));
        var exportTransform = Matrix.CreateTranslation(-worldTopLeft.X + imageArea.X, -worldTopLeft.Y + imageArea.Y, 0f);
        var previousScissor = GraphicsDevice.ScissorRectangle;
        using var scissorRasterizer = new RasterizerState { ScissorTestEnable = true };
        GraphicsDevice.ScissorRectangle = imageArea;
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: scissorRasterizer, transformMatrix: exportTransform);
        DrawGrid(40, _boardTheme.GridColor, worldTopLeft, worldBottomRight);
        DrawDiagramContent(includeInteraction: false);
        _spriteBatch.End();
        GraphicsDevice.ScissorRectangle = previousScissor;

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
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
        if (IsNewKeyPress(keyboard, Keys.Enter))
        {
            CommitLabelEdit();
            return;
        }
        if (IsNewKeyPress(keyboard, Keys.Escape))
        {
            CancelLabelEdit();
            return;
        }
        if (IsNewKeyPress(keyboard, Keys.Back) && _editingLabel.Length > 0)
        {
            _editingLabel = _editingLabel[..^1];
        }
    }
    private void BeginLabelEdit(DiagramNode node)
    {
        _editingNode = node;
        _editingTransition = null;
        _editingLabel = node.Label;
        _draggedNode = null;
        _resizedNode = null;
        _linkSource = null;
        _status = "状態ラベルを編集中です。Enterで確定、Escでキャンセルします。";
    }
    private void BeginLabelEdit(DiagramTransition transition)
    {
        _editingNode = null;
        _editingTransition = transition;
        _editingLabel = transition.Label;
        _draggedNode = null;
        _resizedNode = null;
        _linkSource = null;
        _status = "遷移ラベルを編集中です。Enterで確定、Escでキャンセルします。";
    }
    private void CommitLabelEdit()
    {
        var label = _editingLabel.Trim();
        if (_editingNode is not null)
        {
            _editingNode.Label = string.IsNullOrWhiteSpace(label) ? $"状態{_editingNode.Id}" : label;
            _status = "状態ラベルを更新しました。Ctrl+Sで保存できます。";
        }
        else if (_editingTransition is not null)
        {
            _editingTransition.Label = label;
            _status = "遷移ラベルを更新しました。Tabで表示位置を切り替えられます。";
        }
        _editingNode = null;
        _editingTransition = null;
        _editingLabel = string.Empty;
    }
    private void CancelLabelEdit()
    {
        _editingNode = null;
        _editingTransition = null;
        _editingLabel = string.Empty;
        _status = "ラベル編集をキャンセルしました。";
    }
    private void ToggleNodeKind(DiagramNode node)
    {
        node.Kind = node.Kind switch
        {
            NodeKind.Normal => NodeKind.Start,
            NodeKind.Start => NodeKind.End,
            _ => NodeKind.Normal
        };

        _status = node.Kind switch
        {
            NodeKind.Start => "選択中の状態を開始ノードにしました。",
            NodeKind.End => "選択中の状態を終了ノードにしました。",
            _ => "選択中の状態を通常ノードに戻しました。"
        };
    }
    private void ToggleTransitionLabelSide(DiagramTransition transition)
    {
        transition.LabelSide = transition.LabelSide == 0 ? 1 : 0;
        _status = "遷移ラベルの表示位置を切り替えました。";
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
            _isPanning = false;
            _resizedNode = FindNodeResizeHandleAt(mousePosition);
            if (_resizedNode is not null)
            {
                _selectedNode = _resizedNode;
                _selectedTransition = null;
                _draggedNode = null;
                _draggedHandleTransition = null;
                _draggedHandleKind = TransitionHandleKind.None;
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
                _status = "遷移を選択しました。ハンドルで形を調整、F2・Enterでラベル編集。";
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
                _status = "状態を選択しました。F2・Enterでラベル編集、Tで種別変更。";
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
                        _status = "遷移を作成しました。ハンドルで形を調整、F2・Enterでラベル編集。";
                    }
                }
                _linkSource = null;
            }
            if (_draggedHandleTransition is not null)
            {
                _status = "遷移の形を更新しました。Ctrl+Sで保存できます。";
            }
            if (_draggedNode is not null)
            {
                _status = snapNodes
                    ? "状態を移動しました。中心は半グリッドに吸着しています。"
                    : "状態を移動しました。Alt中は吸着しません。";
            }
            if (_resizedNode is not null)
            {
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
    private void AddNode(Vector2 position)
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
        _status = "状態を追加しました。F2・Enterでラベルを編集できます。";
    }
    private void AddTransition(int sourceId, int targetId)
    {
        if (_transitions.Any(t => t.SourceId == sourceId && t.TargetId == targetId))
        {
            _status = "同じ向きの遷移は既にあります。";
            return;
        }
        var transition = new DiagramTransition { SourceId = sourceId, TargetId = targetId };
        InitializeTransitionEndpoints(transition);
        _transitions.Add(transition);
    }
    private void DeleteSelection()
    {
        if (_selectedNode is not null)
        {
            var id = _selectedNode.Id;
            _nodes.Remove(_selectedNode);
            _transitions.RemoveAll(t => t.SourceId == id || t.TargetId == id);
            _status = "選択中の状態を削除しました。";
            _selectedNode = null;
            return;
        }
        if (_selectedTransition is not null)
        {
            _transitions.Remove(_selectedTransition);
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
        var json = JsonSerializer.Serialize(document, DiagramJsonOptions);
        File.WriteAllText(path, json, Utf8NoBom);
        _currentFilePath = path;
        _status = $"{Path.GetFileName(path)} を保存しました。";
    }
    private void LoadDiagram()
    {
        var path = Path.Combine(AppContext.BaseDirectory, SaveFileName);
        LoadDiagramFromPath(path);
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
        var document = JsonSerializer.Deserialize<DiagramDocument>(File.ReadAllText(path, Encoding.UTF8), DiagramJsonOptions);
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
        _status = $"{Path.GetFileName(path)} を読み込みました。";
    }
    private void LoadOrCreateSample()
    {
        var path = Path.Combine(AppContext.BaseDirectory, SaveFileName);
        if (File.Exists(path))
        {
            try
            {
                LoadDiagramFromPath(path);
                return;
            }
            catch
            {
                _nodes.Clear();
                _transitions.Clear();
            }
        }
        CreateSample();
    }
    private void CreateNewDiagram()
    {
        CreateSample();
        _currentFilePath = null;
        SaveDiagramAs();
    }
    private void CreateSample()
    {
        _nodes.Clear();
        _transitions.Clear();
        _nextNodeId = 1;
        AddNode(new Vector2(230, 220));
        AddNode(new Vector2(530, 220));
        AddNode(new Vector2(530, 480));
        AddNode(new Vector2(230, 480));
        _nodes[0].Label = "開始";
        _nodes[0].Kind = NodeKind.Start;
        _nodes[1].Label = "下書き";
        _nodes[2].Label = "レビュー";
        _nodes[3].Label = "終了";
        _nodes[3].Kind = NodeKind.End;
        AddTransition(_nodes[0].Id, _nodes[1].Id);
        AddTransition(_nodes[1].Id, _nodes[2].Id);
        AddTransition(_nodes[2].Id, _nodes[1].Id);
        AddTransition(_nodes[2].Id, _nodes[3].Id);
        AddTransition(_nodes[1].Id, _nodes[1].Id);
        _transitions[0].Label = "着手";
        _transitions[0].LabelSide = 0;
        _transitions[0].ControlPoint1 = new Vector2(340, 130);
        _transitions[0].ControlPoint2 = new Vector2(430, 145);
        _transitions[1].Label = "確認";
        _transitions[1].LabelSide = 1;
        _transitions[1].SourceAngle = MathHelper.PiOver2;
        _transitions[1].TargetAngle = -MathHelper.PiOver2;
        _transitions[1].ControlPoint1 = new Vector2(635, 320);
        _transitions[1].ControlPoint2 = new Vector2(635, 390);
        _transitions[2].Label = "差戻し";
        _transitions[2].LabelSide = 0;
        _transitions[2].SourceAngle = -MathHelper.PiOver2;
        _transitions[2].TargetAngle = MathHelper.PiOver2;
        _transitions[2].ControlPoint1 = new Vector2(405, 390);
        _transitions[2].ControlPoint2 = new Vector2(405, 320);
        _transitions[3].Label = "承認";
        _transitions[3].LabelSide = 0;
        _transitions[3].ControlPoint1 = new Vector2(420, 560);
        _transitions[3].ControlPoint2 = new Vector2(325, 540);
        _transitions[4].Label = "再入";
        _transitions[4].LabelSide = 1;
        _transitions[4].SourceAngle = -MathHelper.PiOver4;
        _transitions[4].TargetAngle = MathHelper.PiOver4;
        _transitions[4].ControlPoint1 = new Vector2(760, 60);
        _transitions[4].ControlPoint2 = new Vector2(760, 380);
        _selectedNode = null;
        _selectedTransition = null;
        _cameraOffset = Vector2.Zero;
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
        start = PointOnCircle(source.Position, source.Radius, sourceAngle);
        end = PointOnCircle(target.Position, target.Radius, targetAngle);
        return true;
    }

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
            DrawLine(new Vector2(x, topLeft.Y), new Vector2(x, bottomRight.Y), color, 1f);
        }
        for (var y = startY; y <= endY; y += spacing)
        {
            DrawLine(new Vector2(topLeft.X, y), new Vector2(bottomRight.X, y), color, 1f);
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
                DrawCircleOutline(center, 13f, new Color(255, 245, 170), 3f);
            }
            return;
        }

        var node = FindNodeAt(mouseWorld);
        if (node is not null)
        {
            if (node != _selectedNode)
            {
                DrawCircleOutline(node.Position, node.Radius + 8f, new Color(130, 185, 230), 3f);
            }
            return;
        }

        var transition = FindTransitionAt(mouseWorld);
        if (transition is not null && transition != _selectedTransition && TryGetTransitionGeometry(transition, out start, out control1, out control2, out end))
        {
            DrawBezierArrow(start, control1, control2, end, new Color(115, 170, 220, 180), 5f);
        }
    }

    private void DrawDiagramScene(Matrix transformMatrix, bool includeInteraction)
    {
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: transformMatrix);
        DrawGrid(40, _boardTheme.GridColor);
        DrawDiagramContent(includeInteraction);
        _spriteBatch.End();
    }

    private void DrawDiagramContent(bool includeInteraction)
    {
        foreach (var transition in _transitions)
        {
            DrawTransition(transition, includeInteraction && transition == _selectedTransition);
        }
        if (includeInteraction)
        {
            DrawHoverCue();
            if (_linkSource is not null)
            {
                var mouse = Mouse.GetState();
                DrawArrow(_linkSource.Position, ScreenToWorld(mouse.Position.ToVector2()), new Color(250, 205, 95), 3f);
            }
        }
        foreach (var node in _nodes)
        {
            DrawNode(node, includeInteraction && node == _selectedNode);
        }
        if (includeInteraction && _selectedNode is not null)
        {
            DrawNodeResizeHandle(_selectedNode);
        }
        if (includeInteraction && _selectedTransition is not null)
        {
            DrawTransitionHandles(_selectedTransition);
        }
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
        DrawCircle(center + new Vector2(2, 3), 10f, new Color(20, 14, 12, 95));
        DrawCircle(center, 9f, color);
        DrawCircleOutline(center, 9f, new Color(80, 42, 36, 170), 2f);
        DrawCircle(center - new Vector2(3, 3), 3f, new Color(255, 244, 220, 185));
    }

    private void DrawScreenRectangleOutline(Rectangle rectangle, Color color, int thickness)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, thickness), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Bottom - thickness, rectangle.Width, thickness), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Y, thickness, rectangle.Height), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.Right - thickness, rectangle.Y, thickness, rectangle.Height), color);
    }
    private void DrawToolbar()
    {
        var width = GraphicsDevice.Viewport.Width;
        var bounds = new Rectangle(0, 0, width, 58);
        _spriteBatch.Draw(_pixel, bounds, new Color(17, 19, 23, 238));
        _spriteBatch.Draw(_pixel, new Rectangle(0, bounds.Height - 1, width, 1), new Color(65, 72, 84));
        DrawUiText("Skylark State Transition Diagram", new Vector2(12, 8), new Color(245, 247, 250), 18, true);
        DrawUiText(_status, new Vector2(12, 32), new Color(210, 220, 232), 16, false);
    }

    private void DrawInspectorPanel()
    {
        var width = GraphicsDevice.Viewport.Width;
        if (width < 560)
        {
            return;
        }

        const int panelWidth = 290;
        var x = width - panelWidth - 12;
        var bounds = new Rectangle(x, 70, panelWidth, 96);
        _spriteBatch.Draw(_pixel, bounds, new Color(22, 25, 31, 218));
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), new Color(82, 92, 108));
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), new Color(8, 10, 14));

        DrawUiText($"状態: {_nodes.Count}    遷移: {_transitions.Count}", new Vector2(x + 12, bounds.Y + 10), new Color(236, 240, 245), 16, true);
        DrawUiText(GetSelectionSummary(), new Vector2(x + 12, bounds.Y + 36), new Color(196, 210, 226), 15, false);
        DrawUiText(GetFileSummary(), new Vector2(x + 12, bounds.Y + 62), new Color(170, 184, 202), 14, false);
    }

    private void DrawBottomHelp()
    {
        var viewport = GraphicsDevice.Viewport;
        if (viewport.Height < 360)
        {
            return;
        }

        var y = viewport.Height - 34;
        _spriteBatch.Draw(_pixel, new Rectangle(0, y, viewport.Width, 34), new Color(17, 19, 23, 210));

        var position = new Vector2(12, y + 6);
        position = DrawShortcutHint(position, "Alt", "吸着なし");
        position = DrawHelpSeparator(position);
        position = DrawShortcutHint(position, "Tab", "遷移ラベル位置");
        position = DrawHelpSeparator(position);
        position = DrawShortcutHint(position, "Delete", "削除");
        position = DrawHelpSeparator(position);
        position = DrawShortcutHint(position, "0-9", "テーマ");
        position = DrawHelpSeparator(position);
        DrawShortcutHint(position, "空白", "表示移動");
    }

    private Vector2 DrawShortcutHint(Vector2 position, string key, string description)
    {
        var x = DrawKeyCap(key, position);
        x = DrawUiText(description, new Vector2(x + 6, position.Y + 3), _keyCapTheme.DescriptionTextColor, 14, false);
        return new Vector2(x + 12, position.Y);
    }

    private Vector2 DrawHelpSeparator(Vector2 position)
    {
        var x = DrawUiText("/", new Vector2(position.X, position.Y + 3), _keyCapTheme.SeparatorTextColor, 14, false);
        return new Vector2(x + 12, position.Y);
    }

    private string GetSelectionSummary()
    {
        if (_selectedNode is not null)
        {
            var kind = _selectedNode.Kind switch
            {
                NodeKind.Start => "開始",
                NodeKind.End => "終了",
                _ => "通常"
            };
            return $"選択: 状態 {_selectedNode.Id} / {kind} / サイズ {_selectedNode.RadiusUnits}";
        }

        if (_selectedTransition is not null)
        {
            return $"選択: 遷移 {_selectedTransition.SourceId} -> {_selectedTransition.TargetId}";
        }

        return "選択: なし";
    }

    private string GetFileSummary()
        => _currentFilePath is null ? "保存先: 未指定" : $"保存先: {Path.GetFileName(_currentFilePath)}";
    private void DrawNode(DiagramNode node, bool selected)
    {
        var fill = node.Kind == NodeKind.Normal ? Palette[node.ColorIndex % Palette.Length] : new Color(5, 6, 8);
        DrawCircle(node.Position, node.Radius + 4, selected ? new Color(255, 255, 255) : new Color(10, 12, 16));
        DrawCircle(node.Position, node.Radius, fill);

        if (node.Kind == NodeKind.Normal)
        {
            DrawCircleOutline(node.Position, node.Radius, new Color(15, 18, 24), 3f);
        }
        else
        {
            DrawCircleOutline(node.Position, node.Radius, Color.White, node.Kind == NodeKind.Start ? 4f : 3f);
            if (node.Kind == NodeKind.End)
            {
                DrawCircleOutline(node.Position, node.Radius - 10f, Color.White, 3f);
            }
        }

        var label = node == _editingNode ? _editingLabel + "_" : node.Label;
        DrawNodeLabel(label, node.Position, node == _editingNode);
    }

    private void DrawNodeResizeHandle(DiagramNode node)
    {
        var center = GetNodeResizeHandleCenter(node);
        DrawLine(node.Position + new Vector2(node.Radius * 0.72f, node.Radius * 0.72f), center, new Color(255, 230, 120), 2f);
        DrawHandle(center, new Color(255, 230, 120));
    }

    private void DrawNodeLabel(string label, Vector2 center, bool editing)
    {
        var texture = GetLabelTexture(label, editing);
        var position = center - new Vector2(texture.Width / 2f, texture.Height / 2f);
        _spriteBatch.Draw(texture, position, Color.White);
    }
    private float DrawUiText(string text, Vector2 position, Color color, float size, bool bold)
    {
        var texture = GetUiTextTexture(text, size, bold);
        _spriteBatch.Draw(texture, position, color);
        return position.X + texture.Width;
    }

    private float DrawKeyCap(string text, Vector2 position)
    {
        var textTexture = GetUiTextTexture(text, _keyCapTheme.FontSize, true);
        var width = Math.Max(_keyCapTheme.MinWidth, textTexture.Width + (_keyCapTheme.HorizontalPadding * 2));
        var height = _keyCapTheme.Height;
        var bounds = new Rectangle((int)position.X, (int)position.Y, width, height);

        _spriteBatch.Draw(_pixel, bounds, _keyCapTheme.FaceColor);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), _keyCapTheme.TopEdgeColor);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), _keyCapTheme.TopEdgeColor);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - 2, bounds.Width, 2), _keyCapTheme.BottomEdgeColor);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), _keyCapTheme.BottomEdgeColor);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X + 2, bounds.Y + 2, bounds.Width - 4, 1), _keyCapTheme.InnerHighlightColor);

        var textPosition = new Vector2(
            bounds.X + (bounds.Width - textTexture.Width) / 2f,
            bounds.Y + (bounds.Height - textTexture.Height) / 2f);
        _spriteBatch.Draw(textTexture, textPosition, _keyCapTheme.LabelTextColor);
        return bounds.Right;
    }

    private Texture2D GetUiTextTexture(string text, float size, bool bold)
    {
        var cacheKey = $"ui|{size}|{bold}|{text}";
        if (_uiTextTextureCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var texture = CreateUiTextTexture(GraphicsDevice, text, size, bold);
        _uiTextTextureCache[cacheKey] = texture;
        return texture;
    }

    private static Texture2D CreateUiTextTexture(GraphicsDevice graphicsDevice, string text, float size, bool bold)
    {
        var renderedText = string.IsNullOrEmpty(text) ? " " : text;
        using var font = CreateJapaneseFont(size, bold);
        using var measureBitmap = new DrawingBitmap(1, 1);
        using var measureGraphics = DrawingGraphics.FromImage(measureBitmap);
        var measured = measureGraphics.MeasureString(renderedText, font, 1024, StringFormatNoWrap);
        var width = Math.Clamp((int)Math.Ceiling(measured.Width) + 4, 8, 1024);
        var height = Math.Clamp((int)Math.Ceiling(measured.Height) + 4, 8, 64);
        using var bitmap = new DrawingBitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var graphics = DrawingGraphics.FromImage(bitmap);
        graphics.Clear(DrawingColor.Transparent);
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        graphics.DrawString(renderedText, font, DrawingBrushes.White, new DrawingRectangleF(0, 0, width, height), LeftAlignedStringFormat);

        var colors = new Color[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                colors[y * width + x] = new Color(pixel.R, pixel.G, pixel.B, pixel.A);
            }
        }

        var texture = new Texture2D(graphicsDevice, width, height);
        texture.SetData(colors);
        return texture;
    }
    private Texture2D GetLabelTexture(string label, bool editing)
    {
        var cacheKey = $"{(editing ? "edit" : "label")}|{label}";
        if (_labelTextureCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }
        var texture = CreateLabelTexture(GraphicsDevice, label, editing);
        _labelTextureCache[cacheKey] = texture;
        return texture;
    }
    private static Texture2D CreateLabelTexture(GraphicsDevice graphicsDevice, string label, bool editing)
    {
        var text = string.IsNullOrEmpty(label) ? " " : label;
        using var font = CreateJapaneseFont(22, true);
        using var measureBitmap = new DrawingBitmap(1, 1);
        using var measureGraphics = DrawingGraphics.FromImage(measureBitmap);
        var measured = measureGraphics.MeasureString(text, font, 512, StringFormatNoWrap);
        var width = Math.Clamp((int)Math.Ceiling(measured.Width) + 18, 48, 220);
        var height = Math.Clamp((int)Math.Ceiling(measured.Height) + 10, 30, 72);
        using var bitmap = new DrawingBitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var graphics = DrawingGraphics.FromImage(bitmap);
        graphics.Clear(DrawingColor.Transparent);
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        if (editing)
        {
            using var editBrush = new System.Drawing.SolidBrush(DrawingColor.FromArgb(170, 20, 24, 30));
            graphics.FillRectangle(editBrush, 0, 0, width, height);
        }
        var rectangle = new DrawingRectangleF(0, 0, width, height);
        graphics.DrawString(text, font, DrawingBrushes.White, rectangle, CenteredStringFormat);
        var colors = new Color[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                colors[y * width + x] = new Color(pixel.R, pixel.G, pixel.B, pixel.A);
            }
        }
        var texture = new Texture2D(graphicsDevice, width, height);
        texture.SetData(colors);
        return texture;
    }
    private static DrawingFont CreateJapaneseFont(float size, bool bold)
    {
        var candidates = new[] { "Yu Gothic UI", "Meiryo", "MS Gothic", "Noto Sans CJK JP", "Arial Unicode MS" };
        foreach (var candidate in candidates)
        {
            if (DrawingFontFamily.Families.Any(f => string.Equals(f.Name, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return new DrawingFont(candidate, size, bold ? DrawingFontStyle.Bold : DrawingFontStyle.Regular, DrawingGraphicsUnit.Pixel);
            }
        }
        return new DrawingFont(DrawingFontFamily.GenericSansSerif, size, bold ? DrawingFontStyle.Bold : DrawingFontStyle.Regular, DrawingGraphicsUnit.Pixel);
    }
    private static readonly DrawingStringFormat CenteredStringFormat = new()
    {
        Alignment = System.Drawing.StringAlignment.Center,
        LineAlignment = System.Drawing.StringAlignment.Center,
        FormatFlags = DrawingStringFormatFlags.NoWrap
    };
    private static readonly DrawingStringFormat LeftAlignedStringFormat = new()
    {
        Alignment = System.Drawing.StringAlignment.Near,
        LineAlignment = System.Drawing.StringAlignment.Center,
        FormatFlags = DrawingStringFormatFlags.NoWrap
    };
    private static readonly DrawingStringFormat StringFormatNoWrap = new()
    {
        FormatFlags = DrawingStringFormatFlags.NoWrap
    };
    private void DrawTransition(DiagramTransition transition, bool selected)
    {
        if (!TryGetTransitionGeometry(transition, out var start, out var control1, out var control2, out var end))
        {
            return;
        }

        DrawBezierArrow(start, control1, control2, end, selected ? new Color(255, 230, 120) : new Color(185, 195, 210), selected ? 4f : 3f);
        DrawTransitionLabel(transition, start, control1, control2, end, selected);
    }

    private void DrawTransitionHandles(DiagramTransition transition)
    {
        if (!TryGetTransitionGeometry(transition, out var start, out var control1, out var control2, out var end))
        {
            return;
        }

        DrawLine(start, control1, new Color(95, 120, 145), 1f);
        DrawLine(end, control2, new Color(95, 120, 145), 1f);
        DrawHandle(start, new Color(255, 230, 120));
        DrawHandle(end, new Color(255, 230, 120));
        DrawHandle(control1, new Color(80, 190, 230));
        DrawHandle(control2, new Color(80, 190, 230));
    }

    private void DrawHandle(Vector2 center, Color color)
    {
        DrawCircle(center, 8f, color);
        DrawCircleOutline(center, 8f, new Color(20, 24, 30), 2f);
    }

    private void DrawTransitionLabel(DiagramTransition transition, Vector2 start, Vector2 control1, Vector2 control2, Vector2 end, bool selected)
    {
        var editing = transition == _editingTransition;
        var label = editing ? _editingLabel + "_" : transition.Label;
        if (string.IsNullOrWhiteSpace(label) && !editing)
        {
            return;
        }
        var texture = GetLabelTexture(label, editing || selected);
        var center = GetTransitionLabelCenter(start, control1, control2, end, transition.LabelSide, texture);
        var position = center - new Vector2(texture.Width / 2f, texture.Height / 2f);
        _spriteBatch.Draw(texture, position, Color.White);
    }

    private static Vector2 GetTransitionLabelCenter(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end, int labelSide, Texture2D labelTexture)
    {
        var midpoint = CubicBezier(start, control1, control2, end, 0.5f);
        var tangent = CubicBezierTangent(start, control1, control2, end, 0.5f);
        var side = labelSide == 0 ? -1f : 1f;
        if (MathF.Abs(tangent.X) >= MathF.Abs(tangent.Y))
        {
            return midpoint + new Vector2(0, side * (labelTexture.Height / 2f + 18f));
        }
        return midpoint + new Vector2(side * (labelTexture.Width / 2f + 22f), 0);
    }

    private void DrawBezierArrow(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end, Color color, float thickness)
    {
        const int segments = 32;
        var previous = start;
        for (var i = 1; i <= segments; i++)
        {
            var t = i / (float)segments;
            var current = CubicBezier(start, control1, control2, end, t);
            DrawLine(previous, current, color, thickness);
            previous = current;
        }

        var tangent = CubicBezierTangent(start, control1, control2, end, 1f);
        DrawArrowHead(end, tangent, color, thickness);
    }

    private void DrawArrowHead(Vector2 tip, Vector2 tangent, Color color, float thickness)
    {
        if (tangent.LengthSquared() <= 0.01f)
        {
            return;
        }

        var direction = Vector2.Normalize(tangent);
        var normal = new Vector2(-direction.Y, direction.X);
        DrawLine(tip, tip - direction * 18 + normal * 8, color, thickness);
        DrawLine(tip, tip - direction * 18 - normal * 8, color, thickness);
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
    private void DrawArrow(Vector2 start, Vector2 end, Color color, float thickness)
    {
        DrawLine(start, end, color, thickness);
        var direction = Vector2.Normalize(end - start);
        var normal = new Vector2(-direction.Y, direction.X);
        var tip = end;
        DrawLine(tip, tip - direction * 18 + normal * 8, color, thickness);
        DrawLine(tip, tip - direction * 18 - normal * 8, color, thickness);
    }
    private void DrawLine(Vector2 start, Vector2 end, Color color, float thickness)
    {
        var delta = end - start;
        var length = delta.Length();
        if (length <= 0.01f)
        {
            return;
        }
        _spriteBatch.Draw(_pixel, start, null, color, (float)Math.Atan2(delta.Y, delta.X), Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0f);
    }
    private void DrawCircle(Vector2 center, float radius, Color color)
    {
        for (var y = -radius; y <= radius; y++)
        {
            var halfWidth = (float)Math.Sqrt(radius * radius - y * y);
            _spriteBatch.Draw(_pixel, new Rectangle((int)(center.X - halfWidth), (int)(center.Y + y), (int)(halfWidth * 2), 1), color);
        }
    }
    private void DrawCircleOutline(Vector2 center, float radius, Color color, float thickness)
    {
        const int segments = 64;
        var previous = center + new Vector2(radius, 0);
        for (var i = 1; i <= segments; i++)
        {
            var angle = MathHelper.TwoPi * i / segments;
            var current = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            DrawLine(previous, current, color, thickness);
            previous = current;
        }
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
        if (_currentFilePath is not null)
        {
            var currentDirectory = Path.GetDirectoryName(_currentFilePath);
            if (!string.IsNullOrWhiteSpace(currentDirectory) && Directory.Exists(currentDirectory))
            {
                return currentDirectory;
            }
        }
        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
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
    public List<DiagramNode> Nodes { get; set; } = new();
    public List<DiagramTransition> Transitions { get; set; } = new();
}
public sealed class DiagramNode
{
    public const float RadiusUnit = 20f;
    public const int DefaultRadiusUnits = 3;
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
    Normal,
    Start,
    End
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
    Color PinColor);

public static class BoardThemes
{
    public static BoardTheme ForKeyCapTheme(IKeyCapTheme keyCapTheme)
        => keyCapTheme.Name switch
        {
            "Gaming" => new BoardTheme(new Color(18, 20, 28), new Color(42, 88, 96), new Color(15, 18, 24), new Color(230, 236, 232), new Color(78, 104, 108), new Color(88, 232, 206)),
            "Retro" => new BoardTheme(new Color(116, 82, 52), new Color(149, 109, 70), new Color(98, 65, 40), new Color(241, 229, 198), new Color(155, 125, 82), new Color(190, 54, 44)),
            "CopyPaper" => new BoardTheme(new Color(226, 229, 224), new Color(198, 205, 202), new Color(190, 185, 174), new Color(252, 250, 242), new Color(190, 184, 172), new Color(60, 112, 178)),
            "Girly" => new BoardTheme(new Color(67, 47, 62), new Color(119, 78, 104), new Color(92, 62, 78), new Color(255, 236, 240), new Color(205, 142, 162), new Color(232, 92, 132)),
            "Edo" => new BoardTheme(new Color(36, 45, 50), new Color(70, 86, 82), new Color(41, 35, 30), new Color(238, 231, 207), new Color(123, 88, 54), new Color(178, 48, 44)),
            "Monochrome" => new BoardTheme(new Color(34, 34, 34), new Color(68, 68, 68), new Color(24, 24, 24), new Color(235, 235, 228), new Color(120, 120, 114), new Color(210, 210, 210)),
            "Mint" => new BoardTheme(new Color(29, 61, 57), new Color(63, 104, 96), new Color(46, 82, 74), new Color(235, 250, 239), new Color(126, 170, 152), new Color(76, 198, 157)),
            "Amber" => new BoardTheme(new Color(66, 49, 34), new Color(112, 83, 48), new Color(86, 61, 35), new Color(248, 229, 188), new Color(176, 126, 56), new Color(225, 151, 48)),
            "Midnight" => new BoardTheme(new Color(17, 24, 34), new Color(37, 52, 68), new Color(14, 19, 28), new Color(230, 234, 232), new Color(90, 102, 116), new Color(96, 154, 232)),
            _ => new BoardTheme(new Color(28, 31, 36), new Color(42, 46, 52), new Color(104, 73, 48), new Color(244, 236, 218), new Color(150, 132, 106), new Color(190, 54, 44))
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
