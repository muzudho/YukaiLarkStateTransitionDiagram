namespace SkylarkStateTransitionDiagram.Theme;

using Microsoft.Xna.Framework;

public sealed class GirlyKeyCapTheme : KeyCapThemeBase
{
    public override string Name => "Girly";
    public override Color FaceColor => new(255, 214, 231);
    public override Color TopEdgeColor => new(255, 244, 249);
    public override Color BottomEdgeColor => new(214, 112, 151);
    public override Color InnerHighlightColor => new(255, 232, 242);
    public override Color LabelTextColor => new(94, 36, 72);
}