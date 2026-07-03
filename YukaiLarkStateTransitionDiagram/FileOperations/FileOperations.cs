namespace YukaiLarkStateTransitionDiagram;

using System;
using System.IO;
using System.Linq;
using YukaiLarkStateTransitionDiagram.Persistence;
using Microsoft.Xna.Framework;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

public partial class Game1
{
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
        var document = CaptureDiagramDocument();
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
        if (_isEditingFileName || _isEditingDiagramTabName || IsEditingLabel)
        {
            EndTextInputIme();
        }

        ApplyDiagramDocument(document);
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
        if (_isEditingFileName || _isEditingDiagramTabName || IsEditingLabel)
        {
            EndTextInputIme();
        }

        _diagrams.Clear();
        _diagrams.Add(DiagramInstance.CreateDefault());
        _currentDiagramIndex = 0;
        _selectedNode = null;
        _selectedTransition = null;
        _draggedNode = null;
        _linkSource = null;
        _editingNode = null;
        _editingTransition = null;
        _draggedHandleTransition = null;
        _draggedHandleKind = TransitionHandleKind.None;
        _draggedLabelTransition = null;
        _resizedNode = null;
        _textBoxController.Clear();
        _pendingHistorySnapshot = null;
        _cameraOffset = Vector2.Zero;
        _cameraZoom = 1f;
        _isPanning = false;
        _isExportSelecting = false;
        _exportSelectionDragging = false;
        _hasExportSelection = false;
        _exportSelectionRectangle = Rectangle.Empty;
        _exportDragStartRectangle = Rectangle.Empty;
        _exportDragMode = ExportSelectionDragMode.New;
        _status = DefaultStatus;
    }
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
}