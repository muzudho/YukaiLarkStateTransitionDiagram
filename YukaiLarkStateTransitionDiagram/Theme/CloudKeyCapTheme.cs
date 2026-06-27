namespace YukaiLarkStateTransitionDiagram.Theme;

using Microsoft.Xna.Framework;

public sealed class CloudKeyCapTheme : KeyCapThemeBase
{
    public override string Name => "Cloud";
    public override Color FaceColor => new(225, 233, 239);
    public override Color TopEdgeColor => new(255, 255, 255);
    public override Color BottomEdgeColor => new(141, 158, 174);
    public override Color InnerHighlightColor => new(242, 247, 250);
    public override Color LabelTextColor => new(54, 76, 96);
    public override Color DescriptionTextColor => new(91, 112, 128);
    public override Color SeparatorTextColor => new(158, 176, 188);
}
