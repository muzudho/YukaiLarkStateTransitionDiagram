namespace YukaiLarkStateTransitionDiagram.Theme;

using Microsoft.Xna.Framework;

public sealed class ChineseDragonKeyCapTheme : KeyCapThemeBase
{
    public override string Name => "ChineseDragon";
    public override Color FaceColor => new(198, 42, 42);
    public override Color TopEdgeColor => new(255, 112, 80);
    public override Color BottomEdgeColor => new(98, 22, 32);
    public override Color InnerHighlightColor => new(222, 58, 48);
    public override Color LabelTextColor => new(255, 230, 132);
    public override Color DescriptionTextColor => new(244, 186, 92);
    public override Color SeparatorTextColor => new(255, 210, 80);
}
