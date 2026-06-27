namespace YukaiLarkStateTransitionDiagram.Theme;

using Microsoft.Xna.Framework;

public sealed class PeachKeyCapTheme : KeyCapThemeBase
{
    public override string Name => "Peach";
    public override Color FaceColor => new(255, 183, 149);
    public override Color TopEdgeColor => new(255, 231, 205);
    public override Color BottomEdgeColor => new(185, 100, 94);
    public override Color InnerHighlightColor => new(255, 199, 168);
    public override Color LabelTextColor => new(91, 54, 55);
    public override Color DescriptionTextColor => new(143, 91, 86);
    public override Color SeparatorTextColor => new(212, 136, 96);
}
