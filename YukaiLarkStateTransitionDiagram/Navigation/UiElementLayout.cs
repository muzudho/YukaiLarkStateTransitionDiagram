namespace YukaiLarkStateTransitionDiagram.Navigation;

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public readonly record struct UiSize(int Width, int Height);

public readonly record struct UiMargin(int Left, int Top, int Right, int Bottom)
{
    public static UiMargin FromRightBottom(int right, int bottom)
        => new(0, 0, right, bottom);
}

public readonly record struct UiElementLayout(UiSize Size, UiMargin Margin)
{
    public Rectangle GetBottomRightBounds(Viewport viewport, int minimumTop = 0)
    {
        var x = viewport.Width - Size.Width - Margin.Right;
        var y = viewport.Height - Size.Height - Margin.Bottom;
        return new Rectangle(x, Math.Max(minimumTop, y), Size.Width, Size.Height);
    }
}
