namespace SkylarkStateTransitionDiagram.Theme;

using Microsoft.Xna.Framework;

public sealed class MintKeyCapTheme : KeyCapThemeBase
{
    public override string Name => "Mint";
    public override Color FaceColor => new(191, 238, 220);
    public override Color TopEdgeColor => new(235, 255, 247);
    public override Color BottomEdgeColor => new(56, 132, 110);
    public override Color InnerHighlightColor => new(212, 249, 235);
    public override Color LabelTextColor => new(24, 72, 64);
}
