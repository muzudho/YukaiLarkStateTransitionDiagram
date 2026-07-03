namespace YukaiLarkStateTransitionDiagram;

using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

/// <summary>
/// ダイアグラムのインスタンスを表すクラスです。
/// </summary>
public sealed class DiagramInstance
{
    public int Id { get; set; } = 1;
    public string Name { get; set; } = "ダイアグラム1";
    public DiagramDataSection Data { get; set; } = new();
    public AssistSuppressionSection AssistSuppression { get; set; } = new();
    public int NextNodeId { get; set; } = 1;

    [JsonIgnore]
    public List<DiagramNode> Nodes
    {
        get => Data.Nodes;
        set => Data.Nodes = value ?? new List<DiagramNode>();
    }

    [JsonIgnore]
    public List<DiagramTransition> Transitions
    {
        get => Data.Transitions;
        set => Data.Transitions = value ?? new List<DiagramTransition>();
    }

    public static DiagramInstance CreateDefault()
        => new();

    public static DiagramInstance CreateNew(int id)
        => new()
        {
            Id = id,
            Name = $"ダイアグラム{id}"
        };

    public void RefreshNextNodeId()
        => NextNodeId = Nodes.Count == 0 ? 1 : Nodes.Max(node => node.Id) + 1;
}