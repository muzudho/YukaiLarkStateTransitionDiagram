namespace YukaiLarkStateTransitionDiagram;

using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

public partial class Game1
{
    private const int ExportPhotoImageMargin = 34;
    private const int ExportPhotoPaperSidePadding = 16;
    private const int ExportPhotoPaperTopPadding = 18;
    private const int ExportPhotoPaperBottomPadding = 54;
    private const int ExportPhotoOuterBottomPadding = 10;
    private const int ExportSelectionPadding = 40;
    private const int ExportSelectionDefaultWidth = 640;
    private const int ExportSelectionDefaultHeight = 360;
    private const float ExportFlashDurationSeconds = 0.18f;
    private const float ExportPhotoPreviewDurationSeconds = 1.35f;

    private bool _isExportSelecting;
    private bool _exportSelectionDragging;
    private bool _hasExportSelection;
    private Rectangle _exportSelectionRectangle;
    private Rectangle _exportPhotoPreviewRectangle;
    private Rectangle _exportDragStartRectangle;
    private Vector2 _exportDragStart;
    private ExportSelectionDragMode _exportDragMode;
    private float _exportFlashSecondsRemaining;
    private float _exportPhotoPreviewSecondsRemaining;

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
                StartExportPhotoEffect(_exportSelectionRectangle);
                _isExportSelecting = false;
                _exportSelectionDragging = false;
                _hasExportSelection = false;
                _exportDragMode = ExportSelectionDragMode.None;
            }
        }
    }

    private void HandleExportSelectionMouse(KeyboardState keyboard, MouseState mouse)
    {
        var screenPosition = mouse.Position.ToVector2();
        var leftPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;
        var leftReleased = mouse.LeftButton == ButtonState.Released && _previousMouse.LeftButton == ButtonState.Pressed;
        var rightPressed = mouse.RightButton == ButtonState.Pressed && _previousMouse.RightButton == ButtonState.Released;
        var snapSelection = !IsAltDown(keyboard);

        if (rightPressed)
        {
            CancelPngExportSelection("PNG出力をキャンセルしました。");
            return;
        }

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
                _status = "PNG範囲を作成中です。左ドラッグで範囲、Alt中は吸着なし、右クリックでキャンセル。";
            }
            else
            {
                _status = "PNG範囲を調整中です。左ドラッグで調整、Alt中は吸着なし、右クリックでキャンセル。";
            }
            return;
        }

        if (_exportSelectionDragging && mouse.LeftButton == ButtonState.Pressed)
        {
            UpdateExportSelectionDrag(screenPosition, snapSelection);
        }

        if (leftReleased && _exportSelectionDragging)
        {
            UpdateExportSelectionDrag(screenPosition, snapSelection);
            _exportSelectionDragging = false;
            _exportDragMode = ExportSelectionDragMode.None;
            if (_exportSelectionRectangle.Width < 16 || _exportSelectionRectangle.Height < 16)
            {
                _hasExportSelection = false;
                _status = "PNG出力範囲が小さすぎます。左ドラッグで範囲を作り直してください。";
                return;
            }

            _hasExportSelection = true;
            _status = snapSelection
                ? "PNG範囲を半グリッドに吸着しました。左ドラッグで調整、Enterで撮影、右クリックでキャンセル。"
                : "PNG範囲を自由位置にしました。Altを離すと吸着、Enterで撮影、右クリックでキャンセル。";
        }
    }

    private void BeginPngExportSelection()
    {
        _isExportSelecting = true;
        _exportSelectionDragging = false;
        _exportSelectionRectangle = CreateInitialExportSelectionRectangle();
        _hasExportSelection = _exportSelectionRectangle.Width >= 16 && _exportSelectionRectangle.Height >= 16;
        _exportDragStartRectangle = Rectangle.Empty;
        _exportDragMode = ExportSelectionDragMode.None;
        _draggedNode = null;
        _resizedNode = null;
        _draggedHandleTransition = null;
        _draggedHandleKind = TransitionHandleKind.None;
        _draggedLabelTransition = null;
        _linkSource = null;
        _isPanning = false;
        _status = "PNG出力モードです。枠をドラッグで調整、Enterで撮影、右クリック/Escでキャンセル。";
    }

    private Rectangle CreateInitialExportSelectionRectangle()
    {
        if (!TryGetDiagramScreenBounds(out var bounds))
        {
            return CreateDefaultExportSelectionRectangle();
        }

        var rectangle = RectangleFromEdges(
            (int)MathF.Floor(bounds.Left) - ExportSelectionPadding,
            (int)MathF.Floor(bounds.Top) - ExportSelectionPadding,
            (int)MathF.Ceiling(bounds.Right) + ExportSelectionPadding,
            (int)MathF.Ceiling(bounds.Bottom) + ExportSelectionPadding);
        rectangle = EnsureMinimumExportSelectionSize(rectangle);
        var clamped = ClampExportSelectionRectangle(rectangle);
        return clamped.Width >= 16 && clamped.Height >= 16
            ? clamped
            : CreateDefaultExportSelectionRectangle();
    }

    private bool TryGetDiagramScreenBounds(out Rectangle bounds)
    {
        var hasBounds = false;
        var left = 0f;
        var top = 0f;
        var right = 0f;
        var bottom = 0f;

        void Include(Vector2 point)
        {
            var screenPoint = WorldToScreen(point);
            if (!hasBounds)
            {
                left = right = screenPoint.X;
                top = bottom = screenPoint.Y;
                hasBounds = true;
                return;
            }

            left = MathF.Min(left, screenPoint.X);
            top = MathF.Min(top, screenPoint.Y);
            right = MathF.Max(right, screenPoint.X);
            bottom = MathF.Max(bottom, screenPoint.Y);
        }

        foreach (var node in _nodes)
        {
            Include(node.Position - new Vector2(node.Radius));
            Include(node.Position + new Vector2(node.Radius));
        }

        foreach (var transition in _transitions)
        {
            if (!TryGetTransitionGeometry(transition, out var start, out var control1, out var control2, out var end))
            {
                continue;
            }

            Include(start);
            Include(control1);
            Include(control2);
            foreach (var waypoint in transition.Waypoints)
            {
                Include(waypoint);
            }
            foreach (var controls in transition.SegmentControls)
            {
                if (controls.ControlPoint1.HasValue)
                {
                    Include(controls.ControlPoint1.Value);
                }
                if (controls.ControlPoint2.HasValue)
                {
                    Include(controls.ControlPoint2.Value);
                }
            }
            Include(end);
            if (TryGetTransitionLabelBounds(transition, out var labelTopLeft, out var labelSize))
            {
                Include(labelTopLeft);
                Include(labelTopLeft + labelSize);
            }
        }

        bounds = hasBounds
            ? RectangleFromEdges((int)MathF.Floor(left), (int)MathF.Floor(top), (int)MathF.Ceiling(right), (int)MathF.Ceiling(bottom))
            : Rectangle.Empty;
        return hasBounds;
    }

    private Rectangle CreateDefaultExportSelectionRectangle()
    {
        var viewport = GraphicsDevice.Viewport;
        var width = Math.Min(ExportSelectionDefaultWidth, Math.Max(160, viewport.Width - ExportSelectionPadding * 2));
        var height = Math.Min(ExportSelectionDefaultHeight, Math.Max(120, viewport.Height - ExportSelectionPadding * 2));
        return new Rectangle((viewport.Width - width) / 2, (viewport.Height - height) / 2, width, height);
    }

    private Rectangle EnsureMinimumExportSelectionSize(Rectangle rectangle)
    {
        var width = Math.Max(rectangle.Width, Math.Min(ExportSelectionDefaultWidth, GraphicsDevice.Viewport.Width));
        var height = Math.Max(rectangle.Height, Math.Min(ExportSelectionDefaultHeight, GraphicsDevice.Viewport.Height));
        var centerX = rectangle.X + rectangle.Width / 2;
        var centerY = rectangle.Y + rectangle.Height / 2;
        return new Rectangle(centerX - width / 2, centerY - height / 2, width, height);
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

    private void UpdateExportSelectionDrag(Vector2 screenPosition, bool snapSelection)
    {
        var rectangle = _exportDragMode == ExportSelectionDragMode.New
            ? RectangleFromPoints(snapSelection ? SnapScreenToHalfGrid(_exportDragStart) : _exportDragStart, snapSelection ? SnapScreenToHalfGrid(screenPosition) : screenPosition)
            : ResizeExportSelection(_exportDragStartRectangle, _exportDragMode, screenPosition - _exportDragStart, snapSelection);
        _exportSelectionRectangle = ClampExportSelectionRectangle(rectangle);
        _hasExportSelection = _exportSelectionRectangle.Width >= 16 && _exportSelectionRectangle.Height >= 16;
    }

    private Rectangle ResizeExportSelection(Rectangle rectangle, ExportSelectionDragMode mode, Vector2 delta, bool snapSelection)
    {
        var left = rectangle.Left;
        var top = rectangle.Top;
        var right = rectangle.Right;
        var bottom = rectangle.Bottom;
        var dx = (int)MathF.Round(delta.X);
        var dy = (int)MathF.Round(delta.Y);

        if (mode == ExportSelectionDragMode.Move)
        {
            var x = rectangle.X + dx;
            var y = rectangle.Y + dy;
            if (snapSelection)
            {
                var snapped = SnapScreenToHalfGrid(new Vector2(x, y));
                x = (int)MathF.Round(snapped.X);
                y = (int)MathF.Round(snapped.Y);
            }
            return ClampMovedExportSelection(new Rectangle(x, y, rectangle.Width, rectangle.Height));
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

        if (snapSelection)
        {
            if (mode is ExportSelectionDragMode.Left or ExportSelectionDragMode.TopLeft or ExportSelectionDragMode.BottomLeft)
            {
                left = SnapScreenX(left);
            }
            if (mode is ExportSelectionDragMode.Right or ExportSelectionDragMode.TopRight or ExportSelectionDragMode.BottomRight)
            {
                right = SnapScreenX(right);
            }
            if (mode is ExportSelectionDragMode.Top or ExportSelectionDragMode.TopLeft or ExportSelectionDragMode.TopRight)
            {
                top = SnapScreenY(top);
            }
            if (mode is ExportSelectionDragMode.Bottom or ExportSelectionDragMode.BottomLeft or ExportSelectionDragMode.BottomRight)
            {
                bottom = SnapScreenY(bottom);
            }
        }

        return RectangleFromEdges(left, top, right, bottom);
    }

    private Vector2 SnapScreenToHalfGrid(Vector2 screenPosition)
        => WorldToScreen(SnapToHalfGrid(ScreenToWorld(screenPosition)));

    private int SnapScreenX(int x)
        => (int)MathF.Round(SnapScreenToHalfGrid(new Vector2(x, 0)).X);

    private int SnapScreenY(int y)
        => (int)MathF.Round(SnapScreenToHalfGrid(new Vector2(0, y)).Y);
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

        _spriteBatch.Begin(samplerState: SamplerState.LinearClamp);
        DrawExportPhotoFrame(new Rectangle(0, 0, imageWidth, imageHeight), imageArea, fillBackdrop: true, fillImageArea: true);
        _spriteBatch.End();

        var worldTopLeft = ScreenToWorld(new Vector2(selection.X, selection.Y));
        var worldBottomRight = ScreenToWorld(new Vector2(selection.Right, selection.Bottom));
        var exportTransform = Matrix.CreateScale(_cameraZoom) * Matrix.CreateTranslation(-worldTopLeft.X * _cameraZoom + imageArea.X, -worldTopLeft.Y * _cameraZoom + imageArea.Y, 0f);
        var previousScissor = GraphicsDevice.ScissorRectangle;
        using var scissorRasterizer = new RasterizerState { ScissorTestEnable = true };
        GraphicsDevice.ScissorRectangle = imageArea;
        _spriteBatch.Begin(samplerState: SamplerState.LinearClamp, rasterizerState: scissorRasterizer, transformMatrix: exportTransform);
        DrawGrid(40, _boardTheme.GridColor, worldTopLeft, worldBottomRight);
        DrawDiagramContent(includeInteraction: false, TimeSpan.Zero);
        _spriteBatch.End();
        GraphicsDevice.ScissorRectangle = previousScissor;

        _spriteBatch.Begin(samplerState: SamplerState.LinearClamp);
        DrawExportPhotoTop(imageArea);
        _spriteBatch.End();

        GraphicsDevice.SetRenderTargets(previousTargets);
        using var stream = File.Create(path);
        renderTarget.SaveAsPng(stream, imageWidth, imageHeight);
    }

    private static string CreateDefaultPngFileName()
        => $"{DateTime.Now:yyyyMMddHHmmss}_diagram.png";

    private void DrawExportSelectionOverlay()
    {
        if (!_isExportSelecting || (!_hasExportSelection && !_exportSelectionDragging)) return;

        var rectangle = GetExportSelectionRectangle();
        if (rectangle.Width <= 0 || rectangle.Height <= 0) return;

        DrawExportPhotoFrame(new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), rectangle, fillBackdrop: false, fillImageArea: false);
        DrawExportPhotoTop(rectangle);
        DrawScreenRectangleOutline(rectangle, new Color(255, 236, 150), 2);
        DrawExportSelectionHandles(rectangle);
        DrawExportSelectionInstruction(rectangle);
    }

    private void UpdateExportPhotoEffect(GameTime gameTime)
    {
        var elapsedSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _exportFlashSecondsRemaining = MathF.Max(0f, _exportFlashSecondsRemaining - elapsedSeconds);
        _exportPhotoPreviewSecondsRemaining = MathF.Max(0f, _exportPhotoPreviewSecondsRemaining - elapsedSeconds);
    }

    private void StartExportPhotoEffect(Rectangle selection)
    {
        _exportPhotoPreviewRectangle = selection;
        _exportFlashSecondsRemaining = ExportFlashDurationSeconds;
        _exportPhotoPreviewSecondsRemaining = ExportPhotoPreviewDurationSeconds;
    }

    private void DrawExportPhotoEffectOverlay()
    {
        if (_exportPhotoPreviewSecondsRemaining > 0f && _exportPhotoPreviewRectangle.Width > 0 && _exportPhotoPreviewRectangle.Height > 0)
        {
            var previewAlpha = MathHelper.Clamp(_exportPhotoPreviewSecondsRemaining / ExportPhotoPreviewDurationSeconds, 0f, 1f);
            DrawExportPhotoFrame(new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), _exportPhotoPreviewRectangle, fillBackdrop: false, fillImageArea: false);
            DrawExportPhotoTop(_exportPhotoPreviewRectangle);
            DrawScreenRectangleOutline(_exportPhotoPreviewRectangle, new Color((byte)255, (byte)236, (byte)150, (byte)(190 * previewAlpha)), 2);
        }

        if (_exportFlashSecondsRemaining <= 0f)
        {
            return;
        }

        var flashAlpha = MathHelper.Clamp(_exportFlashSecondsRemaining / ExportFlashDurationSeconds, 0f, 1f);
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), new Color((byte)255, (byte)255, (byte)255, (byte)(210 * flashAlpha)));
    }

    private void DrawExportSelectionInstruction(Rectangle rectangle)
    {
        const string instruction = "ドラッグで調整 / Enterで撮影 / Escでキャンセル";
        var textWidth = GetUiTextTexture(instruction, 15, true).Width;
        var width = (int)MathF.Ceiling(textWidth) + 28;
        var height = 34;
        var x = Math.Clamp(rectangle.X + rectangle.Width / 2 - width / 2, 12, GraphicsDevice.Viewport.Width - width - 12);
        var y = rectangle.Bottom + ExportPhotoPaperBottomPadding + 14;
        if (y + height > GraphicsDevice.Viewport.Height - 58)
        {
            y = rectangle.Y - ExportPhotoPaperTopPadding - height - 12;
        }

        y = Math.Clamp(y, 62, GraphicsDevice.Viewport.Height - height - 58);
        var panel = new Rectangle(x, y, width, height);
        _spriteBatch.Draw(_pixel, panel, new Color(42, 36, 30, 220));
        DrawScreenRectangleOutline(panel, new Color(255, 236, 150, 180), 1);
        DrawUiText(instruction, new Vector2(panel.X + 14, panel.Y + 7), new Color(255, 246, 210), 15, true);
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
        _primitiveRenderer.DrawCircle(center + new Vector2(2, 3), 10f, new Color(20, 14, 12, 95));
        _primitiveRenderer.DrawCircle(center, 9f, color);
        _primitiveRenderer.DrawCircleOutline(center, 9f, new Color(80, 42, 36, 170), 2f);
        _primitiveRenderer.DrawCircle(center - new Vector2(3, 3), 3f, new Color(255, 244, 220, 185));
    }
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