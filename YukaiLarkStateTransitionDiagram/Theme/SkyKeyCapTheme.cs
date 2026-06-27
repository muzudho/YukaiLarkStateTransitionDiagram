namespace YukaiLarkStateTransitionDiagram.Theme;

using Microsoft.Xna.Framework;

public sealed class SkyKeyCapTheme : KeyCapThemeBase
{
    public override string Name => "Sky";
    public override Color FaceColor => new(103, 184, 232);
    public override Color TopEdgeColor => new(208, 242, 255);
    public override Color BottomEdgeColor => new(42, 111, 164);
    public override Color InnerHighlightColor => new(130, 203, 240);
    public override Color LabelTextColor => new(30, 70, 105);
    public override Color DescriptionTextColor => new(72, 123, 154);
    public override Color SeparatorTextColor => new(156, 183, 202);
}
