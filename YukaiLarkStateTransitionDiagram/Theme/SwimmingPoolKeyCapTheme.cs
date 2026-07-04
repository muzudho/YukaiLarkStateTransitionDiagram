namespace YukaiLarkStateTransitionDiagram.Theme;

using Microsoft.Xna.Framework;

public sealed class SwimmingPoolKeyCapTheme : KeyCapThemeBase
{
    public override string Name => "SwimmingPool";
    public override Color FaceColor => new(54, 190, 222);
    public override Color TopEdgeColor => new(178, 248, 255);
    public override Color BottomEdgeColor => new(22, 106, 164);
    public override Color InnerHighlightColor => new(86, 214, 238);
    public override Color LabelTextColor => new(18, 66, 106);
    public override Color DescriptionTextColor => new(46, 120, 152);
    public override Color SeparatorTextColor => new(238, 242, 232);
}
