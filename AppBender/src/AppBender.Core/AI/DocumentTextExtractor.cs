using System.IO.Compression;
using System.Text;
using System.Xml;

namespace AppBender.Core.AI;

/// <summary>
/// Extracts plain text from uploaded documents: txt/md/csv/json natively,
/// PDF via PdfPig, DOCX/XLSX by reading their OpenXML parts directly (no heavy deps).
/// </summary>
public static class DocumentTextExtractor
{
    public static bool IsSupported(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".txt" or ".md" or ".csv" or ".json" or ".log" or ".xml" or ".html"
            or ".pdf" or ".docx" or ".xlsx";
    }

    public static string Extract(string fileName, Stream content)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => ExtractPdf(content),
            ".docx" => ExtractDocx(content),
            ".xlsx" => ExtractXlsx(content),
            ".html" => TavilyWebSearchClient.HtmlToText(ReadAll(content)),
            _ => ReadAll(content)
        };
    }

    private static string ReadAll(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static string ExtractPdf(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var sb = new StringBuilder();
        using (var document = UglyToad.PdfPig.PdfDocument.Open(ms.ToArray()))
        {
            foreach (var page in document.GetPages())
                sb.AppendLine(page.Text);
        }
        return sb.ToString();
    }

    private static string ExtractDocx(Stream stream)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var entry = archive.GetEntry("word/document.xml");
        if (entry is null) return "";
        var sb = new StringBuilder();
        using var entryStream = entry.Open();
        using var reader = XmlReader.Create(entryStream);
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                if (reader.LocalName == "t") sb.Append(reader.ReadElementContentAsString());
                else if (reader.LocalName is "p" or "br" or "tab") sb.Append('\n');
            }
        }
        return sb.ToString();
    }

    private static string ExtractXlsx(Stream stream)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);

        // shared strings table
        var shared = new List<string>();
        var sharedEntry = archive.GetEntry("xl/sharedStrings.xml");
        if (sharedEntry is not null)
        {
            using var sharedStream = sharedEntry.Open();
            using var reader = XmlReader.Create(sharedStream);
            var current = new StringBuilder();
            var inItem = false;
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "si") { inItem = true; current.Clear(); }
                else if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "si") { inItem = false; shared.Add(current.ToString()); }
                else if (inItem && reader.NodeType == XmlNodeType.Element && reader.LocalName == "t")
                    current.Append(reader.ReadElementContentAsString());
            }
        }

        var sb = new StringBuilder();
        foreach (var entry in archive.Entries.Where(e =>
                     e.FullName.StartsWith("xl/worksheets/") && e.FullName.EndsWith(".xml")))
        {
            sb.AppendLine($"[Sheet: {Path.GetFileNameWithoutExtension(entry.Name)}]");
            using var sheetStream = entry.Open();
            using var reader = XmlReader.Create(sheetStream);
            string? cellType = null;
            var rowValues = new List<string>();
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    switch (reader.LocalName)
                    {
                        case "row":
                            if (rowValues.Count > 0) { sb.AppendLine(string.Join("\t", rowValues)); rowValues.Clear(); }
                            break;
                        case "c":
                            cellType = reader.GetAttribute("t");
                            break;
                        case "v":
                            var value = reader.ReadElementContentAsString();
                            if (cellType == "s" && int.TryParse(value, out var idx) && idx < shared.Count)
                                rowValues.Add(shared[idx]);
                            else rowValues.Add(value);
                            break;
                    }
                }
            }
            if (rowValues.Count > 0) sb.AppendLine(string.Join("\t", rowValues));
        }
        return sb.ToString();
    }

    /// <summary>Splits text into overlapping chunks suitable for embedding.</summary>
    public static List<string> Chunk(string text, int chunkSize = 1200, int overlap = 150)
    {
        text = text.Replace("\r\n", "\n").Trim();
        if (text.Length == 0) return [];
        if (text.Length <= chunkSize) return [text];

        var chunks = new List<string>();
        var position = 0;
        while (position < text.Length)
        {
            var length = Math.Min(chunkSize, text.Length - position);
            var slice = text.Substring(position, length);
            // try to break at a paragraph/sentence boundary
            if (position + length < text.Length)
            {
                var lastBreak = slice.LastIndexOf("\n\n", StringComparison.Ordinal);
                if (lastBreak < chunkSize / 2) lastBreak = slice.LastIndexOf(". ", StringComparison.Ordinal);
                if (lastBreak >= chunkSize / 2) { slice = slice[..(lastBreak + 1)]; length = slice.Length; }
            }
            chunks.Add(slice.Trim());
            position += Math.Max(1, length - overlap);
        }
        return chunks.Where(c => c.Length > 0).ToList();
    }
}
