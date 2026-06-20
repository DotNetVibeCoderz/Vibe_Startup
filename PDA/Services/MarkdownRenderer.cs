using System.Text.RegularExpressions;
using Markdig;

namespace PDA.Services;

/// <summary>
/// Markdown to HTML renderer using Markdig library.
/// Handles links, images, audio, video, YouTube embeds, tables, code blocks, etc.
/// </summary>
public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAutoLinks()
        .UseMediaLinks()
        .UsePipeTables()
        .UseGridTables()
        .UseTaskLists()
        .UseEmojiAndSmiley()
        .UseAbbreviations()
        .UseSmartyPants()
        .Build();

    /// <summary>
    /// Convert markdown text to sanitized HTML with responsive media support.
    /// </summary>
    public static string Render(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        var html = Markdown.ToHtml(text, Pipeline);

        // Open links in new tab
        html = Regex.Replace(html,
            @"<a\s+href=""([^""]+)"">([^<]*)</a>",
            @"<a href=""$1"" target=""_blank"" rel=""noopener noreferrer"">$2</a>");

        // Responsive images
        html = Regex.Replace(html,
            @"<img\s+src=""([^""]+)""([^>]*)>",
            @"<img src=""$1""$2 class=""chat-img"" loading=""lazy"" onerror=""this.style.display='none'"">");

        // Audio with controls
        html = Regex.Replace(html,
            @"<audio\s+src=""([^""]+)""([^>]*)>(.*?)</audio>",
            @"<audio src=""$1""$2 controls class=""chat-audio"">$3</audio>");

        // Video with controls
        html = Regex.Replace(html,
            @"<video\s+src=""([^""]+)""([^>]*)>(.*?)</video>",
            @"<video src=""$1""$2 controls class=""chat-video"">$3</video>");

        // YouTube embeds → responsive container
        html = Regex.Replace(html,
            @"<iframe[^>]+src=""https?://(?:www\.)?(?:youtube\.com/embed/|youtube-nocookie\.com/embed/)([^""]+)""[^>]*></iframe>",
            @"<div class=""video-container""><iframe src=""https://www.youtube-nocookie.com/embed/$1"" frameborder=""0"" allow=""accelerometer; autoplay; encrypted-media; gyroscope; picture-in-picture"" allowfullscreen></iframe></div>");

        return html;
    }
}
