using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using RealmForge.Components.Pages;

namespace RealmForge.Tests.Components;

/// <summary>
/// Tests for the Home page component
/// </summary>
public class HomePageTests : TestContext
{
    public HomePageTests()
    {
        Services.AddMudServices();
    }

    [Fact]
    public void Home_Page_Should_Render_Successfully()
    {
        // Act
        var cut = RenderComponent<Home>();

        // Assert
        cut.Should().NotBeNull();
        cut.Markup.Should().NotBeEmpty();
    }

    [Fact]
    public void Home_Page_Should_Display_RealmForge_Title()
    {
        // Act
        var cut = RenderComponent<Home>();

        // Assert
        cut.Markup.Should().Contain("RealmForge");
        cut.Find("h1").TextContent.Should().Contain("RealmForge");
    }

    [Fact]
    public void Home_Page_Should_Display_Subtitle()
    {
        // Act
        var cut = RenderComponent<Home>();

        // Assert
        cut.Markup.Should().Contain("JSON Data Editor for RealmEngine");
    }

    [Fact]
    public void Home_Page_Should_Display_Feature_Cards()
    {
        // Act
        var cut = RenderComponent<Home>();

        // Assert
        cut.Markup.Should().Contain("Edit JSON Files");
        cut.Markup.Should().Contain("Validate Data");
        cut.Markup.Should().Contain("Browse Catalog");
    }

    [Fact]
    public void Home_Page_Should_Have_Editor_Link()
    {
        // Act
        var cut = RenderComponent<Home>();

        // Assert
        var link = cut.Find("a[href='editor']");
        link.Should().NotBeNull();
        link.TextContent.Should().Contain("Open JSON Editor");
    }

    [Fact]
    public void Home_Page_Should_Have_Three_Feature_Cards()
    {
        // Act
        var cut = RenderComponent<Home>();

        // Assert
        var featureCards = cut.FindAll(".feature-card");
        featureCards.Count.Should().Be(3);
    }
}
