namespace SkylarkStateTransitionDiagram.Theme;

using Microsoft.Xna.Framework;

public sealed class RetroKeyCapTheme : KeyCapThemeBase
{
    public override string Name => "Retro";
    public override Color FaceColor => new(216, 205, 174);
    public override Color TopEdgeColor => new(249, 239, 205);
    public override Color BottomEdgeColor => new(128, 108, 75);
    public override Color InnerHighlightColor => new(235, 225, 194);
    public override Color LabelTextColor => new(55, 48, 36);
}