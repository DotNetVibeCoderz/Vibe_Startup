// Markdown rendering service using Markdig
using Markdig;

namespace Joka.Services;

public class MarkdownService
{
    private readonly MarkdownPipeline _pipeline;

    public MarkdownService()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UsePipeTables()
            .UseMediaLinks()
            .UseEmojiAndSmiley()
            .Build();
    }

    /// <summary>
    /// Convert Markdown text to HTML with full extension support.
    /// Supports: tables, code blocks, images, videos, audio, emojis.
    /// </summary>
    public string ToHtml(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return string.Empty;
        var html = Markdown.ToHtml(markdown, _pipeline);
        return PostProcess(html);
    }

    /// <summary>
    /// Post-process HTML to add additional styling and media support.
    /// </summary>
    private string PostProcess(string html)
    {
        // Add responsive wrapper for tables
        html = html.Replace("<table>", "<div class='table-wrapper'><table class='md-table'>");
        html = html.Replace("</table>", "</table></div>");

        // Make images responsive
        html = System.Text.RegularExpressions.Regex.Replace(html,
            @"<img\s+src=""([^""]+)""",
            @"<img class=""md-image img-fluid rounded"" loading=""lazy"" src=""$1""");

        // Add code block styling
        html = html.Replace("<pre><code>", "<pre class='code-block'><code>");
        
        return html;
    }
}
