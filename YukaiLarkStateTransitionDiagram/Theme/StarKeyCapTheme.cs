namespace YukaiLarkStateTransitionDiagram.Theme;

using Microsoft.Xna.Framework;

public sealed class StarKeyCapTheme : KeyCapThemeBase
{
    public override string Name => "Star";
    public override Color FaceColor => new(48, 62, 129);
    public override Color TopEdgeColor => new(118, 134, 214);
    public override Color BottomEdgeColor => new(15, 22, 70);
    public override Color InnerHighlightColor => new(68, 82, 158);
    public override Color LabelTextColor => new(255, 239, 170);
    public override Color DescriptionTextColor => new(105, 122, 184);
    public override Color SeparatorTextColor => new(245, 205, 92);
}
