namespace SkylarkStateTransitionDiagram.Theme;

using Microsoft.Xna.Framework;

public sealed class MidnightKeyCapTheme : KeyCapThemeBase
{
    public override string Name => "Midnight";
    public override Color FaceColor => new(30, 39, 66);
    public override Color TopEdgeColor => new(104, 130, 190);
    public override Color BottomEdgeColor => new(10, 14, 27);
    public override Color InnerHighlightColor => new(48, 62, 101);
    public override Color LabelTextColor => new(229, 238, 255);
}
