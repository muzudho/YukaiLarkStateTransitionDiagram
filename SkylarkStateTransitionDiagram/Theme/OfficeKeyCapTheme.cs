namespace SkylarkStateTransitionDiagram.Theme;

using Microsoft.Xna.Framework;

public sealed class OfficeKeyCapTheme : KeyCapThemeBase
{
    public override string Name => "Office";
    public override Color FaceColor => new(232, 235, 240);
    public override Color TopEdgeColor => new(255, 255, 255);
    public override Color BottomEdgeColor => new(130, 140, 154);
    public override Color InnerHighlightColor => new(246, 248, 251);
    public override Color LabelTextColor => new(34, 42, 54);
}