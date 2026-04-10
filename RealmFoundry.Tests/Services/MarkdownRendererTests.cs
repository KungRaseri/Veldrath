namespace RealmFoundry.Tests.Services;

public class MarkdownRendererTests
{
    [Fact]
    public void RenderSafe_NullInput_ReturnsEmptyString()
    {
        var result = MarkdownRenderer.RenderSafe(null);
        result.Should().BeEmpty();
    }

    [Fact]
    public void RenderSafe_EmptyInput_ReturnsEmptyString()
    {
        var result = MarkdownRenderer.RenderSafe(string.Empty);
        result.Should().BeEmpty();
    }

    [Fact]
    public void RenderSafe_PlainMarkdown_RendersToHtml()
    {
        var result = MarkdownRenderer.RenderSafe("# Heading");
        result.Should().Contain("<h1>");
        result.Should().Contain("Heading");
    }

    [Fact]
    public void RenderSafe_Bold_RendersStrongTag()
    {
        var result = MarkdownRenderer.RenderSafe("**bold text**");
        result.Should().Contain("<strong>");
        result.Should().Contain("bold text");
    }

    [Fact]
    public void RenderSafe_ScriptTag_IsStripped()
    {
        var result = MarkdownRenderer.RenderSafe("<script>alert('xss')</script>");
        result.Should().NotContain("<script>");
        result.Should().NotContain("alert('xss')");
    }

    [Fact]
    public void RenderSafe_OnClickAttribute_IsStripped()
    {
        var result = MarkdownRenderer.RenderSafe("<a href=\"/\" onclick=\"evil()\">link</a>");
        result.Should().NotContain("onclick");
        result.Should().Contain("link");
    }

    [Fact]
    public void RenderSafe_StyleAttribute_IsStripped()
    {
        var result = MarkdownRenderer.RenderSafe("<p style=\"color:red\">text</p>");
        result.Should().NotContain("style=");
        result.Should().Contain("text");
    }

    [Fact]
    public void RenderSafe_JavascriptHref_IsStripped()
    {
        var result = MarkdownRenderer.RenderSafe("[click](javascript:evil())");
        result.Should().NotContain("javascript:");
    }

    [Fact]
    public void RenderSafe_DetailsAndSummaryTags_AreAllowed()
    {
        var result = MarkdownRenderer.RenderSafe("<details><summary>Title</summary>Body</details>");
        result.Should().Contain("<details>");
        result.Should().Contain("<summary>");
    }

    [Fact]
    public void RenderSafe_ImgTag_IsAllowed()
    {
        var result = MarkdownRenderer.RenderSafe("![alt](https://example.com/img.png)");
        result.Should().Contain("<img");
    }
}
