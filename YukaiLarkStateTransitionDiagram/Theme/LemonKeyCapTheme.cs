namespace YukaiLarkStateTransitionDiagram.Theme;

using Microsoft.Xna.Framework;

public sealed class LemonKeyCapTheme : KeyCapThemeBase
{
    public override string Name => "Lemon";
    public override Color FaceColor => new(250, 222, 84);
    public override Color TopEdgeColor => new(255, 248, 174);
    public override Color BottomEdgeColor => new(152, 129, 31);
    public override Color InnerHighlightColor => new(255, 232, 110);
    public override Color LabelTextColor => new(63, 65, 34);
    public override Color DescriptionTextColor => new(112, 113, 58);
    public override Color SeparatorTextColor => new(192, 148, 48);
}
