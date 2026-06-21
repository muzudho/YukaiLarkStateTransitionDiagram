namespace YukaiLarkStateTransitionDiagram.Assistants;

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

internal sealed class YukaiLarkAssistant
{
    /// <summary>
    /// ［開始ノード作成アシスト］が起動するまでの秒数
    /// </summary>
    private const double StartNodeAssistWakeSeconds = 1.2;

    private const int MascotTargetWidth = 176;

    private double _startNodeAssistSeconds;

    public Rectangle MascotBounds { get; private set; }

    private bool IsStartNodeAssistReady => _startNodeAssistSeconds >= StartNodeAssistWakeSeconds;

    public string Update(GameTime gameTime, YukaiLarkAssistantContext context, string currentStatus, string defaultStatus)
    {
        if (!ShouldOfferStartNodeAssist(context))
        {
            _startNodeAssistSeconds = 0;
            return currentStatus;
        }

        _startNodeAssistSeconds += gameTime.ElapsedGameTime.TotalSeconds;
        return IsStartNodeAssistReady && currentStatus == defaultStatus
            ? "ユカイラーク: まず開始ノードを作れます。Enterか鳥をクリック。"
            : currentStatus;
    }

    public bool ShouldCreateStartNodeFromKeyboard(YukaiLarkAssistantContext context, KeyboardState keyboard, KeyboardState previousKeyboard)
        => IsStartNodeAssistReady
            && ShouldOfferStartNodeAssist(context)
            && keyboard.IsKeyDown(Keys.Enter)
            && !previousKeyboard.IsKeyDown(Keys.Enter);

    public bool ShouldCreateStartNodeFromMouse(YukaiLarkAssistantContext context, Point mousePosition)
        => IsStartNodeAssistReady
            && ShouldOfferStartNodeAssist(context)
            && MascotBounds.Contains(mousePosition);

    public Vector2 GetStartNodeScreenPosition(Viewport viewport)
        => new(viewport.Width * 0.42f, viewport.Height * 0.45f);

    public void Reset()
    {
        _startNodeAssistSeconds = 0;
    }

    public void Draw(
        SpriteBatch spriteBatch,
        Texture2D mascotTexture,
        Texture2D pixel,
        Viewport viewport,
        TimeSpan totalGameTime,
        YukaiLarkAssistantContext context,
        DrawRectangleOutline drawRectangleOutline,
        DrawUiText drawUiText)
    {
        MascotBounds = Rectangle.Empty;
        if (viewport.Width < 640 || viewport.Height < 420)
        {
            return;
        }

        var source = new Rectangle(0, 0, mascotTexture.Width, (int)(mascotTexture.Height * 0.66f));
        var targetHeight = (int)MathF.Round(MascotTargetWidth * source.Height / (float)source.Width);
        var bob = ShouldOfferStartNodeAssist(context)
            ? MathF.Sin((float)totalGameTime.TotalSeconds * 8.5f) * 8f
            : 0f;
        var target = new Rectangle(viewport.Width - MascotTargetWidth - 22, 178 + (int)MathF.Round(bob), MascotTargetWidth, targetHeight);
        MascotBounds = target;
        spriteBatch.Draw(mascotTexture, target, source, Color.White * 0.92f);

        if (IsStartNodeAssistReady)
        {
            DrawStartNodeAssistBubble(spriteBatch, pixel, target, drawRectangleOutline, drawUiText);
        }
    }

    private static bool ShouldOfferStartNodeAssist(YukaiLarkAssistantContext context)
        => !context.HasStartNode && context.IsInteractionIdle;

    private static void DrawStartNodeAssistBubble(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle mascotBounds,
        DrawRectangleOutline drawRectangleOutline,
        DrawUiText drawUiText)
    {
        const int bubbleWidth = 318;
        const int bubbleHeight = 72;
        var bubble = new Rectangle(mascotBounds.X - bubbleWidth + 18, mascotBounds.Y + 22, bubbleWidth, bubbleHeight);
        spriteBatch.Draw(pixel, bubble, new Color(255, 253, 239, 235));
        drawRectangleOutline(bubble, new Color(83, 178, 176, 210), 2);
        drawUiText("開始ノードを作る？", new Vector2(bubble.X + 12, bubble.Y + 10), new Color(58, 45, 34), 17, true);
        drawUiText("Enter または鳥をクリック", new Vector2(bubble.X + 12, bubble.Y + 38), new Color(74, 86, 92), 15, false);
    }
}

internal readonly record struct YukaiLarkAssistantContext(bool HasStartNode, bool IsInteractionIdle);

internal delegate void DrawRectangleOutline(Rectangle rectangle, Color color, int thickness);

internal delegate float DrawUiText(string text, Vector2 position, Color color, float size, bool bold);

