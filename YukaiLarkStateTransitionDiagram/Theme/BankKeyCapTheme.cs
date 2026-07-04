namespace YukaiLarkStateTransitionDiagram.Theme;

using Microsoft.Xna.Framework;

public sealed class BankKeyCapTheme : KeyCapThemeBase
{
    public override string Name => "Bank";
    public override Color FaceColor => new(42, 110, 82);
    public override Color TopEdgeColor => new(118, 176, 132);
    public override Color BottomEdgeColor => new(22, 58, 48);
    public override Color InnerHighlightColor => new(56, 132, 94);
    public override Color LabelTextColor => new(232, 224, 184);
    public override Color DescriptionTextColor => new(178, 188, 154);
    public override Color SeparatorTextColor => new(200, 164, 74);
}
