namespace YukaiLarkStateTransitionDiagram;

using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using YukaiLarkStateTransitionDiagram.Persistence;

public partial class Game1
{
    private const int ColorPaletteSwatchSize = 42;
    private const int ColorPaletteSwatchGap = 10;
    private const int ColorPaletteSwapButtonHeight = 24;
    private static readonly Keys[] ColorPaletteDigitKeys =
    [
        Keys.D1,
        Keys.D2,
        Keys.D3,
        Keys.D4,
        Keys.D5,
        Keys.D6
    ];
    private static readonly Keys[] ColorPaletteNumPadKeys =
    [
        Keys.NumPad1,
        Keys.NumPad2,
        Keys.NumPad3,
        Keys.NumPad4,
        Keys.NumPad5,
        Keys.NumPad6
    ];

    private bool _isColorPaletteOpen;
    private int _colorPaletteSwapSourceIndex = -1;

    private void OpenColorPalette()
    {
        if (_selectedNode is null || _selectedNode.Kind != NodeKind.Normal)
        {
            _isColorPaletteOpen = false;
            _colorPaletteSwapSourceIndex = -1;
            _status = "通常ノードを選択すると、Cで状態色パレットを開けます。";
            return;
        }

        _isColorPaletteOpen = true;
        _colorPaletteSwapSourceIndex = -1;
        _status = "状態色パレット: 1-6またはクリックで色を選択。下の > で隣の色と入れ替えます。";
    }

    private void CloseColorPalette(string status)
    {
        _isColorPaletteOpen = false;
        _colorPaletteSwapSourceIndex = -1;
        EnsureImeClosedForShortcutInput();
        _status = status;
    }

    private void HandleColorPaletteKeyboard(KeyboardState keyboard)
    {
        if (IsNewKeyPress(keyboard, Keys.Escape) || IsNewKeyPress(keyboard, Keys.C))
        {
            CloseColorPalette("状態色パレットを閉じました。");
            return;
        }

        if (TryGetColorPaletteShortcutIndex(keyboard, out var paletteIndex))
        {
            AssignSelectedNodeColorIndex(paletteIndex);
        }
    }

    private bool HandleColorPaletteMouse(MouseState mouse)
    {
        var leftPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;
        var rightPressed = mouse.RightButton == ButtonState.Pressed && _previousMouse.RightButton == ButtonState.Released;
        if (rightPressed)
        {
            CloseColorPalette("状態色パレットを閉じました。");
            return true;
        }

        if (!leftPressed)
        {
            return false;
        }

        var point = mouse.Position;
        for (var i = 0; i < _boardTheme.NormalNodePalette.Length - 1; i++)
        {
            if (GetColorPaletteSwapButtonRectangle(i).Contains(point))
            {
                SwapColorPaletteEntries(i, i + 1);
                return true;
            }
        }

        for (var i = 0; i < _boardTheme.NormalNodePalette.Length; i++)
        {
            if (!GetColorPaletteSwatchRectangle(i).Contains(point))
            {
                continue;
            }

            var keyboard = Keyboard.GetState();
            if (IsShiftDown(keyboard))
            {
                HandleColorPaletteSwapClick(i);
                return true;
            }

            AssignSelectedNodeColorIndex(i);
            return true;
        }

        return GetColorPalettePanelRectangle().Contains(point);
    }

    private void DrawColorPaletteOverlay()
    {
        if (!_isColorPaletteOpen || _selectedNode is null || _selectedNode.Kind != NodeKind.Normal)
        {
            return;
        }

        var panel = GetColorPalettePanelRectangle();
        _spriteBatch.Draw(_pixel, new Rectangle(panel.X + 5, panel.Y + 6, panel.Width, panel.Height), WithAlpha(Blend(_boardTheme.BackgroundColor, Color.Black, 0.42f), 100));
        _spriteBatch.Draw(_pixel, panel, WithAlpha(_boardTheme.PanelBackgroundColor, 246));
        DrawScreenRectangleOutline(panel, WithAlpha(_boardTheme.PanelTopEdgeColor, 230), 2);

        DrawUiText("状態色パレット", new Vector2(panel.X + 16, panel.Y + 12), _boardTheme.PanelPrimaryTextColor, 16, true);
        DrawUiText("1-6/クリックで選択  下の > で隣と入替", new Vector2(panel.X + 16, panel.Y + 38), _boardTheme.PanelSecondaryTextColor, 13, false);

        for (var i = 0; i < _boardTheme.NormalNodePalette.Length; i++)
        {
            DrawColorPaletteSwatch(i);
        }

        for (var i = 0; i < _boardTheme.NormalNodePalette.Length - 1; i++)
        {
            DrawColorPaletteSwapButton(i);
        }
    }

    private void DrawColorPaletteSwatch(int index)
    {
        var bounds = GetColorPaletteSwatchRectangle(index);
        var color = _boardTheme.NormalNodePalette[index];
        var selected = _selectedNode?.ColorIndex % _boardTheme.NormalNodePalette.Length == index;
        var swapSource = _colorPaletteSwapSourceIndex == index;
        var edge = swapSource
            ? _boardTheme.SelectedTransitionLineColor
            : selected ? _boardTheme.SelectedNodeOuterRingColor : _boardTheme.PanelTopEdgeColor;

        _spriteBatch.Draw(_pixel, bounds, color);
        DrawScreenRectangleOutline(bounds, WithAlpha(edge, 245), selected || swapSource ? 3 : 1);

        var keyText = (index + 1).ToString();
        var keyTexture = GetUiTextTexture(keyText, 13, true);
        var keyBounds = new Rectangle(bounds.X + 4, bounds.Y + 4, 18, 18);
        _spriteBatch.Draw(_pixel, keyBounds, WithAlpha(_boardTheme.PanelBackgroundColor, 224));
        DrawUiText(keyText, new Vector2(keyBounds.X + (keyBounds.Width - keyTexture.Width) / 2f, keyBounds.Y + 1), _boardTheme.PanelPrimaryTextColor, 13, true);
    }

    private void DrawColorPaletteSwapButton(int index)
    {
        var bounds = GetColorPaletteSwapButtonRectangle(index);
        var fill = WithAlpha(Blend(_boardTheme.PanelBackgroundColor, _keyCapTheme.FaceColor, 0.18f), 232);
        var edge = WithAlpha(_keyCapTheme.BottomEdgeColor, 220);
        _spriteBatch.Draw(_pixel, bounds, fill);
        DrawScreenRectangleOutline(bounds, edge, 1);

        var label = ">";
        var texture = GetUiTextTexture(label, 16, true);
        var position = new Vector2(
            bounds.X + (bounds.Width - texture.Width) / 2f,
            bounds.Y + (bounds.Height - texture.Height) / 2f - 1f);
        DrawUiText(label, position, _keyCapTheme.LabelTextColor, 16, true);
    }

    private Rectangle GetColorPalettePanelRectangle()
    {
        var viewport = GraphicsDevice.Viewport;
        const int panelWidth = 382;
        const int panelHeight = 150;
        var x = Math.Clamp(viewport.Width - panelWidth - 16, 12, Math.Max(12, viewport.Width - panelWidth - 12));
        var y = Math.Clamp(54, 48, Math.Max(48, viewport.Height - panelHeight - 44));

        if (_selectedNode is not null)
        {
            var nodeScreen = WorldToScreen(_selectedNode.Position);
            x = (int)Math.Clamp(nodeScreen.X - panelWidth / 2f, 12, Math.Max(12, viewport.Width - panelWidth - 12));
            y = (int)Math.Clamp(nodeScreen.Y + (_selectedNode.Radius * _cameraZoom) + 18f, 48, Math.Max(48, viewport.Height - panelHeight - 44));
        }

        return new Rectangle(x, y, panelWidth, panelHeight);
    }

    private Rectangle GetColorPaletteSwatchRectangle(int index)
    {
        var panel = GetColorPalettePanelRectangle();
        var x = panel.X + 16 + index * (ColorPaletteSwatchSize + ColorPaletteSwatchGap);
        return new Rectangle(x, panel.Y + 62, ColorPaletteSwatchSize, ColorPaletteSwatchSize);
    }

    private Rectangle GetColorPaletteSwapButtonRectangle(int index)
    {
        var swatch = GetColorPaletteSwatchRectangle(index);
        return new Rectangle(swatch.X, swatch.Bottom + 8, swatch.Width, ColorPaletteSwapButtonHeight);
    }

    private bool TryGetColorPaletteShortcutIndex(KeyboardState keyboard, out int paletteIndex)
    {
        for (var i = 0; i < ColorPaletteDigitKeys.Length; i++)
        {
            if (IsNewKeyPress(keyboard, ColorPaletteDigitKeys[i]) || IsNewKeyPress(keyboard, ColorPaletteNumPadKeys[i]))
            {
                paletteIndex = i;
                return true;
            }
        }

        paletteIndex = -1;
        return false;
    }

    private void AssignSelectedNodeColorIndex(int paletteIndex)
    {
        if (_selectedNode is null || _selectedNode.Kind != NodeKind.Normal)
        {
            CloseColorPalette("通常ノードを選択すると、Cで状態色パレットを開けます。");
            return;
        }

        var normalizedIndex = Math.Clamp(paletteIndex, 0, _boardTheme.NormalNodePalette.Length - 1);
        ExecuteUndoableChange(() => _selectedNode.ColorIndex = normalizedIndex);
        _colorPaletteSwapSourceIndex = -1;
        _status = $"選択中の状態色を {normalizedIndex + 1} 番にしました。別の状態を選んでもパレットは開いたままです。";
    }

    private void HandleColorPaletteSwapClick(int paletteIndex)
    {
        if (_colorPaletteSwapSourceIndex < 0)
        {
            _colorPaletteSwapSourceIndex = paletteIndex;
            _status = $"入れ替え元 {paletteIndex + 1} 番を選びました。Shift+クリックで入れ替え先を選んでください。";
            return;
        }

        if (_colorPaletteSwapSourceIndex == paletteIndex)
        {
            _colorPaletteSwapSourceIndex = -1;
            _status = "状態色パレットの入れ替えをキャンセルしました。";
            return;
        }

        SwapColorPaletteEntries(_colorPaletteSwapSourceIndex, paletteIndex);
        _colorPaletteSwapSourceIndex = -1;
    }

    private void SwapColorPaletteEntries(int sourceIndex, int targetIndex)
    {
        if (sourceIndex < 0
            || targetIndex < 0
            || sourceIndex >= _boardTheme.NormalNodePalette.Length
            || targetIndex >= _boardTheme.NormalNodePalette.Length
            || sourceIndex == targetIndex)
        {
            return;
        }

        var colors = _boardTheme.NormalNodePalette.ToArray();
        (colors[sourceIndex], colors[targetIndex]) = (colors[targetIndex], colors[sourceIndex]);
        _appConfig.SetNormalNodePaletteOverride(_keyCapTheme.Name, colors);
        AppConfigStore.Save(_appConfig);
        ApplyCurrentBoardTheme();
        _status = $"状態色パレットの {sourceIndex + 1} 番と {targetIndex + 1} 番を入れ替えました。";
    }
}
