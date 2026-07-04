namespace YukaiLarkStateTransitionDiagram.Theme;

using Microsoft.Xna.Framework;

public sealed class KidsRoomKeyCapTheme : KeyCapThemeBase
{
    public override string Name => "KidsRoom";
    public override Color FaceColor => new(255, 184, 102);
    public override Color TopEdgeColor => new(255, 230, 142);
    public override Color BottomEdgeColor => new(92, 150, 224);
    public override Color InnerHighlightColor => new(255, 202, 118);
    public override Color LabelTextColor => new(58, 76, 138);
    public override Color DescriptionTextColor => new(110, 94, 142);
    public override Color SeparatorTextColor => new(238, 94, 126);
}
