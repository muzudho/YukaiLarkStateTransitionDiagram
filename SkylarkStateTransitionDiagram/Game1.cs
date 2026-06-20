namespace SkylarkStateTransitionDiagram;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

public class Game1 : Game
{
    private const string SaveFileName = "diagram.json";
    private readonly GraphicsDeviceManager _graphics;
    private readonly List<DiagramNode> _nodes = new();
    private readonly List<DiagramTransition> _transitions = new();
    private SpriteBatch _spriteBatch = null!;
    private Texture2D _pixel = null!;
    private MouseState _previousMouse;
    private KeyboardState _previousKeyboard;
    private DiagramNode? _selectedNode;
    private DiagramTransition? _selectedTransition;
    private DiagramNode? _draggedNode;
    private DiagramNode? _linkSource;
    private Vector2 _dragOffset;
    private int _nextNodeId = 1;
    private string _status = "N: add  SHIFT+drag: link  DEL: delete  CTRL+S: save  CTRL+O: load";

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

        if (keyboard.IsKeyDown(Keys.Escape) || GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
        {
            Exit();
        }

        HandleKeyboard(keyboard, mouse);
        HandleMouse(keyboard, mouse);

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

        DrawToolbar();
        _spriteBatch.End();
        base.Draw(gameTime);
    }

    private void HandleKeyboard(KeyboardState keyboard, MouseState mouse)
    {
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
            _status = $"Changed color of {_selectedNode.Label}.";
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

    private void HandleMouse(KeyboardState keyboard, MouseState mouse)
    {
        var mousePosition = mouse.Position.ToVector2();
        var leftPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;
        var leftReleased = mouse.LeftButton == ButtonState.Released && _previousMouse.LeftButton == ButtonState.Pressed;
        var shiftDown = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);

        if (leftPressed)
        {
            var node = FindNodeAt(mousePosition);
            _selectedNode = node;
            _selectedTransition = node is null ? FindTransitionAt(mousePosition) : null;

            if (shiftDown && node is not null)
            {
                _linkSource = node;
                _status = $"Linking from {node.Label}. Release on another state.";
                return;
            }

            if (node is not null)
            {
                _draggedNode = node;
                _dragOffset = mousePosition - node.Position;
                _status = $"Selected {node.Label}.";
            }
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
                    AddTransition(_linkSource.Id, target.Id);
                    _status = $"Linked {_linkSource.Label} -> {target.Label}.";
                }
                _linkSource = null;
            }

            _draggedNode = null;
        }
    }

    private void AddNode(Vector2 position)
    {
        var node = new DiagramNode
        {
            Id = _nextNodeId++,
            Label = $"STATE {_nextNodeId - 1}",
            Position = position,
            ColorIndex = (_nextNodeId - 2) % Palette.Length
        };

        _nodes.Add(node);
        _selectedNode = node;
        _selectedTransition = null;
        _status = $"Added {node.Label}.";
    }

    private void AddTransition(int sourceId, int targetId)
    {
        if (_transitions.Any(t => t.SourceId == sourceId && t.TargetId == targetId))
        {
            _status = "That transition already exists.";
            return;
        }

        _transitions.Add(new DiagramTransition { SourceId = sourceId, TargetId = targetId });
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
        var json = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true, IncludeFields = true });
        File.WriteAllText(path, json);
        _status = $"Saved {SaveFileName}.";
    }

    private void LoadDiagram()
    {
        var path = Path.Combine(AppContext.BaseDirectory, SaveFileName);
        if (!File.Exists(path))
        {
            _status = $"No {SaveFileName} found next to the executable.";
            return;
        }

        var document = JsonSerializer.Deserialize<DiagramDocument>(File.ReadAllText(path), new JsonSerializerOptions { IncludeFields = true });
        if (document is null)
        {
            _status = "Could not load diagram.";
            return;
        }

        _nodes.Clear();
        _transitions.Clear();
        _nodes.AddRange(document.Nodes);
        _transitions.AddRange(document.Transitions);
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

        _nodes[0].Label = "IDEA";
        _nodes[1].Label = "DRAFT";
        _nodes[2].Label = "REVIEW";
        _nodes[3].Label = "DONE";

        AddTransition(_nodes[0].Id, _nodes[1].Id);
        AddTransition(_nodes[1].Id, _nodes[2].Id);
        AddTransition(_nodes[2].Id, _nodes[1].Id);
        AddTransition(_nodes[2].Id, _nodes[3].Id);
        _selectedNode = null;
        _selectedTransition = null;
        _status = "N: add  drag: move  SHIFT+drag: link  C: color  DEL: delete  CTRL+S/O: save/load";
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
            var source = FindNode(transition.SourceId);
            var target = FindNode(transition.TargetId);
            if (source is null || target is null)
            {
                continue;
            }

            if (DistanceToSegment(position, source.Position, target.Position) <= 8f)
            {
                return transition;
            }
        }

        return null;
    }

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

        var labelWidth = PrimitiveText.Measure(node.Label, 3).X;
        DrawText(node.Label, node.Position - new Vector2(labelWidth / 2f, 8), Color.White, 3);
    }

    private void DrawTransition(DiagramTransition transition, bool selected)
    {
        var source = FindNode(transition.SourceId);
        var target = FindNode(transition.TargetId);
        if (source is null || target is null)
        {
            return;
        }

        var direction = Vector2.Normalize(target.Position - source.Position);
        var start = source.Position + direction * (DiagramNode.Radius + 2);
        var end = target.Position - direction * (DiagramNode.Radius + 8);
        DrawArrow(start, end, selected ? new Color(255, 230, 120) : new Color(185, 195, 210), selected ? 4f : 3f);
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

