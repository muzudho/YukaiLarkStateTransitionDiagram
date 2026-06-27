namespace YukaiLarkStateTransitionDiagram.Theme;

using Microsoft.Xna.Framework;

public sealed class OrangeKeyCapTheme : KeyCapThemeBase
{
    public override string Name => "Orange";
    public override Color FaceColor => new(246, 154, 58);
    public override Color TopEdgeColor => new(255, 214, 134);
    public override Color BottomEdgeColor => new(159, 76, 28);
    public override Color InnerHighlightColor => new(255, 176, 76);
    public override Color LabelTextColor => new(77, 48, 30);
    public override Color DescriptionTextColor => new(128, 85, 52);
    public override Color SeparatorTextColor => new(207, 118, 48);
}
