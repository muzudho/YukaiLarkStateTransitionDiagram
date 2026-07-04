namespace YukaiLarkStateTransitionDiagram.Theme;

using Microsoft.Xna.Framework;

public sealed class CasinoKeyCapTheme : KeyCapThemeBase
{
    public override string Name => "Casino";
    public override Color FaceColor => new(24, 118, 68);
    public override Color TopEdgeColor => new(70, 184, 104);
    public override Color BottomEdgeColor => new(94, 16, 34);
    public override Color InnerHighlightColor => new(36, 146, 78);
    public override Color LabelTextColor => new(255, 232, 142);
    public override Color DescriptionTextColor => new(226, 190, 120);
    public override Color SeparatorTextColor => new(226, 48, 58);
}
