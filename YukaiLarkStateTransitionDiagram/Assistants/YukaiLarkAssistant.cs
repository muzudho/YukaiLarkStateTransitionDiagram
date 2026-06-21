namespace YukaiLarkStateTransitionDiagram.Assistants;

using System;
using YukaiLarkStateTransitionDiagram;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

internal sealed class YukaiLarkAssistant
{
    private const double AssistWakeSeconds = 1.2;
    private const double CompletedAssistDisplaySeconds = 5.0;
    private const int MascotTargetWidth = 176;

    private double _assistSeconds;
    private double _completedAssistSeconds;
    private YukaiLarkAssistKind _activeKind;
    private YukaiLarkAssistKind _completedKind;

    public Rectangle MascotBounds { get; private set; }

    private bool IsAssistReady => _assistSeconds >= AssistWakeSeconds;

    public string Update(GameTime gameTime, YukaiLarkAssistantContext context, string currentStatus, string defaultStatus)
    {
        if (_completedAssistSeconds > 0)
        {
            _completedAssistSeconds = Math.Max(0, _completedAssistSeconds - gameTime.ElapsedGameTime.TotalSeconds);
            if (_completedAssistSeconds <= 0)
            {
                _completedKind = YukaiLarkAssistKind.None;
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

    public bool ShouldRunFromMouse(YukaiLarkAssistantContext context, Point mousePosition, out YukaiLarkAssistKind kind)
    {
        kind = GetRunnableAssistKind(context);
        return kind != YukaiLarkAssistKind.None && MascotBounds.Contains(mousePosition);
    }

    public Vector2 GetNodeScreenPosition(Viewport viewport, YukaiLarkAssistKind kind)
        => kind switch
        {
            YukaiLarkAssistKind.CreateStateNode => new Vector2(viewport.Width * 0.58f, viewport.Height * 0.45f),
            YukaiLarkAssistKind.CreateEndNode => new Vector2(viewport.Width * 0.74f, viewport.Height * 0.45f),
            _ => new Vector2(viewport.Width * 0.42f, viewport.Height * 0.45f)
        };

    public bool ShouldDrawStartNodeGhost(YukaiLarkAssistantContext context)
        => IsAssistReady && GetAssistKind(context) == YukaiLarkAssistKind.CreateStartNode;

    public static float GetAssistBobOffset(TimeSpan totalGameTime)
        => MathF.Sin((float)totalGameTime.TotalSeconds * 8.5f) * 8f;

    public void NotifyAssistCompleted(YukaiLarkAssistKind kind)
    {
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
        DrawRectangleOutline drawRectangleOutline,
        DrawUiText drawUiText)
    {
        MascotBounds = Rectangle.Empty;
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
        var target = new Rectangle(viewport.Width - MascotTargetWidth - 22, 178 + (int)MathF.Round(bob), MascotTargetWidth, targetHeight);
        MascotBounds = target;
        spriteBatch.Draw(mascotTexture, target, source, Color.White * 0.92f);

        if (_completedKind != YukaiLarkAssistKind.None && _completedAssistSeconds > 0)
        {
            DrawCompletedAssistBubble(spriteBatch, pixel, target, _completedKind, theme, drawRectangleOutline, drawUiText);
        }
        else if (IsAssistReady && assistKind != YukaiLarkAssistKind.None)
        {
            DrawAssistBubble(spriteBatch, pixel, target, assistKind, context, theme, drawRectangleOutline, drawUiText);
        }
    }

    private YukaiLarkAssistKind GetRunnableAssistKind(YukaiLarkAssistantContext context)
    {
        var kind = GetAssistKind(context);
        return IsAssistReady ? kind : YukaiLarkAssistKind.None;
    }

    private static YukaiLarkAssistKind GetAssistKind(YukaiLarkAssistantContext context)
    {
        if (!context.IsInteractionIdle)
        {
            return YukaiLarkAssistKind.None;
        }

        if (!context.HasStartNode)
        {
            return YukaiLarkAssistKind.CreateStartNode;
        }

        if (context.NodeCount == 1)
        {
            return YukaiLarkAssistKind.CreateStateNode;
        }

        if (context.NodeCount >= 2 && context.TransitionCount == 0)
        {
            return YukaiLarkAssistKind.CreateTransition;
        }

        if (context.HasMissingTransitionEvent)
        {
            return YukaiLarkAssistKind.AddTransitionEvent;
        }

        if (!context.HasEndNode)
        {
            return YukaiLarkAssistKind.CreateEndNode;
        }

        return YukaiLarkAssistKind.None;
    }

    private static bool IsAssistantStatus(string status)
        => status.StartsWith("ユカイラーク:", StringComparison.Ordinal);

    private static string GetStatusText(YukaiLarkAssistantContext context, YukaiLarkAssistKind kind)
        => kind switch
        {
            YukaiLarkAssistKind.CreateStartNode => "ユカイラーク: まず開始ノードを作れます。Enterか鳥をクリック。",
            YukaiLarkAssistKind.CreateStateNode => "ユカイラーク: 次の状態ノードを作れます。Enterか鳥をクリック。",
            YukaiLarkAssistKind.CreateTransition => "ユカイラーク: 開始から次の状態へ遷移を作れます。Enterか鳥をクリック。",
            YukaiLarkAssistKind.AddTransitionEvent => $"ユカイラーク: {context.MissingTransitionEventSummary} 間の遷移にイベントがありません。Enterか鳥をクリック。",
            YukaiLarkAssistKind.CreateEndNode => "ユカイラーク: 終了ノードがまだありません。Enterか鳥をクリック。",
            _ => string.Empty
        };

    private static void DrawAssistBubble(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle mascotBounds,
        YukaiLarkAssistKind kind,
        YukaiLarkAssistantContext context,
        BoardTheme theme,
        DrawRectangleOutline drawRectangleOutline,
        DrawUiText drawUiText)
    {
        const int bubbleWidth = 338;
        const int bubbleHeight = 72;
        var bubble = new Rectangle(mascotBounds.X - bubbleWidth + 18, mascotBounds.Y + 22, bubbleWidth, bubbleHeight);
        var (title, body) = GetBubbleText(kind, context);
        spriteBatch.Draw(pixel, bubble, theme.AssistantBubbleColor);
        drawRectangleOutline(bubble, theme.AssistantBubbleBorderColor, 2);
        drawUiText(title, new Vector2(bubble.X + 12, bubble.Y + 10), theme.AssistantTitleTextColor, 17, true);
        drawUiText(body, new Vector2(bubble.X + 12, bubble.Y + 38), theme.AssistantBodyTextColor, 15, false);
    }

    private static (string Title, string Body) GetBubbleText(YukaiLarkAssistKind kind, YukaiLarkAssistantContext context)
        => kind switch
        {
            YukaiLarkAssistKind.CreateStartNode => ("開始ノードを作る？", "Enter または鳥をクリック"),
            YukaiLarkAssistKind.CreateStateNode => ("次の状態を作る？", "Enter または鳥をクリック"),
            YukaiLarkAssistKind.CreateTransition => ("遷移をつなぐ？", "Enter または鳥をクリック"),
            YukaiLarkAssistKind.AddTransitionEvent => ("イベントを追加する？", $"{context.MissingTransitionEventSummary} 間の遷移"),
            YukaiLarkAssistKind.CreateEndNode => ("終了ノードを作る？", "Enter または鳥をクリック"),
            _ => (string.Empty, string.Empty)
        };

    private static void DrawCompletedAssistBubble(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle mascotBounds,
        YukaiLarkAssistKind kind,
        BoardTheme theme,
        DrawRectangleOutline drawRectangleOutline,
        DrawUiText drawUiText)
    {
        const int bubbleWidth = 410;
        const int bubbleHeight = 104;
        var bubble = new Rectangle(mascotBounds.X - bubbleWidth + 18, mascotBounds.Y + 18, bubbleWidth, bubbleHeight);
        var (title, action, hint) = GetCompletedBubbleText(kind);
        spriteBatch.Draw(pixel, bubble, theme.AssistantBubbleColor);
        drawRectangleOutline(bubble, theme.AssistantCompletedBubbleBorderColor, 2);
        drawUiText(title, new Vector2(bubble.X + 12, bubble.Y + 10), theme.AssistantTitleTextColor, 17, true);
        drawUiText(action, new Vector2(bubble.X + 12, bubble.Y + 38), theme.AssistantBodyTextColor, 15, false);
        drawUiText(hint, new Vector2(bubble.X + 12, bubble.Y + 66), theme.AssistantHintTextColor, 14, false);
    }

    private static (string Title, string Action, string Hint) GetCompletedBubbleText(YukaiLarkAssistKind kind)
        => kind switch
        {
            YukaiLarkAssistKind.CreateStartNode => ("ユカイラークが作図しました", "開始ノードを追加し、開始種別にして選択しました。", "手動なら Nで状態追加、Tで種別変更です。"),
            YukaiLarkAssistKind.CreateStateNode => ("ユカイラークが作図しました", "次の状態ノードを追加し、選択しました。", "手動なら Nで状態追加、ドラッグで位置調整です。"),
            YukaiLarkAssistKind.CreateTransition => ("ユカイラークが作図しました", "開始ノードから次の状態へ遷移を作成しました。", "手動なら Shift+ドラッグで状態同士を接続します。"),
            YukaiLarkAssistKind.AddTransitionEvent => ("ユカイラークが見つけました", "イベント未設定の遷移を選択しました。", "イベント名を入力して Enterで確定します。"),
            YukaiLarkAssistKind.CreateEndNode => ("ユカイラークが作図しました", "終了ノードを追加し、終了種別にして選択しました。", "手動なら Nで状態追加、Tで種別変更です。"),
            _ => (string.Empty, string.Empty, string.Empty)
        };
}

internal readonly record struct YukaiLarkAssistantContext(bool HasStartNode, bool HasEndNode, int NodeCount, int TransitionCount, bool HasMissingTransitionEvent, string MissingTransitionEventSummary, bool IsInteractionIdle);

internal enum YukaiLarkAssistKind
{
    None,
    CreateStartNode,
    CreateStateNode,
    CreateTransition,
    AddTransitionEvent,
    CreateEndNode
}

internal delegate void DrawRectangleOutline(Rectangle rectangle, Color color, int thickness);

internal delegate float DrawUiText(string text, Vector2 position, Color color, float size, bool bold);
