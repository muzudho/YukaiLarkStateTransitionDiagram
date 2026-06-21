namespace YukaiLarkStateTransitionDiagram.Persistence;

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

internal static class YukaiDialogJsonSerializer
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        IncludeFields = true
    };
}
