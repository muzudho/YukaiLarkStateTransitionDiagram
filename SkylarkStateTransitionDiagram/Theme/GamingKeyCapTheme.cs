namespace SkylarkStateTransitionDiagram.Theme;

using Microsoft.Xna.Framework;

public sealed class GamingKeyCapTheme : KeyCapThemeBase
{
    public override string Name => "Gaming";
    public override Color FaceColor => new(28, 32, 44);
    public override Color TopEdgeColor => new(86, 244, 255);
    public override Color BottomEdgeColor => new(214, 75, 255);
    public override Color InnerHighlightColor => new(70, 82, 112);
    public override Color LabelTextColor => new(235, 252, 255);
}