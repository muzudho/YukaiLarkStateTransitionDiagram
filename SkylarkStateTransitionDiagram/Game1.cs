namespace SkylarkStateTransitionDiagram;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
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
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
public class Game1 : Game
{
    private const string SaveFileName = "diagram.json";
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
    private DiagramTransition? _draggedEndpointTransition;
    private bool _draggedEndpointIsSource;
    private Vector2 _dragOffset;
    private int _nextNodeId = 1;
    private string _editingLabel = string.Empty;
    private string _status = "N: add  F2/ENTER: edit label  drag edge endpoints  SHIFT+drag: link  CTRL+S/O: save/load";
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
        if (IsEditingLabel)
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
        GraphicsDevice.Clear(new Color(28, 31, 36));
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        DrawGrid(40, new Color(42, 46, 52));
        foreach (var transition in _transitions)
        {
            DrawTransition(transition, transition == _selectedTransition);
        }
        if (_linkSource is not null)
        {
            var mouse = Mouse.GetState();
            DrawArrow(_linkSource.Position, mouse.Position.ToVector2(), new Color(250, 205, 95), 3f);
        }
        foreach (var node in _nodes)
        {
            DrawNode(node, node == _selectedNode);
        }
        if (_selectedTransition is not null)
        {
            DrawTransitionHandles(_selectedTransition);
        }
        DrawToolbar();
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
            _status = "Label is limited to 24 characters.";
            return;
        }
        _editingLabel += e.Character;
    }
    private void HandleKeyboard(KeyboardState keyboard, MouseState mouse)
    {
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
            AddNode(mouse.Position.ToVector2());
        }
        if (IsNewKeyPress(keyboard, Keys.Delete) || IsNewKeyPress(keyboard, Keys.Back))
        {
            DeleteSelection();
        }
        if (IsNewKeyPress(keyboard, Keys.R))
        {
            CreateSample();
            _status = "Sample diagram reset.";
        }
        if (IsNewKeyPress(keyboard, Keys.C) && _selectedNode is not null)
        {
            _selectedNode.ColorIndex = (_selectedNode.ColorIndex + 1) % Palette.Length;
            _status = "Changed selected state color.";
        }
        if (IsControlDown(keyboard) && IsNewKeyPress(keyboard, Keys.S))
        {
            SaveDiagram();
        }
        if (IsControlDown(keyboard) && IsNewKeyPress(keyboard, Keys.O))
        {
            LoadDiagram();
        }
    }
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
        _linkSource = null;
        _status = "Editing state label: type Japanese text, ENTER commits, ESC cancels.";
    }
    private void BeginLabelEdit(DiagramTransition transition)
    {
        _editingNode = null;
        _editingTransition = transition;
        _editingLabel = transition.Label;
        _draggedNode = null;
        _linkSource = null;
        _status = "Editing edge label: type Japanese text, ENTER commits, ESC cancels.";
    }
    private void CommitLabelEdit()
    {
        var label = _editingLabel.Trim();
        if (_editingNode is not null)
        {
            _editingNode.Label = string.IsNullOrWhiteSpace(label) ? $"状態{_editingNode.Id}" : label;
            _status = "State label updated. CTRL+S saves UTF-8 without BOM.";
        }
        else if (_editingTransition is not null)
        {
            _editingTransition.Label = label;
            _status = "Edge label updated. Press TAB to flip its side.";
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
        _status = "Label edit canceled.";
    }
    private void ToggleTransitionLabelSide(DiagramTransition transition)
    {
        transition.LabelSide = transition.LabelSide == 0 ? 1 : 0;
        _status = "Edge label side flipped. Horizontal edges use top/bottom; vertical edges use left/right.";
    }
    private void HandleMouse(KeyboardState keyboard, MouseState mouse)
    {
        var mousePosition = mouse.Position.ToVector2();
        var leftPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;
        var leftReleased = mouse.LeftButton == ButtonState.Released && _previousMouse.LeftButton == ButtonState.Pressed;
        var shiftDown = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
        if (leftPressed)
        {
            var endpoint = FindTransitionEndpointAt(mousePosition);
            if (endpoint.Transition is not null)
            {
                _selectedNode = null;
                _selectedTransition = endpoint.Transition;
                _draggedNode = null;
                _draggedEndpointTransition = endpoint.Transition;
                _draggedEndpointIsSource = endpoint.IsSource;
                UpdateTransitionEndpoint(endpoint.Transition, endpoint.IsSource, mousePosition);
                _status = "Dragging edge contact point on the node circle.";
                return;
            }

            var node = FindNodeAt(mousePosition);
            _selectedNode = node;
            _selectedTransition = node is null ? FindTransitionAt(mousePosition) : null;
            if (_selectedTransition is not null)
            {
                _status = "Selected edge. Drag endpoint handles, F2/ENTER edits label, TAB flips label side.";
            }
            if (shiftDown && node is not null)
            {
                _linkSource = node;
                _status = "Linking from selected state. Release on another state.";
                return;
            }
            if (node is not null)
            {
                _draggedNode = node;
                _dragOffset = mousePosition - node.Position;
                _status = "Selected state. Press F2 or ENTER to edit label.";
            }
        }
        if (_draggedEndpointTransition is not null && mouse.LeftButton == ButtonState.Pressed)
        {
            UpdateTransitionEndpoint(_draggedEndpointTransition, _draggedEndpointIsSource, mousePosition);
        }
        if (_draggedNode is not null && mouse.LeftButton == ButtonState.Pressed)
        {
            _draggedNode.Position = mousePosition - _dragOffset;
        }
        if (leftReleased)
        {
            if (_linkSource is not null)
            {
                var target = FindNodeAt(mousePosition);
                if (target is not null && target != _linkSource)
                {
                    var transitionCount = _transitions.Count;
                    AddTransition(_linkSource.Id, target.Id);
                    if (_transitions.Count > transitionCount)
                    {
                        _selectedNode = null;
                        _selectedTransition = _transitions.LastOrDefault();
                        _status = "Linked states. Drag edge contact points or press F2/ENTER to edit edge label.";
                    }
                }
                _linkSource = null;
            }
            if (_draggedEndpointTransition is not null)
            {
                _status = "Edge contact point moved. CTRL+S saves it.";
            }
            _draggedNode = null;
            _draggedEndpointTransition = null;
        }
    }
    private void AddNode(Vector2 position)
    {
        var node = new DiagramNode
        {
            Id = _nextNodeId++,
            Label = $"状態{_nextNodeId - 1}",
            Position = position,
            ColorIndex = (_nextNodeId - 2) % Palette.Length
        };
        _nodes.Add(node);
        _selectedNode = node;
        _selectedTransition = null;
        _status = "Added state. Press F2 or ENTER to edit label.";
    }
    private void AddTransition(int sourceId, int targetId)
    {
        if (_transitions.Any(t => t.SourceId == sourceId && t.TargetId == targetId))
        {
            _status = "That transition already exists.";
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
            _status = "Deleted selected state.";
            _selectedNode = null;
            return;
        }
        if (_selectedTransition is not null)
        {
            _transitions.Remove(_selectedTransition);
            _selectedTransition = null;
            _status = "Deleted selected transition.";
        }
    }
    private void SaveDiagram()
    {
        var path = Path.Combine(AppContext.BaseDirectory, SaveFileName);
        var document = new DiagramDocument { Nodes = _nodes, Transitions = _transitions };
        var json = JsonSerializer.Serialize(document, DiagramJsonOptions);
        File.WriteAllText(path, json, Utf8NoBom);
        _status = $"Saved {SaveFileName} as UTF-8 without BOM.";
    }
    private void LoadDiagram()
    {
        var path = Path.Combine(AppContext.BaseDirectory, SaveFileName);
        if (!File.Exists(path))
        {
            _status = $"No {SaveFileName} found next to the executable.";
            return;
        }
        var document = JsonSerializer.Deserialize<DiagramDocument>(File.ReadAllText(path, Encoding.UTF8), DiagramJsonOptions);
        if (document is null)
        {
            _status = "Could not load diagram.";
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
        _status = $"Loaded {SaveFileName}.";
    }
    private void LoadOrCreateSample()
    {
        var path = Path.Combine(AppContext.BaseDirectory, SaveFileName);
        if (File.Exists(path))
        {
            try
            {
                LoadDiagram();
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
    private void CreateSample()
    {
        _nodes.Clear();
        _transitions.Clear();
        _nextNodeId = 1;
        AddNode(new Vector2(230, 220));
        AddNode(new Vector2(530, 220));
        AddNode(new Vector2(530, 480));
        AddNode(new Vector2(230, 480));
        _nodes[0].Label = "着想";
        _nodes[1].Label = "下書き";
        _nodes[2].Label = "レビュー";
        _nodes[3].Label = "完了";
        AddTransition(_nodes[0].Id, _nodes[1].Id);
        AddTransition(_nodes[1].Id, _nodes[2].Id);
        AddTransition(_nodes[2].Id, _nodes[1].Id);
        AddTransition(_nodes[2].Id, _nodes[3].Id);
        _transitions[0].Label = "着手";
        _transitions[0].LabelSide = 0;
        _transitions[1].Label = "確認";
        _transitions[1].LabelSide = 1;
        _transitions[1].SourceAngle = MathHelper.PiOver2;
        _transitions[1].TargetAngle = -MathHelper.PiOver2;
        _transitions[2].Label = "差戻し";
        _transitions[2].LabelSide = 0;
        _transitions[2].SourceAngle = -MathHelper.PiOver2;
        _transitions[2].TargetAngle = MathHelper.PiOver2;
        _transitions[3].Label = "承認";
        _transitions[3].LabelSide = 0;
        _selectedNode = null;
        _selectedTransition = null;
        _status = "N: add  F2/ENTER: edit label  TAB: flip edge label  SHIFT+drag: link  C: color  DEL: delete  CTRL+S/O: save/load";
    }
    private DiagramNode? FindNodeAt(Vector2 position)
    {
        for (var i = _nodes.Count - 1; i >= 0; i--)
        {
            if (Vector2.Distance(_nodes[i].Position, position) <= DiagramNode.Radius)
            {
                return _nodes[i];
            }
        }
        return null;
    }
    private DiagramTransition? FindTransitionAt(Vector2 position)
    {
        foreach (var transition in _transitions)
        {
            if (!TryGetTransitionEndpoints(transition, out var start, out var end))
            {
                continue;
            }

            if (DistanceToSegment(position, start, end) <= 8f)
            {
                return transition;
            }
        }
        return null;
    }

    private TransitionEndpointHit FindTransitionEndpointAt(Vector2 position)
    {
        foreach (var transition in Enumerable.Reverse(_transitions))
        {
            if (!TryGetTransitionEndpoints(transition, out var start, out var end))
            {
                continue;
            }

            if (Vector2.Distance(position, start) <= 14f)
            {
                return new TransitionEndpointHit(transition, true);
            }

            if (Vector2.Distance(position, end) <= 14f)
            {
                return new TransitionEndpointHit(transition, false);
            }
        }

        return new TransitionEndpointHit(null, false);
    }

    private void InitializeTransitionEndpoints(DiagramTransition transition)
    {
        var source = FindNode(transition.SourceId);
        var target = FindNode(transition.TargetId);
        if (source is null || target is null)
        {
            return;
        }

        transition.SourceAngle ??= AngleFromTo(source.Position, target.Position);
        transition.TargetAngle ??= AngleFromTo(target.Position, source.Position);
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
        start = PointOnCircle(source.Position, DiagramNode.Radius, sourceAngle);
        end = PointOnCircle(target.Position, DiagramNode.Radius, targetAngle);
        return true;
    }

    private static Vector2 PointOnCircle(Vector2 center, float radius, float angle)
        => center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;

    private static float AngleFromTo(Vector2 from, Vector2 to)
        => MathF.Atan2(to.Y - from.Y, to.X - from.X);
    private DiagramNode? FindNode(int id) => _nodes.FirstOrDefault(n => n.Id == id);
    private void DrawGrid(int spacing, Color color)
    {
        var width = GraphicsDevice.Viewport.Width;
        var height = GraphicsDevice.Viewport.Height;
        for (var x = 0; x < width; x += spacing)
        {
            DrawLine(new Vector2(x, 0), new Vector2(x, height), color, 1f);
        }
        for (var y = 0; y < height; y += spacing)
        {
            DrawLine(new Vector2(0, y), new Vector2(width, y), color, 1f);
        }
    }
    private void DrawToolbar()
    {
        var bounds = new Rectangle(0, 0, GraphicsDevice.Viewport.Width, 34);
        _spriteBatch.Draw(_pixel, bounds, new Color(18, 20, 24));
        DrawText(_status.ToUpperInvariant(), new Vector2(12, 10), new Color(230, 234, 240), 2);
    }
    private void DrawNode(DiagramNode node, bool selected)
    {
        var fill = Palette[node.ColorIndex % Palette.Length];
        DrawCircle(node.Position, DiagramNode.Radius + 4, selected ? new Color(255, 255, 255) : new Color(10, 12, 16));
        DrawCircle(node.Position, DiagramNode.Radius, fill);
        DrawCircleOutline(node.Position, DiagramNode.Radius, new Color(15, 18, 24), 3f);
        var label = node == _editingNode ? _editingLabel + "_" : node.Label;
        DrawNodeLabel(label, node.Position, node == _editingNode);
    }
    private void DrawNodeLabel(string label, Vector2 center, bool editing)
    {
        var texture = GetLabelTexture(label, editing);
        var position = center - new Vector2(texture.Width / 2f, texture.Height / 2f);
        _spriteBatch.Draw(texture, position, Color.White);
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
        using var font = CreateJapaneseFont(22);
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
    private static DrawingFont CreateJapaneseFont(float size)
    {
        var candidates = new[] { "Yu Gothic UI", "Meiryo", "MS Gothic", "Noto Sans CJK JP", "Arial Unicode MS" };
        foreach (var candidate in candidates)
        {
            if (DrawingFontFamily.Families.Any(f => string.Equals(f.Name, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return new DrawingFont(candidate, size, DrawingFontStyle.Bold, DrawingGraphicsUnit.Pixel);
            }
        }
        return new DrawingFont(DrawingFontFamily.GenericSansSerif, size, DrawingFontStyle.Bold, DrawingGraphicsUnit.Pixel);
    }
    private static readonly DrawingStringFormat CenteredStringFormat = new()
    {
        Alignment = System.Drawing.StringAlignment.Center,
        LineAlignment = System.Drawing.StringAlignment.Center,
        FormatFlags = DrawingStringFormatFlags.NoWrap
    };
    private static readonly DrawingStringFormat StringFormatNoWrap = new()
    {
        FormatFlags = DrawingStringFormatFlags.NoWrap
    };
    private void DrawTransition(DiagramTransition transition, bool selected)
    {
        if (!TryGetTransitionEndpoints(transition, out var start, out var end))
        {
            return;
        }

        if ((end - start).LengthSquared() <= 0.01f)
        {
            return;
        }

        DrawArrow(start, end, selected ? new Color(255, 230, 120) : new Color(185, 195, 210), selected ? 4f : 3f);
        DrawTransitionLabel(transition, start, end, selected);
    }

    private void DrawTransitionHandles(DiagramTransition transition)
    {
        if (!TryGetTransitionEndpoints(transition, out var start, out var end))
        {
            return;
        }

        DrawCircle(start, 8f, new Color(255, 230, 120));
        DrawCircleOutline(start, 8f, new Color(20, 24, 30), 2f);
        DrawCircle(end, 8f, new Color(255, 230, 120));
        DrawCircleOutline(end, 8f, new Color(20, 24, 30), 2f);
    }
    private void DrawTransitionLabel(DiagramTransition transition, Vector2 start, Vector2 end, bool selected)
    {
        var editing = transition == _editingTransition;
        var label = editing ? _editingLabel + "_" : transition.Label;
        if (string.IsNullOrWhiteSpace(label) && !editing)
        {
            return;
        }
        var texture = GetLabelTexture(label, editing || selected);
        var center = GetTransitionLabelCenter(start, end, transition.LabelSide, texture);
        var position = center - new Vector2(texture.Width / 2f, texture.Height / 2f);
        _spriteBatch.Draw(texture, position, Color.White);
    }
    private static Vector2 GetTransitionLabelCenter(Vector2 start, Vector2 end, int labelSide, Texture2D labelTexture)
    {
        var midpoint = (start + end) * 0.5f;
        var delta = end - start;
        var side = labelSide == 0 ? -1f : 1f;
        if (MathF.Abs(delta.X) >= MathF.Abs(delta.Y))
        {
            return midpoint + new Vector2(0, side * (labelTexture.Height / 2f + 18f));
        }
        return midpoint + new Vector2(side * (labelTexture.Width / 2f + 22f), 0);
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
    public const float Radius = 62f;
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public Vector2 Position { get; set; }
    public int ColorIndex { get; set; }
}
public sealed class DiagramTransition
{
    public int SourceId { get; set; }
    public int TargetId { get; set; }
    public string Label { get; set; } = string.Empty;
    public int LabelSide { get; set; }
    public float? SourceAngle { get; set; }
    public float? TargetAngle { get; set; }
}
public sealed record TransitionEndpointHit(DiagramTransition? Transition, bool IsSource);
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
