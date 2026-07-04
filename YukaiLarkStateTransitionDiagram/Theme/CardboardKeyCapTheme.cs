namespace YukaiLarkStateTransitionDiagram.Theme;

using Microsoft.Xna.Framework;

public sealed class CardboardKeyCapTheme : KeyCapThemeBase
{
    public override string Name => "Cardboard";
    public override Color FaceColor => new(178, 132, 82);
    public override Color TopEdgeColor => new(226, 188, 132);
    public override Color BottomEdgeColor => new(103, 72, 44);
    public override Color InnerHighlightColor => new(198, 150, 94);
    public override Color LabelTextColor => new(58, 42, 30);
    public override Color DescriptionTextColor => new(118, 86, 56);
    public override Color SeparatorTextColor => new(154, 108, 64);
}
