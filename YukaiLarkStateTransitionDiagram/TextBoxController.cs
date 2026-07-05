namespace YukaiLarkStateTransitionDiagram;

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

/// <summary>
/// TextBoxController のキーボード入力の結果を表す列挙型
/// </summary>
public sealed class TextBoxController
{
    private const double CaretKeyRepeatInitialDelaySeconds = 0.42d;
    private const double CaretKeyRepeatIntervalSeconds = 0.055d;

    /// <summary>
    /// テキストボックスに入力できる最大文字数を保持します。
    /// </summary>
    private readonly int _maxLength;

    private double _leftKeyRepeatCountdown = CaretKeyRepeatInitialDelaySeconds;
    private double _rightKeyRepeatCountdown = CaretKeyRepeatInitialDelaySeconds;

    /// <summary>
    /// IME の確定前の文字列を保持します。
    /// </summary>
    private string _compositionText = string.Empty;

    /// <summary>
    /// TextBoxController のインスタンスを初期化します。
    /// </summary>
    /// <param name="maxLength">テキストボックスに入力できる最大文字数</param>
    public TextBoxController(int maxLength)
    {
        _maxLength = maxLength;
    }

    /// <summary>
    /// テキストボックスの現在のテキストを取得します。
    /// </summary>
    public string Text { get; private set; } = string.Empty;

    /// <summary>
    /// キャレットの位置を保持します。
    /// </summary>
    public int CaretIndex { get; private set; }

    /// <summary>
    /// IME の確定前の文字列が存在するかどうかを示します。
    /// </summary>
    public bool HasComposition => !string.IsNullOrEmpty(_compositionText);

    /// <summary>
    /// テキストボックスの入力を開始します。
    /// </summary>
    /// <param name="text">初期テキスト</param>
    public void Begin(string text)
    {
        Text = text;
        CaretIndex = Text.Length;
        _compositionText = string.Empty;
        ResetCaretKeyRepeat();
    }

    /// <summary>
    /// テキストボックスの内容をクリアします。
    /// </summary>
    public void Clear()
    {
        Text = string.Empty;
        CaretIndex = 0;
        _compositionText = string.Empty;
        ResetCaretKeyRepeat();
    }

    /// <summary>
    /// 指定された文字をテキストボックスに入力します。
    /// </summary>
    /// <param name="character">入力する文字</param>
    /// <returns>入力が成功した場合は true、それ以外の場合は false</returns>
    public bool TryInputCharacter(char character)
    {
        if (char.IsControl(character))
        {
            return true;
        }

        if (Text.Length >= _maxLength)
        {
            return false;
        }

        Text = Text.Insert(CaretIndex, character.ToString());
        CaretIndex++;
        _compositionText = string.Empty;
        return true;
    }

    /// <summary>
    /// IME の確定前の文字列を更新します。
    /// </summary>
    public void UpdateImeComposition()
        => _compositionText = WindowsImeCompositionReader.GetCompositionString();

    /// <summary>
    /// テキストボックスの表示用のテキストを取得します。
    /// </summary>
    /// <returns>表示用のテキスト</returns>
    public string GetDisplayText()
    {
        var composition = GetVisibleComposition();
        return Text[..CaretIndex] + composition + Text[CaretIndex..];
    }

    /// <summary>
    /// IME の確定前の文字列を表示用に加工して取得します。
    /// </summary>
    /// <returns>表示用の IME 確定前の文字列の長さ</returns>
    public int GetDisplayCaretIndex()
        => CaretIndex + GetVisibleComposition().Length;

    /// <summary>
    /// キーボード入力を処理します。
    /// </summary>
    /// <param name="keyboard">現在のキーボード状態</param>
    /// <param name="previousKeyboard">前回のキーボード状態</param>
    /// <param name="gameTime">前回更新からの経過時間</param>
    /// <returns>キーボード入力の結果を表すアクション</returns>
    public TextBoxKeyboardAction HandleKeyboard(KeyboardState keyboard, KeyboardState previousKeyboard, GameTime gameTime)
    {
        if (HasComposition)
        {
            ResetCaretKeyRepeat();
            return TextBoxKeyboardAction.None;
        }

        if (IsNewKeyPress(keyboard, previousKeyboard, Keys.Enter))
        {
            return TextBoxKeyboardAction.Commit;
        }

        if (IsNewKeyPress(keyboard, previousKeyboard, Keys.Escape))
        {
            return TextBoxKeyboardAction.Cancel;
        }

        if (ShouldHandleRepeatedKey(keyboard, previousKeyboard, Keys.Left, ref _leftKeyRepeatCountdown, gameTime) && CaretIndex > 0)
        {
            CaretIndex--;
        }

        if (ShouldHandleRepeatedKey(keyboard, previousKeyboard, Keys.Right, ref _rightKeyRepeatCountdown, gameTime) && CaretIndex < Text.Length)
        {
            CaretIndex++;
        }

        if (IsNewKeyPress(keyboard, previousKeyboard, Keys.Home))
        {
            CaretIndex = 0;
        }

        if (IsNewKeyPress(keyboard, previousKeyboard, Keys.End))
        {
            CaretIndex = Text.Length;
        }

        if (IsNewKeyPress(keyboard, previousKeyboard, Keys.Back) && CaretIndex > 0)
        {
            Text = Text.Remove(CaretIndex - 1, 1);
            CaretIndex--;
        }

        if (IsNewKeyPress(keyboard, previousKeyboard, Keys.Delete) && CaretIndex < Text.Length)
        {
            Text = Text.Remove(CaretIndex, 1);
        }

        return TextBoxKeyboardAction.None;
    }

    /// <summary>
    /// IME の確定前の文字列を表示用に加工して取得します。
    /// </summary>
    /// <returns>表示用の IME 確定前の文字列</returns>
    private string GetVisibleComposition()
    {
        if (string.IsNullOrEmpty(_compositionText))
        {
            return string.Empty;
        }

        var availableLength = Math.Max(0, _maxLength - Text.Length);
        return _compositionText.Length <= availableLength
            ? _compositionText
            : _compositionText[..availableLength];
    }

    private static bool IsNewKeyPress(KeyboardState keyboard, KeyboardState previousKeyboard, Keys key)
        => keyboard.IsKeyDown(key) && previousKeyboard.IsKeyUp(key);

    private static bool ShouldHandleRepeatedKey(
        KeyboardState keyboard,
        KeyboardState previousKeyboard,
        Keys key,
        ref double repeatCountdown,
        GameTime gameTime)
    {
        if (keyboard.IsKeyUp(key))
        {
            repeatCountdown = CaretKeyRepeatInitialDelaySeconds;
            return false;
        }

        if (previousKeyboard.IsKeyUp(key))
        {
            repeatCountdown = CaretKeyRepeatInitialDelaySeconds;
            return true;
        }

        repeatCountdown -= gameTime.ElapsedGameTime.TotalSeconds;
        if (repeatCountdown > 0d)
        {
            return false;
        }

        repeatCountdown += CaretKeyRepeatIntervalSeconds;
        if (repeatCountdown <= 0d)
        {
            repeatCountdown = CaretKeyRepeatIntervalSeconds;
        }

        return true;
    }

    private void ResetCaretKeyRepeat()
    {
        _leftKeyRepeatCountdown = CaretKeyRepeatInitialDelaySeconds;
        _rightKeyRepeatCountdown = CaretKeyRepeatInitialDelaySeconds;
    }
}

/// <summary>
/// TextBoxController.HandleKeyboard の戻り値を表す列挙型です。
/// </summary>
public enum TextBoxKeyboardAction
{
    None,
    Commit,
    Cancel
}
