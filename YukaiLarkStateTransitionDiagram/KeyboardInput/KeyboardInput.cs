namespace YukaiLarkStateTransitionDiagram;

using System;
using System.IO;
using YukaiLarkStateTransitionDiagram.Assistants;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

public partial class Game1
{
    private KeyboardState _previousKeyboard;

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (_isEditingFileName)
        {
            if (!_fileNameTextBoxController.TryInputCharacter(e.Character))
            {
                _fileNameEditWarning = "ファイル名は255文字までです。";
                return;
            }

            UpdateFileNameEditWarning();
            return;
        }

        if (_isEditingDiagramTabName)
        {
            if (!_textBoxController.TryInputCharacter(e.Character))
            {
                _status = "タブ名は24文字までです。";
            }
            return;
        }

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
        if (IsControlDown(keyboard) && IsNewKeyPress(keyboard, Keys.W))
        {
            DeleteCurrentDiagramTab();
            return;
        }
        if (IsControlDown(keyboard) && IsNewKeyPress(keyboard, Keys.Tab))
        {
            if (IsShiftDown(keyboard))
            {
                SelectPreviousDiagramTab();
            }
            else
            {
                SelectNextDiagramTab();
            }
            return;
        }
        if (IsControlDown(keyboard) && IsAltDown(keyboard) && IsNewKeyPress(keyboard, Keys.N))
        {
            AddDiagramTab();
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
            OpenFileMenu(isStartup: false);
            return;
        }
        if (IsControlDown(keyboard) && IsNewKeyPress(keyboard, Keys.P))
        {
            BeginPngExportSelection();
            return;
        }
        if (IsNewKeyPress(keyboard, Keys.T))
        {
            OpenThemeMenu();
            return;
        }
        if (TryGetThemeShortcutIndex(keyboard, out var themeIndex))
        {
            ApplyThemeShortcut(themeIndex);
            return;
        }
        if (_yukaiLarkAssistant.ShouldRunFromKeyboard(CreateAssistantContext(), keyboard, _previousKeyboard, out var assistKind))
        {
            RunYukaiLarkAssist(assistKind);
            return;
        }
        if (_yukaiLarkAssistant.ShouldSuppressFromKeyboard(CreateAssistantContext(), keyboard, _previousKeyboard, out var suppressedAssistKind))
        {
            SuppressYukaiLarkAssist(suppressedAssistKind);
            return;
        }
        if (IsAltDown(keyboard) && IsNewKeyPress(keyboard, Keys.Up))
        {
            ExitToParentSubstate();
            return;
        }
        if (IsControlDown(keyboard) && IsAltDown(keyboard) && IsNewKeyPress(keyboard, Keys.Down))
        {
            OpenSubstateLinkMenu();
            return;
        }
        if (IsAltDown(keyboard) && IsNewKeyPress(keyboard, Keys.Down))
        {
            EnterSelectedNodeSubstate();
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
            if (IsNewKeyPress(keyboard, Keys.F2))
            {
                BeginDiagramTabNameEdit();
                return;
            }
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
            if (!TryDeleteSelectedWaypoint())
            {
                DeleteSelection();
            }
        }
        if (IsNewKeyPress(keyboard, Keys.C) && _selectedNode is not null)
        {
            if (_selectedNode.Kind == NodeKind.Normal)
            {
                OpenColorPalette();
            }
            else
            {
                _status = "開始・終了マークの色はテーマに合わせて表示されます。状態の色は通常ノードで変更できます。";
            }
        }
    }

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

    private void HandleDiagramTabNameEditingKeyboard(KeyboardState keyboard)
    {
        switch (_textBoxController.HandleKeyboard(keyboard, _previousKeyboard))
        {
            case TextBoxKeyboardAction.Commit:
                CommitDiagramTabNameEdit();
                break;
            case TextBoxKeyboardAction.Cancel:
                CancelDiagramTabNameEdit();
                break;
        }
    }
    private void BeginLabelEdit(DiagramNode node)
    {
        _editingNode = node;
        _editingTransition = null;
        _textBoxController.Begin(node.Label);
        BeginTextInputIme();
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
        BeginTextInputIme();
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
                ? "遷移ラベルを更新しました。ラベルはドラッグで移動できます。"
                : "開始マークから最初の状態へ入る遷移にはイベントを付けられません。";
        }
        EndTextInputIme();
        _editingNode = null;
        _editingTransition = null;
        _textBoxController.Clear();
    }
    private void CancelLabelEdit()
    {
        EndTextInputIme();
        _editingNode = null;
        _editingTransition = null;
        _textBoxController.Clear();
        _status = "ラベル編集をキャンセルしました。";
    }

    private void HandleFileNameEditingKeyboard(KeyboardState keyboard)
    {
        switch (_fileNameTextBoxController.HandleKeyboard(keyboard, _previousKeyboard))
        {
            case TextBoxKeyboardAction.Commit:
                CommitFileNameEdit();
                break;
            case TextBoxKeyboardAction.Cancel:
                CancelFileNameEdit();
                break;
            default:
                UpdateFileNameEditWarning();
                break;
        }
    }

    private void BeginFileNameEdit()
    {
        if (_currentFilePath is null)
        {
            _status = "未保存の図はまだ保存先がありません。Ctrl+Sで保存先とファイル名を指定してください。";
            return;
        }

        _isEditingFileName = true;
        _editingNode = null;
        _editingTransition = null;
        _draggedNode = null;
        _resizedNode = null;
        _linkSource = null;
        _isPanning = false;
        _fileNameTextBoxController.Begin(Path.GetFileName(_currentFilePath));
        BeginTextInputIme();
        UpdateFileNameEditWarning();
        _status = "ファイル名を編集中です。Enterで変更、Escでキャンセルします。";
    }

    private void CommitFileNameEdit()
    {
        if (!TryValidateFileNameEdit(_fileNameTextBoxController.Text, out var targetPath, out var warning))
        {
            _fileNameEditWarning = warning;
            return;
        }

        if (_currentFilePath is null)
        {
            CancelFileNameEdit();
            return;
        }

        var oldPath = _currentFilePath;
        if (string.Equals(Path.GetFullPath(oldPath), Path.GetFullPath(targetPath), StringComparison.Ordinal))
        {
            _status = "ファイル名は変更されていません。";
            CancelFileNameEdit(keepStatus: true);
            return;
        }

        try
        {
            MoveFileAllowingCaseChange(oldPath, targetPath);
            _currentFilePath = targetPath;
            _appConfig.RecentFiles.RemoveAll(file => string.Equals(file, oldPath, StringComparison.OrdinalIgnoreCase));
            RememberDiagramFile(targetPath);
            _status = $"ファイル名を {Path.GetFileName(targetPath)} に変更しました。";
            CancelFileNameEdit(keepStatus: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _fileNameEditWarning = "ファイル名を変更できませんでした。権限、使用中、パスの長さを確認してください。";
        }
    }

    private void CancelFileNameEdit(bool keepStatus = false)
    {
        EndTextInputIme();
        _isEditingFileName = false;
        _fileNameTextBoxController.Clear();
        _fileNameEditWarning = string.Empty;
        if (!keepStatus)
        {
            _status = "ファイル名編集をキャンセルしました。";
        }
    }

    private void UpdateFileNameEditWarning()
    {
        TryValidateFileNameEdit(_fileNameTextBoxController.Text, out _, out _fileNameEditWarning);
    }

    private bool TryValidateFileNameEdit(string text, out string targetPath, out string warning)
    {
        targetPath = string.Empty;
        var fileName = text;
        if (_currentFilePath is null)
        {
            warning = "未保存の図はCtrl+Sで保存先を指定してください。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            warning = "ファイル名を入力してください。";
            return false;
        }

        if (fileName.StartsWith(' '))
        {
            warning = "先頭が空白のファイル名は避けてください。";
            return false;
        }

        if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            warning = "ファイル名に使えない文字が入っています。";
            return false;
        }

        if (fileName.EndsWith(' ') || fileName.EndsWith('.'))
        {
            warning = "Windowsでは末尾が空白またはピリオドのファイル名は使えません。";
            return false;
        }

        if (fileName.Length > MaxFileNameLength)
        {
            warning = "ファイル名は255文字までです。";
            return false;
        }

        if (IsReservedWindowsFileName(fileName))
        {
            warning = "Windowsの予約名はファイル名に使えません。";
            return false;
        }

        var directory = Path.GetDirectoryName(_currentFilePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            warning = "保存先フォルダーを確認できません。Ctrl+Shift+Sで保存し直してください。";
            return false;
        }

        try
        {
            targetPath = Path.GetFullPath(Path.Combine(directory, fileName));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            warning = "ファイル名から作るパスが無効です。";
            return false;
        }

        var currentFullPath = Path.GetFullPath(_currentFilePath);
        if (targetPath.Length >= 260)
        {
            warning = "フルパスが長すぎます。短いファイル名にしてください。";
            return false;
        }

        if (File.Exists(targetPath) && !string.Equals(targetPath, currentFullPath, StringComparison.OrdinalIgnoreCase))
        {
            warning = "同じ名前のファイルが既にあります。別の名前にしてください。";
            return false;
        }

        warning = string.Empty;
        return true;
    }

    private static void MoveFileAllowingCaseChange(string oldPath, string targetPath)
    {
        var oldFullPath = Path.GetFullPath(oldPath);
        var targetFullPath = Path.GetFullPath(targetPath);
        if (!string.Equals(oldFullPath, targetFullPath, StringComparison.OrdinalIgnoreCase))
        {
            File.Move(oldPath, targetPath);
            return;
        }

        var directory = Path.GetDirectoryName(oldFullPath) ?? string.Empty;
        var temporaryPath = Path.Combine(directory, $".{Guid.NewGuid():N}.rename.tmp");
        File.Move(oldFullPath, temporaryPath);
        try
        {
            File.Move(temporaryPath, targetFullPath);
        }
        catch
        {
            if (File.Exists(temporaryPath) && !File.Exists(oldFullPath))
            {
                File.Move(temporaryPath, oldFullPath);
            }

            throw;
        }
    }

    private static bool IsReservedWindowsFileName(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName).TrimEnd(' ', '.');
        if (string.IsNullOrEmpty(stem))
        {
            return false;
        }

        var upper = stem.ToUpperInvariant();
        return upper is "CON" or "PRN" or "AUX" or "NUL"
            || IsReservedWindowsDeviceName(upper, "COM")
            || IsReservedWindowsDeviceName(upper, "LPT");
    }

    private static bool IsReservedWindowsDeviceName(string stem, string prefix)
        => stem.Length == 4
            && stem.StartsWith(prefix, StringComparison.Ordinal)
            && stem[3] is >= '1' and <= '9';

    private bool IsNewKeyPress(KeyboardState keyboard, Keys key)
        => keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
    private static bool IsControlDown(KeyboardState keyboard)
        => keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl);
    private static bool IsShiftDown(KeyboardState keyboard)
        => keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
    private static bool IsAltDown(KeyboardState keyboard)
        => keyboard.IsKeyDown(Keys.LeftAlt) || keyboard.IsKeyDown(Keys.RightAlt);
}
