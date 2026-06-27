namespace YukaiLarkStateTransitionDiagram.Theme;

using Microsoft.Xna.Framework;

public sealed class MarineKeyCapTheme : KeyCapThemeBase
{
    public override string Name => "Marine";
    public override Color FaceColor => new(36, 143, 174);
    public override Color TopEdgeColor => new(139, 221, 232);
    public override Color BottomEdgeColor => new(12, 76, 108);
    public override Color InnerHighlightColor => new(58, 166, 194);
    public override Color LabelTextColor => new(232, 250, 252);
    public override Color DescriptionTextColor => new(70, 126, 150);
    public override Color SeparatorTextColor => new(91, 184, 184);
}
