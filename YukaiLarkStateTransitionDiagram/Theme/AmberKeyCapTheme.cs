namespace SkylarkStateTransitionDiagram.Theme;

using Microsoft.Xna.Framework;

public sealed class AmberKeyCapTheme : KeyCapThemeBase
{
    public override string Name => "Amber";
    public override Color FaceColor => new(244, 191, 96);
    public override Color TopEdgeColor => new(255, 233, 174);
    public override Color BottomEdgeColor => new(128, 73, 24);
    public override Color InnerHighlightColor => new(255, 211, 132);
    public override Color LabelTextColor => new(61, 36, 14);
}
