namespace YukaiLarkStateTransitionDiagram.Theme;

using Microsoft.Xna.Framework;

public sealed class StrawberryKeyCapTheme : KeyCapThemeBase
{
    public override string Name => "Strawberry";
    public override Color FaceColor => new(222, 68, 89);
    public override Color TopEdgeColor => new(255, 152, 166);
    public override Color BottomEdgeColor => new(121, 33, 60);
    public override Color InnerHighlightColor => new(238, 88, 108);
    public override Color LabelTextColor => new(255, 244, 232);
    public override Color DescriptionTextColor => new(157, 82, 92);
    public override Color SeparatorTextColor => new(111, 165, 84);
}
