using Markdig;

namespace AppBender.Core.AI;

/// <summary>Renders chat/AI markdown (tables, code, media, task lists) to HTML.</summary>
public class MarkdownService
{
    private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()      // tables, footnotes, task lists, auto-links...
        .UseMediaLinks()              // ![](video.mp4) -> <video>, youtube embeds
        .UseEmojiAndSmiley()
        .UseSoftlineBreakAsHardlineBreak()
        .DisableHtml()                // prevent raw-HTML script injection from model output
        .Build();

    public string ToHtml(string? markdown)
        => string.IsNullOrWhiteSpace(markdown) ? "" : Markdown.ToHtml(markdown, _pipeline);
}
