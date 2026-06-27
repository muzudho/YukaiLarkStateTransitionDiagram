namespace YukaiLarkStateTransitionDiagram.Theme;

using Microsoft.Xna.Framework;

public sealed class MoonKeyCapTheme : KeyCapThemeBase
{
    public override string Name => "Moon";
    public override Color FaceColor => new(98, 104, 137);
    public override Color TopEdgeColor => new(177, 181, 206);
    public override Color BottomEdgeColor => new(42, 48, 78);
    public override Color InnerHighlightColor => new(119, 124, 155);
    public override Color LabelTextColor => new(250, 244, 216);
    public override Color DescriptionTextColor => new(116, 124, 154);
    public override Color SeparatorTextColor => new(217, 198, 129);
}
