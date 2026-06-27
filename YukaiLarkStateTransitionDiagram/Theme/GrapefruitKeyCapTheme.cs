namespace YukaiLarkStateTransitionDiagram.Theme;

using Microsoft.Xna.Framework;

public sealed class GrapefruitKeyCapTheme : KeyCapThemeBase
{
    public override string Name => "Grapefruit";
    public override Color FaceColor => new(246, 118, 91);
    public override Color TopEdgeColor => new(255, 199, 172);
    public override Color BottomEdgeColor => new(154, 55, 62);
    public override Color InnerHighlightColor => new(255, 145, 112);
    public override Color LabelTextColor => new(82, 38, 45);
    public override Color DescriptionTextColor => new(126, 72, 76);
    public override Color SeparatorTextColor => new(222, 160, 82);
}
