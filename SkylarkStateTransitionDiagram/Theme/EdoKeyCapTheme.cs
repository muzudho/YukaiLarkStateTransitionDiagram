namespace SkylarkStateTransitionDiagram.Theme;

using Microsoft.Xna.Framework;

public sealed class EdoKeyCapTheme : KeyCapThemeBase
{
    public override string Name => "Edo";
    public override Color FaceColor => new(46, 78, 92);
    public override Color TopEdgeColor => new(206, 189, 133);
    public override Color BottomEdgeColor => new(20, 36, 43);
    public override Color InnerHighlightColor => new(67, 105, 118);
    public override Color LabelTextColor => new(245, 235, 202);
}