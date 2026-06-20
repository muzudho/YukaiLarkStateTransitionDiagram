namespace SkylarkStateTransitionDiagram.Theme;

using Microsoft.Xna.Framework;

public sealed class MonochromeKeyCapTheme : KeyCapThemeBase
{
    public override string Name => "Monochrome";
    public override Color FaceColor => new(235, 235, 235);
    public override Color TopEdgeColor => new(255, 255, 255);
    public override Color BottomEdgeColor => new(72, 72, 72);
    public override Color InnerHighlightColor => new(248, 248, 248);
    public override Color LabelTextColor => new(18, 18, 18);
}
