using Ganss.Xss;
using Markdig;

namespace RealmFoundry.Services;

/// <summary>
/// Converts Markdown to sanitised HTML to prevent XSS injection from user-submitted content.
/// Only safe formatting elements are permitted in the output; script tags, iframes, event
/// handlers and inline styles are all stripped by the sanitiser.
/// </summary>
public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline =
        new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    // Allowlist-based sanitiser. HtmlSanitizer defaults are already restrictive;
    // we only make two tweaks: allow <details>/<summary> for collapsible sections
    // and strip the style attribute, which can be used for CSS-based exfiltration.
    private static readonly HtmlSanitizer Sanitizer = BuildSanitizer();

    private static HtmlSanitizer BuildSanitizer()
    {
        var s = new HtmlSanitizer();
        s.AllowedTags.Add("details");
        s.AllowedTags.Add("summary");
        s.AllowedAttributes.Remove("style");
        return s;
    }

    /// <summary>
    /// Renders <paramref name="markdown"/> to sanitised HTML.
    /// Returns an empty string when the input is <c>null</c> or whitespace.
    /// </summary>
    public static string RenderSafe(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return string.Empty;
        var rawHtml = Markdown.ToHtml(markdown, Pipeline);
        return Sanitizer.Sanitize(rawHtml);
    }
}
