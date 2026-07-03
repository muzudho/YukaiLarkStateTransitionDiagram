namespace YukaiLarkStateTransitionDiagram.Persistence;

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

public static class YukaiDialogJsonReader
{
    public static DiagramDocument? Read(string path)
    {
        var json = File.ReadAllText(path, Encoding.UTF8);
        var root = JsonNode.Parse(json)?.AsObject();
        if (root is null)
        {
            return null;
        }

        if (root.ContainsKey(nameof(DiagramDocument.Data)) || root.ContainsKey(nameof(DiagramDocument.Diagrams)))
        {
            var document = JsonSerializer.Deserialize<DiagramDocument>(json, YukaiDialogJsonSerializer.Options);
            document?.EnsureDiagrams();
            return document;
        }

        var legacyDocument = JsonSerializer.Deserialize<LegacyDiagramDocument>(json, YukaiDialogJsonSerializer.Options);
        return legacyDocument is null
            ? null
            : CreateDocumentFromLegacy(legacyDocument);
    }

    private static DiagramDocument CreateDocumentFromLegacy(LegacyDiagramDocument legacyDocument)
    {
        var document = new DiagramDocument
        {
            FormatVersion = legacyDocument.FormatVersion,
            Data = new DiagramDataSection
            {
                Nodes = legacyDocument.Nodes,
                Transitions = legacyDocument.Transitions
            }
        };
        document.EnsureDiagrams();
        return document;
    }

    private sealed class LegacyDiagramDocument
    {
        public int FormatVersion { get; set; } = 1;
        public List<DiagramNode> Nodes { get; set; } = new();
        public List<DiagramTransition> Transitions { get; set; } = new();
    }
}
