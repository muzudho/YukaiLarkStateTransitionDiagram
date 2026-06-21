namespace YukaiLarkStateTransitionDiagram.Persistence;

using System.IO;
using System.Text;
using System.Text.Json;

public static class YukaiDialogJsonWriter
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    public static void Write(string path, DiagramDocument document)
    {
        document.FormatVersion = DiagramDocument.CurrentFormatVersion;
        var json = JsonSerializer.Serialize(document, YukaiDialogJsonSerializer.Options);
        File.WriteAllText(path, json, Utf8NoBom);
    }
}
