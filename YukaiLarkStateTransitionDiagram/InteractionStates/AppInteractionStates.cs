namespace YukaiLarkStateTransitionDiagram;

using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

public partial class Game1
{
    private readonly IAppInteractionState[] _interactionStates =
    {
        new FileMenuInteractionState(),
        new ThemeMenuInteractionState(),
        new SubstateLinkMenuInteractionState(),
        new ColorPaletteInteractionState(),
        new PngExportInteractionState(),
        new FileNameEditingInteractionState(),
        new DiagramTabNameEditingInteractionState(),
        new LabelEditingInteractionState(),
        new NeutralInteractionState()
    };

    private IAppInteractionState GetCurrentInteractionState()
        => _interactionStates.First(state => state.IsActive(this));

    private interface IAppInteractionState
    {
        bool UpdatesAssistantBeforeInput => false;

        bool IsActive(Game1 game);

        bool UsesTextInput(Game1 game) => false;

        void Update(Game1 game, KeyboardState keyboard, MouseState mouse, GameTime gameTime);
    }

    /// <summary>
    /// ファイルメニューが開いている状態の操作状態
    /// </summary>
    private sealed class FileMenuInteractionState : IAppInteractionState
    {
        public bool IsActive(Game1 game) => game._isFileMenuOpen;

        public void Update(Game1 game, KeyboardState keyboard, MouseState mouse, GameTime gameTime)
        {
            game.HandleFileMenuKeyboard(keyboard);
            game.HandleFileMenuMouse(mouse);
        }
    }

    /// <summary>
    /// テーマメニュー
    /// </summary>
    private sealed class ThemeMenuInteractionState : IAppInteractionState
    {
        public bool IsActive(Game1 game) => game._isThemeMenuOpen;

        public void Update(Game1 game, KeyboardState keyboard, MouseState mouse, GameTime gameTime)
        {
            game.HandleThemeMenuKeyboard(keyboard);
            game.HandleThemeMenuMouse(mouse);
        }
    }

    /// <summary>
    /// サブステート・リンクメニューオープン時
    /// </summary>
    private sealed class SubstateLinkMenuInteractionState : IAppInteractionState
    {
        public bool IsActive(Game1 game) => game._isSubstateLinkMenuOpen;

        public void Update(Game1 game, KeyboardState keyboard, MouseState mouse, GameTime gameTime)
        {
            game.HandleSubstateLinkMenuKeyboard(keyboard);
            game.HandleSubstateLinkMenuMouse(mouse);
        }
    }

    /// <summary>
    /// カラーパレット
    /// </summary>
    private sealed class ColorPaletteInteractionState : IAppInteractionState
    {
        public bool IsActive(Game1 game) => game._isColorPaletteOpen;

        public void Update(Game1 game, KeyboardState keyboard, MouseState mouse, GameTime gameTime)
        {
            game.HandleColorPaletteKeyboard(keyboard);
            var paletteConsumedMouse = game.HandleColorPaletteMouse(mouse);
            if (game._isColorPaletteOpen && !paletteConsumedMouse)
            {
                game.HandleMouse(keyboard, mouse);
            }
        }
    }

    /// <summary>
    /// PNG画像エクスポート
    /// </summary>
    private sealed class PngExportInteractionState : IAppInteractionState
    {
        public bool UpdatesAssistantBeforeInput => true;

        public bool IsActive(Game1 game) => game._isExportSelecting;

        public void Update(Game1 game, KeyboardState keyboard, MouseState mouse, GameTime gameTime)
        {
            game.HandleExportSelectionKeyboard(keyboard);
            if (game._isExportSelecting)
            {
                game.HandleExportSelectionMouse(keyboard, mouse);
            }
        }
    }

    /// <summary>
    /// ファイル名編集中
    /// </summary>
    private sealed class FileNameEditingInteractionState : IAppInteractionState
    {
        public bool UpdatesAssistantBeforeInput => true;

        public bool IsActive(Game1 game) => game._isEditingFileName;

        public bool UsesTextInput(Game1 game) => true;

        public void Update(Game1 game, KeyboardState keyboard, MouseState mouse, GameTime gameTime)
        {
            game._fileNameTextBoxController.UpdateImeComposition();
            game.HandleFileNameEditingKeyboard(keyboard, gameTime);
            if (game._isEditingFileName)
            {
                game.HandleTextEditingMouse(mouse);
            }
        }
    }

    /// <summary>
    /// ダイアグラム・タブ名編集中
    /// </summary>
    private sealed class DiagramTabNameEditingInteractionState : IAppInteractionState
    {
        public bool UpdatesAssistantBeforeInput => true;

        public bool IsActive(Game1 game) => game._isEditingDiagramTabName;

        public bool UsesTextInput(Game1 game) => true;

        public void Update(Game1 game, KeyboardState keyboard, MouseState mouse, GameTime gameTime)
        {
            game._textBoxController.UpdateImeComposition();
            game.HandleDiagramTabNameEditingKeyboard(keyboard, gameTime);
            if (game._isEditingDiagramTabName)
            {
                game.HandleTextEditingMouse(mouse);
            }
        }
    }

    /// <summary>
    /// ラベル編集中
    /// </summary>
    private sealed class LabelEditingInteractionState : IAppInteractionState
    {
        public bool UpdatesAssistantBeforeInput => true;

        public bool IsActive(Game1 game) => game.IsEditingLabel;

        public bool UsesTextInput(Game1 game) => true;

        public void Update(Game1 game, KeyboardState keyboard, MouseState mouse, GameTime gameTime)
        {
            game._textBoxController.UpdateImeComposition();
            game.HandleLabelEditingKeyboard(keyboard, gameTime);
            if (game.IsEditingLabel)
            {
                game.HandleTextEditingMouse(mouse);
            }
        }
    }

    /// <summary>
    /// ニュートラル時
    /// </summary>
    private sealed class NeutralInteractionState : IAppInteractionState
    {
        public bool UpdatesAssistantBeforeInput => true;

        public bool IsActive(Game1 game) => true;

        public void Update(Game1 game, KeyboardState keyboard, MouseState mouse, GameTime gameTime)
        {
            game._textBoxController.Clear();
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
            {
                game.Exit();
            }

            game.HandleKeyboard(keyboard, mouse);
            game.HandleMouse(keyboard, mouse);
        }
    }
}
