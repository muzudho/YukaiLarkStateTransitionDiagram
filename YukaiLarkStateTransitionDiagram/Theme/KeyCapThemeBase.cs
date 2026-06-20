namespace SkylarkStateTransitionDiagram.Theme;

using Microsoft.Xna.Framework;

public abstract class KeyCapThemeBase : IKeyCapTheme
{
    public abstract string Name { get; }
    public abstract Color FaceColor { get; }
    public abstract Color TopEdgeColor { get; }
    public abstract Color BottomEdgeColor { get; }
    public abstract Color InnerHighlightColor { get; }
    public abstract Color LabelTextColor { get; }
    public virtual Color DescriptionTextColor => new(188, 201, 218);
    public virtual Color SeparatorTextColor => new(112, 126, 145);
    public virtual int Height => 22;
    public virtual int MinWidth => 34;
    public virtual int HorizontalPadding => 8;
    public virtual float FontSize => 13f;
}