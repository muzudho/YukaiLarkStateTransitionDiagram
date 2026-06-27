namespace YukaiLarkStateTransitionDiagram.Theme;

using Microsoft.Xna.Framework;

public sealed class SunKeyCapTheme : KeyCapThemeBase
{
    public override string Name => "Sun";
    public override Color FaceColor => new(255, 194, 68);
    public override Color TopEdgeColor => new(255, 238, 146);
    public override Color BottomEdgeColor => new(176, 91, 28);
    public override Color InnerHighlightColor => new(255, 211, 88);
    public override Color LabelTextColor => new(73, 55, 32);
    public override Color DescriptionTextColor => new(124, 91, 53);
    public override Color SeparatorTextColor => new(228, 126, 52);
}
