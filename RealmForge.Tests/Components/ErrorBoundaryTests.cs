using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using RealmForge.Components.Shared;

namespace RealmForge.Tests.Components;

/// <summary>
/// Tests for the ErrorBoundary component
/// </summary>
public class ErrorBoundaryTests : TestContext
{
    public ErrorBoundaryTests()
    {
        Services.AddMudServices();
    }

    [Fact]
    public void ErrorBoundary_Should_Render_Successfully()
    {
        // Act & Assert - component renders without errors
        var cut = RenderComponent<ErrorBoundary>();
        cut.Should().NotBeNull();
    }

    [Fact]
    public void ErrorBoundary_Should_Render_Child_Content_When_Provided()
    {
        // Act
        var cut = RenderComponent<ErrorBoundary>(parameters => parameters
            .AddChildContent("<div id='test-content'>Test Content</div>"));

        // Assert
        cut.Find("#test-content").TextContent.Should().Be("Test Content");
    }

    [Fact]
    public void ErrorBoundary_Should_Render_Without_Errors()
    {
        // Act & Assert - should not throw
        var cut = RenderComponent<ErrorBoundary>();
        cut.Should().NotBeNull();
    }
}
