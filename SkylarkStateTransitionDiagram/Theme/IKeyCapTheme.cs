namespace SkylarkStateTransitionDiagram.Theme;

using Microsoft.Xna.Framework;

public interface IKeyCapTheme
{
    string Name { get; }
    Color FaceColor { get; }
    Color TopEdgeColor { get; }
    Color BottomEdgeColor { get; }
    Color InnerHighlightColor { get; }
    Color LabelTextColor { get; }
    Color DescriptionTextColor { get; }
    Color SeparatorTextColor { get; }
    int Height { get; }
    int MinWidth { get; }
    int HorizontalPadding { get; }
    float FontSize { get; }
}