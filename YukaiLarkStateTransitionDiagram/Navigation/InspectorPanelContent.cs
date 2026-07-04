namespace YukaiLarkStateTransitionDiagram.Navigation;

using System.Collections.Generic;

/// <summary>
/// インスペクターパネルへ表示する行の集まりです。
/// </summary>
public sealed class InspectorPanelContent
{
    private readonly List<InspectorPanelLine> _lines = new();

    public IReadOnlyList<InspectorPanelLine> Lines => _lines;

    public void AddPrimary(string text)
        => _lines.Add(new InspectorPanelLine(text, InspectorPanelTextStyle.Primary));

    public void AddSecondary(string text)
        => _lines.Add(new InspectorPanelLine(text, InspectorPanelTextStyle.Secondary));

    public void AddSectionTitle(string text)
        => _lines.Add(new InspectorPanelLine(text, InspectorPanelTextStyle.SectionTitle));

    public void AddAction(string text, InspectorPanelAction action)
        => _lines.Add(new InspectorPanelLine(text, InspectorPanelTextStyle.Action, action));
}

public readonly record struct InspectorPanelLine(
    string Text,
    InspectorPanelTextStyle Style,
    InspectorPanelAction Action = InspectorPanelAction.None);

public enum InspectorPanelTextStyle
{
    Primary = 0,
    Secondary = 1,
    SectionTitle = 2,
    Action = 3
}

public enum InspectorPanelAction
{
    None = 0,
    UnlinkSelectedNodeSubstate = 1,
    UnlinkCurrentDiagramParentSubstate = 2
}
