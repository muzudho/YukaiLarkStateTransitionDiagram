namespace SkylarkStateTransitionDiagram;
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
    private DiagramTransition? _draggedHandleTransition;
    private TransitionHandleKind _draggedHandleKind;
    private DiagramNode? _resizedNode;
    private Vector2 _dragOffset;
    private Vector2 _cameraOffset;
    private Vector2 _panStartMouse;
    private Vector2 _panStartCamera;
    private bool _isPanning;
    private int _nextNodeId = 1;
    private string _editingLabel = string.Empty;
    private string _status = "N: add  T: node kind  C: color  F2/ENTER: edit label  drag resize handle: size  drag empty area: pan  CTRL+S/O: save/load";
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
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: GetViewMatrix());
        DrawGrid(40, new Color(42, 46, 52));
        foreach (var transition in _transitions)
        {
            DrawTransition(transition, transition == _selectedTransition);
        }
        if (_linkSource is not null)
        {
            var mouse = Mouse.GetState();
            DrawArrow(_linkSource.Position, ScreenToWorld(mouse.Position.ToVector2()), new Color(250, 205, 95), 3f);
        }
        foreach (var node in _nodes)
        {
            DrawNode(node, node == _selectedNode);
        }
        if (_selectedNode is not null)
        {
            DrawNodeResizeHandle(_selectedNode);
        }
        if (_selectedTransition is not null)
        {
            DrawTransitionHandles(_selectedTransition);
        }
        _spriteBatch.End();

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
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
            AddNode(ScreenToWorld(mouse.Position.ToVector2()));
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
        if (IsNewKeyPress(keyboard, Keys.T) && _selectedNode is not null)
        {
            ToggleNodeKind(_selectedNode);
        }
        if (IsNewKeyPress(keyboard, Keys.C) && _selectedNode is not null)
        {
            if (_selectedNode.Kind == NodeKind.Normal)
            {
                _selectedNode.ColorIndex = (_selectedNode.ColorIndex + 1) % Palette.Length;
                _status = "Changed selected state color.";
            }
            else
            {
                _status = "Start and end states use fixed black. Press T to return to normal.";
            }
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
        _resizedNode = null;
        _linkSource = null;
        _status = "Editing state label: type Japanese text, ENTER commits, ESC cancels.";
    }
    private void BeginLabelEdit(DiagramTransition transition)
    {
        _editingNode = null;
        _editingTransition = transition;
        _editingLabel = transition.Label;
        _draggedNode = null;
        _resizedNode = null;
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
            NodeKind.Start => "Changed selected state kind to start.",
            NodeKind.End => "Changed selected state kind to end.",
            _ => "Changed selected state kind to normal."
        };
    }    private void ToggleTransitionLabelSide(DiagramTransition transition)
    {
        transition.LabelSide = transition.LabelSide == 0 ? 1 : 0;
        _status = "Edge label side flipped. Horizontal edges use top/bottom; vertical edges use left/right.";
    }
    private void HandleMouse(KeyboardState keyboard, MouseState mouse)
    {
        var screenMousePosition = mouse.Position.ToVector2();
        var mousePosition = ScreenToWorld(screenMousePosition);
        var leftPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;
        var leftReleased = mouse.LeftButton == ButtonState.Released && _previousMouse.LeftButton == ButtonState.Pressed;
        var shiftDown = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
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
                _status = "Resizing state. Radius snaps to half-grid units.";
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
                    ? "Dragging edge contact point on the node circle."
                    : "Dragging cubic Bezier control point.";
                return;
            }

            var node = FindNodeAt(mousePosition);
            _selectedNode = node;
            _selectedTransition = node is null ? FindTransitionAt(mousePosition) : null;
            if (_selectedTransition is not null)
            {
                _status = "Selected edge. Drag endpoint/control handles, F2/ENTER edits label.";
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
            else if (_selectedTransition is null)
            {
                _isPanning = true;
                _panStartMouse = screenMousePosition;
                _panStartCamera = _cameraOffset;
                _linkSource = null;
                _status = "Panning view. Release the mouse to stop.";
            }
        }
        if (_draggedHandleTransition is not null && mouse.LeftButton == ButtonState.Pressed)
        {
            UpdateTransitionHandle(_draggedHandleTransition, _draggedHandleKind, mousePosition);
        }
        if (_draggedNode is not null && mouse.LeftButton == ButtonState.Pressed)
        {
            _draggedNode.Position = mousePosition - _dragOffset;
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
                        _status = "Linked states. Drag Bezier handles or press F2/ENTER to edit edge label.";
                    }
                }
                _linkSource = null;
            }
            if (_draggedHandleTransition is not null)
            {
                _status = "Edge handle moved. CTRL+S saves it.";
            }
            if (_resizedNode is not null)
            {
                _status = $"State radius set to {_resizedNode.RadiusUnits} half-grid unit(s). CTRL+S saves it.";
            }
            _draggedNode = null;
            _resizedNode = null;
            _draggedHandleTransition = null;
            _draggedHandleKind = TransitionHandleKind.None;
            if (_isPanning)
            {
                _isPanning = false;
                _status = "View panned. Drag empty area to pan again.";
            }
        }
    }
    private void AddNode(Vector2 position)
    {
        var node = new DiagramNode
        {
            Id = _nextNodeId++,
            Label = $"状態{_nextNodeId - 1}",
            Position = position,
            RadiusUnits = DiagramNode.DefaultRadiusUnits,
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
        _status = "N: add  T: node kind  C: color  F2/ENTER: edit label  drag resize handle: size  drag empty area: pan  CTRL+S/O: save/load";
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
    private void DrawGrid(int spacing, Color color)
    {
        var topLeft = ScreenToWorld(Vector2.Zero);
        var bottomRight = ScreenToWorld(new Vector2(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height));
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
    private void DrawToolbar()
    {
        var bounds = new Rectangle(0, 0, GraphicsDevice.Viewport.Width, 34);
        _spriteBatch.Draw(_pixel, bounds, new Color(18, 20, 24));
        DrawText(_status.ToUpperInvariant(), new Vector2(12, 10), new Color(230, 234, 240), 2);
    }
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
