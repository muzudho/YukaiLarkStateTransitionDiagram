namespace SkylarkStateTransitionDiagram.Theme;

using Microsoft.Xna.Framework;

public sealed class CopyPaperKeyCapTheme : KeyCapThemeBase
{
    public override string Name => "CopyPaper";
    public override Color FaceColor => new(252, 252, 248);
    public override Color TopEdgeColor => new(255, 255, 255);
    public override Color BottomEdgeColor => new(185, 188, 190);
    public override Color InnerHighlightColor => new(255, 255, 255);
    public override Color LabelTextColor => new(25, 28, 32);
}