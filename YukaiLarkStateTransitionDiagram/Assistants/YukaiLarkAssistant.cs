namespace YukaiLarkStateTransitionDiagram.Assistants;

using System;
using YukaiLarkStateTransitionDiagram;
using YukaiLarkStateTransitionDiagram.Theme;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

internal sealed class YukaiLarkAssistant
{
    private const double AssistWakeSeconds = 1.2;
    private const double AssistGhostDelaySeconds = 0.7;
    private const double OptionalAssistSkipSeconds = 6.0;
    private const double CompletedAssistDisplaySeconds = 5.0;
    private const int MascotTargetWidth = 176;

    private double _assistSeconds;
    private double _completedAssistSeconds;
    private YukaiLarkAssistKind _activeKind;
    private YukaiLarkAssistKind _completedKind;
    private bool _skipSecondStateNode;

    public Rectangle MascotBounds { get; private set; }
    public Rectangle CutInBandBounds { get; private set; }
    public Rectangle AssistAcceptButtonBounds { get; private set; }
    public Rectangle AssistDeclineButtonBounds { get; private set; }

    private bool IsAssistReady => _assistSeconds >= AssistWakeSeconds;
    private bool IsAssistGhostReady => _assistSeconds >= AssistWakeSeconds + AssistGhostDelaySeconds;
    private bool IsCompletedAssistActive => _completedKind != YukaiLarkAssistKind.None && _completedAssistSeconds > 0;

    public string Update(GameTime gameTime, YukaiLarkAssistantContext context, string currentStatus, string defaultStatus)
    {
        if (context.NormalNodeCount != 1 || !context.HasStartToNormalTransition)
        {
            _skipSecondStateNode = false;
        }

        if (_completedAssistSeconds > 0)
        {
            _completedAssistSeconds = Math.Max(0, _completedAssistSeconds - gameTime.ElapsedGameTime.TotalSeconds);
            if (_completedAssistSeconds <= 0)
            {
                _completedKind = YukaiLarkAssistKind.None;
            }
            else
            {
                Reset();
                return currentStatus;
            }
        }

        var nextKind = GetAssistKind(context);
        if (nextKind == YukaiLarkAssistKind.None)
        {
            Reset();
            return currentStatus;
        }

        if (nextKind != _activeKind)
        {
            _activeKind = nextKind;
            _assistSeconds = 0;
        }

        _assistSeconds += gameTime.ElapsedGameTime.TotalSeconds;
        if (nextKind == YukaiLarkAssistKind.CreateSecondStateNode && _assistSeconds >= AssistWakeSeconds + OptionalAssistSkipSeconds)
        {
            _skipSecondStateNode = true;
            Reset();
            return IsAssistantStatus(currentStatus) ? defaultStatus : currentStatus;
        }

        return IsAssistReady && (currentStatus == defaultStatus || IsAssistantStatus(currentStatus))
            ? GetStatusText(context, nextKind)
            : currentStatus;
    }

    public bool ShouldRunFromKeyboard(YukaiLarkAssistantContext context, KeyboardState keyboard, KeyboardState previousKeyboard, out YukaiLarkAssistKind kind)
    {
        kind = GetRunnableAssistKind(context);
        return kind != YukaiLarkAssistKind.None
            && keyboard.IsKeyDown(Keys.Enter)
            && !previousKeyboard.IsKeyDown(Keys.Enter);
    }

    public bool ShouldSuppressFromKeyboard(YukaiLarkAssistantContext context, KeyboardState keyboard, KeyboardState previousKeyboard, out YukaiLarkAssistKind kind)
    {
        kind = GetRunnableAssistKind(context);
        return (kind == YukaiLarkAssistKind.CreateTransition && context.IsNormalToEndTransitionSuggestion
                || kind == YukaiLarkAssistKind.EditStateNodeLabel)
            && keyboard.IsKeyDown(Keys.Escape)
            && !previousKeyboard.IsKeyDown(Keys.Escape);
    }

    public bool ShouldRunFromMouse(YukaiLarkAssistantContext context, Point mousePosition, out YukaiLarkAssistKind kind)
    {
        kind = GetRunnableAssistKind(context);
        return kind != YukaiLarkAssistKind.None
            && (MascotBounds.Contains(mousePosition) || AssistAcceptButtonBounds.Contains(mousePosition));
    }

    public bool ShouldSuppressFromMouse(YukaiLarkAssistantContext context, Point mousePosition, out YukaiLarkAssistKind kind)
    {
        kind = GetRunnableAssistKind(context);
        return kind == YukaiLarkAssistKind.EditStateNodeLabel
            && AssistDeclineButtonBounds.Contains(mousePosition);
    }

    public Vector2 GetNodeScreenPosition(Viewport viewport, YukaiLarkAssistKind kind)
        => kind switch
        {
            YukaiLarkAssistKind.CreateStateNode => new Vector2(viewport.Width * 0.66f, viewport.Height * 0.45f),
            YukaiLarkAssistKind.CreateSecondStateNode => new Vector2(viewport.Width * 0.82f, viewport.Height * 0.45f),
            YukaiLarkAssistKind.CreateEndMarker => new Vector2(viewport.Width * 0.82f, viewport.Height * 0.64f),
            _ => new Vector2(viewport.Width * 0.42f, viewport.Height * 0.45f)
        };

    public bool ShouldDrawStartMarkerGhost(YukaiLarkAssistantContext context)
        => !IsCompletedAssistActive && IsAssistGhostReady && GetAssistKind(context) == YukaiLarkAssistKind.CreateStartMarker;

    public bool ShouldDrawStateNodeGhost(YukaiLarkAssistantContext context)
    {
        var kind = GetAssistKind(context);
        return !IsCompletedAssistActive
            && IsAssistGhostReady
            && (kind == YukaiLarkAssistKind.CreateStateNode || kind == YukaiLarkAssistKind.CreateSecondStateNode);
    }

    public bool ShouldDrawStateNodeLabelEditGhost(YukaiLarkAssistantContext context)
        => !IsCompletedAssistActive
            && IsAssistGhostReady
            && GetAssistKind(context) == YukaiLarkAssistKind.EditStateNodeLabel;

    public bool ShouldDrawTransitionGhost(YukaiLarkAssistantContext context)
    {
        var kind = GetAssistKind(context);
        return !IsCompletedAssistActive
            && IsAssistGhostReady
            && (kind == YukaiLarkAssistKind.CreateTransition || kind == YukaiLarkAssistKind.ConnectUnreachedStateNode);
    }

    public bool ShouldDrawTransitionEventGhost(YukaiLarkAssistantContext context)
        => !IsCompletedAssistActive && IsAssistGhostReady && GetAssistKind(context) == YukaiLarkAssistKind.AddTransitionEvent;

    public static float GetAssistBobOffset(TimeSpan totalGameTime)
        => MathF.Sin((float)totalGameTime.TotalSeconds * 8.5f) * 8f;

    public void NotifyAssistCompleted(YukaiLarkAssistKind kind)
    {
        if (kind == YukaiLarkAssistKind.CreateSecondStateNode)
        {
            _skipSecondStateNode = false;
        }

        _completedKind = kind;
        _completedAssistSeconds = CompletedAssistDisplaySeconds;
    }

    public void Reset()
    {
        _assistSeconds = 0;
        _activeKind = YukaiLarkAssistKind.None;
    }

    public void Draw(
        SpriteBatch spriteBatch,
        Texture2D mascotTexture,
        Texture2D pixel,
        Viewport viewport,
        TimeSpan totalGameTime,
        YukaiLarkAssistantContext context,
        BoardTheme theme,
        Rectangle avoidBounds,
        DrawRectangleOutline drawRectangleOutline,
        DrawUiText drawUiText)
    {
        MascotBounds = Rectangle.Empty;
        CutInBandBounds = Rectangle.Empty;
        AssistAcceptButtonBounds = Rectangle.Empty;
        AssistDeclineButtonBounds = Rectangle.Empty;
        if (viewport.Width < 640 || viewport.Height < 420)
        {
            return;
        }

        var assistKind = GetAssistKind(context);
        var source = new Rectangle(0, 0, mascotTexture.Width, (int)(mascotTexture.Height * 0.66f));
        var targetHeight = (int)MathF.Round(MascotTargetWidth * source.Height / (float)source.Width);
        var bob = assistKind != YukaiLarkAssistKind.None
            ? GetAssistBobOffset(totalGameTime)
            : 0f;
        var target = GetMascotTarget(viewport, targetHeight, bob, avoidBounds);
        MascotBounds = target;
        spriteBatch.Draw(mascotTexture, target, source, Color.White * 0.92f);

        if (_completedKind != YukaiLarkAssistKind.None && _completedAssistSeconds > 0)
        {
            DrawCompletedAssistBubble(spriteBatch, pixel, viewport, target, _completedKind, theme, drawRectangleOutline, drawUiText);
        }
        else if (IsAssistReady && assistKind != YukaiLarkAssistKind.None)
        {
            DrawAssistBubble(spriteBatch, pixel, viewport, target, assistKind, context, theme, drawRectangleOutline, drawUiText);
        }
    }

    private static Rectangle GetMascotTarget(Viewport viewport, int targetHeight, float bob, Rectangle avoidBounds)
    {
        const int margin = 22;
        const int headerHeight = 58;
        const int headerGap = 22;
        const int avoidGap = 24;
        var topMargin = headerHeight + headerGap;
        var bobOffset = (int)MathF.Round(bob);
        var target = new Rectangle(viewport.Width - MascotTargetWidth - margin, topMargin + bobOffset, MascotTargetWidth, targetHeight);
        if (!IntersectsWithGap(target, avoidBounds, avoidGap))
        {
            return target;
        }

        target.Y = avoidBounds.Y - targetHeight - avoidGap + bobOffset;
        if (target.Y >= topMargin && !IntersectsWithGap(target, avoidBounds, avoidGap))
        {
            return target;
        }

        target.X = avoidBounds.X - MascotTargetWidth - avoidGap;
        target.Y = topMargin + bobOffset;
        if (target.X >= margin)
        {
            return target;
        }

        target.X = margin;
        return target;
    }

    private static bool IntersectsWithGap(Rectangle target, Rectangle avoidBounds, int gap)
    {
        if (avoidBounds == Rectangle.Empty)
        {
            return false;
        }

        var expandedAvoidBounds = new Rectangle(
            avoidBounds.X - gap,
            avoidBounds.Y - gap,
            avoidBounds.Width + gap * 2,
            avoidBounds.Height + gap * 2);
        return target.Intersects(expandedAvoidBounds);
    }

    private YukaiLarkAssistKind GetRunnableAssistKind(YukaiLarkAssistantContext context)
    {
        var kind = GetAssistKind(context);
        return !IsCompletedAssistActive && IsAssistReady ? kind : YukaiLarkAssistKind.None;
    }

    private YukaiLarkAssistKind GetAssistKind(YukaiLarkAssistantContext context)
    {
        if (!context.IsInteractionIdle)
        {
            return YukaiLarkAssistKind.None;
        }

        if (!context.HasStartMarker)
        {
            return YukaiLarkAssistKind.CreateStartMarker;
        }

        if (context.NormalNodeCount == 0)
        {
            return YukaiLarkAssistKind.CreateStateNode;
        }

        if (context.HasDefaultStateNodeLabel)
        {
            return YukaiLarkAssistKind.EditStateNodeLabel;
        }

        if (!context.HasStartToNormalTransition)
        {
            return YukaiLarkAssistKind.CreateTransition;
        }

        if (context.NormalNodeCount == 1 && !_skipSecondStateNode)
        {
            return YukaiLarkAssistKind.CreateSecondStateNode;
        }

        if (context.NormalNodeCount >= 2 && !context.HasNormalToNormalTransition)
        {
            return YukaiLarkAssistKind.CreateTransition;
        }

        if (context.HasMissingTransitionEvent)
        {
            return YukaiLarkAssistKind.AddTransitionEvent;
        }

        if (context.ShouldSuggestShiftDiagramLeft)
        {
            return YukaiLarkAssistKind.ShiftDiagramLeft;
        }

        if (!context.HasEndMarker)
        {
            return YukaiLarkAssistKind.CreateEndMarker;
        }

        if (!context.HasNormalToEndTransition)
        {
            return YukaiLarkAssistKind.CreateTransition;
        }

        if (context.HasUnreachedNormalNode)
        {
            return YukaiLarkAssistKind.ConnectUnreachedStateNode;
        }

        return YukaiLarkAssistKind.None;
    }

    private static bool IsAssistantStatus(string status)
        => status.StartsWith("ユカイラーク:", StringComparison.Ordinal);

    private static string GetStatusText(YukaiLarkAssistantContext context, YukaiLarkAssistKind kind)
        => kind switch
        {
            YukaiLarkAssistKind.CreateStartMarker => "ユカイラーク: わたしの名前はユカイラークです。開始マークを作れます。",
            YukaiLarkAssistKind.CreateStateNode => "ユカイラーク: 次の状態ノードを作れます。Enterか鳥をクリック。",
            YukaiLarkAssistKind.EditStateNodeLabel => $"ユカイラーク: {context.DefaultStateNodeLabelSummary} のラベルを編集しますか？ Enterか鳥をクリック。",
            YukaiLarkAssistKind.CreateSecondStateNode => "ユカイラーク: 通常ノード同士の遷移例用に、2つ目の状態を作れます。",
            YukaiLarkAssistKind.CreateTransition => GetTransitionStatusText(context),
            YukaiLarkAssistKind.AddTransitionEvent => $"ユカイラーク: {context.MissingTransitionEventSummary} 間の遷移にイベントがありません。Enterか鳥をクリック。",
            YukaiLarkAssistKind.ShiftDiagramLeft => "ユカイラーク: 図が右に寄ってきたぜ。Enterか鳥クリックで左に寄せます。",
            YukaiLarkAssistKind.CreateEndMarker => "ユカイラーク: 終了マークがまだありません。Enterか鳥をクリック。",
            YukaiLarkAssistKind.ConnectUnreachedStateNode => "ユカイラーク: 入ってくる遷移がない通常ノードがあります。近くのノードからつなげますか？",
            _ => string.Empty
        };

    private static string GetTransitionStatusText(YukaiLarkAssistantContext context)
    {
        if (!context.HasStartToNormalTransition)
        {
            return "ユカイラーク: 開始から次の状態へ遷移を作れます。Enterか鳥をクリック。";
        }

        if (context.NormalNodeCount >= 2 && !context.HasNormalToNormalTransition)
        {
            return "ユカイラーク: 通常ノード同士の遷移をつなげます。Enterか鳥をクリック。";
        }

        return "ユカイラーク: 開始から一番遠い状態から終了マークへ遷移をつなげます。Enterか鳥クリックでつなぐ、Escでつながない。";
    }

    private void DrawAssistBubble(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Viewport viewport,
        Rectangle mascotBounds,
        YukaiLarkAssistKind kind,
        YukaiLarkAssistantContext context,
        BoardTheme theme,
        DrawRectangleOutline drawRectangleOutline,
        DrawUiText drawUiText)
    {
        const int bubbleWidth = 376;
        const int bubbleHeight = 50;
        var bubble = new Rectangle(mascotBounds.X - bubbleWidth + 18, mascotBounds.Y + 22, bubbleWidth, bubbleHeight);
        var (title, body) = GetBubbleText(kind, context);
        spriteBatch.Draw(pixel, bubble, theme.AssistantBubbleColor);
        drawRectangleOutline(bubble, theme.AssistantBubbleBorderColor, 2);
        drawUiText(title, new Vector2(bubble.X + 12, bubble.Y + 13), theme.AssistantTitleTextColor, 17, true);
        DrawAssistantCutIn(spriteBatch, pixel, viewport, mascotBounds, body, string.Empty, kind, theme, drawRectangleOutline, drawUiText);
    }

    private static (string Title, string Body) GetBubbleText(YukaiLarkAssistKind kind, YukaiLarkAssistantContext context)
        => kind switch
        {
            YukaiLarkAssistKind.CreateStartMarker => ("わたしの名前はユカイラークです", "開始マークを作る？ Enter または鳥をクリック"),
            YukaiLarkAssistKind.CreateStateNode => ("次の状態を作る？", "Enter または鳥をクリック"),
            YukaiLarkAssistKind.EditStateNodeLabel => ("ノードのラベルを編集する？", $"{context.DefaultStateNodeLabelSummary} / Enter: ラベルを入力する / Esc: しない"),
            YukaiLarkAssistKind.CreateSecondStateNode => ("2つ目の状態を作る？", "作らないときは数秒待つと次へ進みます"),
            YukaiLarkAssistKind.CreateTransition => GetTransitionBubbleText(context),
            YukaiLarkAssistKind.AddTransitionEvent => ("イベントを追加する？", $"{context.MissingTransitionEventSummary} 間の遷移"),
            YukaiLarkAssistKind.ShiftDiagramLeft => ("図を左に寄せる？", "作業スペースを広げるぜ"),
            YukaiLarkAssistKind.CreateEndMarker => ("終了マークを作る？", "Enter または鳥をクリック"),
            YukaiLarkAssistKind.ConnectUnreachedStateNode => ("近くのノードからつなげる？", "Enter または鳥をクリック"),
            _ => (string.Empty, string.Empty)
        };

    private static (string Title, string Body) GetTransitionBubbleText(YukaiLarkAssistantContext context)
    {
        if (!context.HasStartToNormalTransition)
        {
            return ("遷移をつなぐ？", "Enter または鳥をクリック");
        }

        if (context.NormalNodeCount >= 2 && !context.HasNormalToNormalTransition)
        {
            return ("通常ノード同士をつなぐ？", "Enter または鳥をクリック");
        }

        return ("終了マークへつなぐ？", "Enterでつなぐ / Escでつながない");
    }

    private void DrawCompletedAssistBubble(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Viewport viewport,
        Rectangle mascotBounds,
        YukaiLarkAssistKind kind,
        BoardTheme theme,
        DrawRectangleOutline drawRectangleOutline,
        DrawUiText drawUiText)
    {
        const int bubbleWidth = 430;
        const int bubbleHeight = 50;
        var bubble = new Rectangle(mascotBounds.X - bubbleWidth + 18, mascotBounds.Y + 18, bubbleWidth, bubbleHeight);
        var (title, action, hint) = GetCompletedBubbleText(kind);
        spriteBatch.Draw(pixel, bubble, theme.AssistantBubbleColor);
        drawRectangleOutline(bubble, theme.AssistantCompletedBubbleBorderColor, 2);
        drawUiText(title, new Vector2(bubble.X + 12, bubble.Y + 13), theme.AssistantTitleTextColor, 17, true);
        DrawAssistantCutIn(spriteBatch, pixel, viewport, mascotBounds, action, hint, YukaiLarkAssistKind.None, theme, drawRectangleOutline, drawUiText);
    }

    private void DrawAssistantCutIn(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Viewport viewport,
        Rectangle mascotBounds,
        string primaryText,
        string secondaryText,
        YukaiLarkAssistKind kind,
        BoardTheme theme,
        DrawRectangleOutline drawRectangleOutline,
        DrawUiText drawUiText)
    {
        if (string.IsNullOrWhiteSpace(primaryText) && string.IsNullOrWhiteSpace(secondaryText))
        {
            return;
        }

        var hasSecondaryText = !string.IsNullOrWhiteSpace(secondaryText);
        var hasActionButtons = kind == YukaiLarkAssistKind.EditStateNodeLabel;
        var bandHeight = hasActionButtons ? 86 : hasSecondaryText ? 76 : 56;
        var preferredY = Math.Max(mascotBounds.Bottom + 16, (int)(viewport.Height * 0.72f));
        var y = Math.Clamp(preferredY, 86, viewport.Height - bandHeight - 72);
        var band = new Rectangle(0, y, viewport.Width, bandHeight);
        CutInBandBounds = band;
        var frameWidth = Math.Min(760, Math.Max(420, viewport.Width - 112));
        var frame = new Rectangle((viewport.Width - frameWidth) / 2, y + 7, frameWidth, bandHeight - 14);
        var accent = hasSecondaryText
            ? theme.AssistantCompletedBubbleBorderColor
            : theme.AssistantBubbleBorderColor;

        spriteBatch.Draw(pixel, new Rectangle(0, y + 5, viewport.Width, bandHeight), theme.AssistantCutInShadowColor);
        spriteBatch.Draw(pixel, band, theme.AssistantCutInBandColor);
        spriteBatch.Draw(pixel, new Rectangle(0, band.Y, viewport.Width, 1), accent);
        spriteBatch.Draw(pixel, new Rectangle(0, band.Bottom - 1, viewport.Width, 1), accent);
        spriteBatch.Draw(pixel, frame, theme.AssistantCutInFrameColor);
        drawRectangleOutline(frame, accent, 1);

        drawUiText(primaryText, new Vector2(frame.X + 18, frame.Y + 8), theme.AssistantCutInPrimaryTextColor, 15, true);
        if (hasSecondaryText)
        {
            drawUiText(secondaryText, new Vector2(frame.X + 18, frame.Y + 34), theme.AssistantCutInSecondaryTextColor, 14, false);
        }
        if (hasActionButtons)
        {
            DrawAssistantActionButtons(spriteBatch, pixel, frame, theme, drawRectangleOutline, drawUiText);
        }
    }

    private void DrawAssistantActionButtons(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle frame,
        BoardTheme theme,
        DrawRectangleOutline drawRectangleOutline,
        DrawUiText drawUiText)
    {
        const int gap = 10;
        const int acceptWidth = 184;
        const int declineWidth = 112;
        const int height = 26;
        var y = frame.Y + 38;
        var x = frame.X + 18;
        AssistAcceptButtonBounds = new Rectangle(x, y, acceptWidth, height);
        AssistDeclineButtonBounds = new Rectangle(x + acceptWidth + gap, y, declineWidth, height);

        DrawAssistantActionButton(spriteBatch, pixel, AssistAcceptButtonBounds, "Enter  ラベルを入力", true, theme, drawRectangleOutline, drawUiText);
        DrawAssistantActionButton(spriteBatch, pixel, AssistDeclineButtonBounds, "Esc  しない", false, theme, drawRectangleOutline, drawUiText);
    }

    private static void DrawAssistantActionButton(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle bounds,
        string label,
        bool primary,
        BoardTheme theme,
        DrawRectangleOutline drawRectangleOutline,
        DrawUiText drawUiText)
    {
        var fill = primary
            ? theme.AssistantBubbleBorderColor * 0.56f
            : theme.AssistantCutInFrameColor * 0.82f;
        var border = primary
            ? theme.AssistantCompletedBubbleBorderColor
            : theme.AssistantBubbleBorderColor * 0.72f;
        var text = primary
            ? theme.AssistantCutInPrimaryTextColor
            : theme.AssistantCutInSecondaryTextColor;

        spriteBatch.Draw(pixel, bounds, fill);
        drawRectangleOutline(bounds, border, 1);
        drawUiText(label, new Vector2(bounds.X + 10, bounds.Y + 4), text, 13, primary);
    }
    private static (string Title, string Action, string Hint) GetCompletedBubbleText(YukaiLarkAssistKind kind)
        => kind switch
        {
            YukaiLarkAssistKind.CreateStartMarker => ("ユカイラークが作図しました", "開始マークを追加し、開始マークにして選択しました。", "手動なら Sで開始マークを追加できます。"),
            YukaiLarkAssistKind.DeleteStartMarker => ("ユカイラークが気づきました", "開始マークを削除したんですね？", "必要なら Ctrl+Z で元に戻せます。"),
            YukaiLarkAssistKind.CreateStateNode => ("ユカイラークが作図しました", "次の状態ノードを追加し、選択しました。", "手動なら Nで状態追加、ドラッグで位置調整です。"),
            YukaiLarkAssistKind.EditStateNodeLabel => ("ユカイラークが見つけました", "状態ノードのラベル入力を開始しました。", "状態名を入力して Enterで確定します。"),
            YukaiLarkAssistKind.CreateSecondStateNode => ("ユカイラークが作図しました", "2つ目の状態ノードを追加し、選択しました。", "次は通常ノード同士の遷移をつなげます。"),
            YukaiLarkAssistKind.CreateTransition => ("ユカイラークが作図しました", "遷移を作成しました。", "通常ノード同士の遷移ならイベントを付けられます。"),
            YukaiLarkAssistKind.AddTransitionEvent => ("ユカイラークが見つけました", "イベント未設定の遷移を選択しました。", "イベント名を入力して Enterで確定します。"),
            YukaiLarkAssistKind.ShiftDiagramLeft => ("ユカイラークが整えました", "図を左に寄せて、右側の作業スペースを広げました。", "Ctrl+Zで元に戻せます。"),
            YukaiLarkAssistKind.CreateEndMarker => ("ユカイラークが作図しました", "終了マークを追加し、終了マークにして選択しました。", "手動なら Eで終了マークを追加できます。"),
            YukaiLarkAssistKind.ConnectUnreachedStateNode => ("ユカイラークが作図しました", "入ってくる遷移がなかった通常ノードへ遷移を作成しました。", "必要ならイベント名を追加できます。"),
            _ => (string.Empty, string.Empty, string.Empty)
        };
}

internal readonly record struct YukaiLarkAssistantContext(
    bool HasStartMarker,
    bool HasEndMarker,
    int NormalNodeCount,
    bool HasDefaultStateNodeLabel,
    string DefaultStateNodeLabelSummary,
    bool HasStartToNormalTransition,
    bool HasNormalToNormalTransition,
    bool HasNormalToEndTransition,
    bool IsNormalToEndTransitionSuggestion,
    bool HasMissingTransitionEvent,
    string MissingTransitionEventSummary,
    bool ShouldSuggestShiftDiagramLeft,
    float ShiftDiagramLeftDistance,
    bool HasUnreachedNormalNode,
    bool IsInteractionIdle);

internal enum YukaiLarkAssistKind
{
    None,
    CreateStartMarker,
    DeleteStartMarker,
    CreateStateNode,
    EditStateNodeLabel,
    CreateSecondStateNode,
    CreateTransition,
    AddTransitionEvent,
    ShiftDiagramLeft,
    CreateEndMarker,
    ConnectUnreachedStateNode
}

internal delegate void DrawRectangleOutline(Rectangle rectangle, Color color, int thickness);

internal delegate float DrawUiText(string text, Vector2 position, Color color, float size, bool bold);
