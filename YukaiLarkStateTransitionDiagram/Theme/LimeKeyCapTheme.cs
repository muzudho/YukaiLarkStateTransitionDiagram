namespace YukaiLarkStateTransitionDiagram.Theme;

using Microsoft.Xna.Framework;

public sealed class LimeKeyCapTheme : KeyCapThemeBase
{
    public override string Name => "Lime";
    public override Color FaceColor => new(143, 216, 74);
    public override Color TopEdgeColor => new(214, 255, 162);
    public override Color BottomEdgeColor => new(62, 124, 48);
    public override Color InnerHighlightColor => new(168, 235, 90);
    public override Color LabelTextColor => new(39, 72, 48);
    public override Color DescriptionTextColor => new(73, 122, 74);
    public override Color SeparatorTextColor => new(162, 180, 70);
}
