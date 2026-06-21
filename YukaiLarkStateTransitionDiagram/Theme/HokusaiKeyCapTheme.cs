namespace YukaiLarkStateTransitionDiagram.Theme;

using Microsoft.Xna.Framework;

public sealed class HokusaiKeyCapTheme : KeyCapThemeBase
{
    public override string Name => "Hokusai";
    public override Color FaceColor => new(32, 72, 142);
    public override Color TopEdgeColor => new(244, 238, 211);
    public override Color BottomEdgeColor => new(13, 33, 82);
    public override Color InnerHighlightColor => new(58, 103, 171);
    public override Color LabelTextColor => new(250, 246, 225);
    public override Color DescriptionTextColor => new(44, 72, 107);
    public override Color SeparatorTextColor => new(102, 124, 142);
}