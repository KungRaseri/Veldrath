using Bunit;
using FluentAssertions;
using FluentValidation.Results;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using RealmForge.Components.Shared;

namespace RealmForge.Tests.Components;

/// <summary>
/// Tests for the ValidationPanel component
/// </summary>
public class ValidationPanelTests : TestContext
{
    public ValidationPanelTests()
    {
        Services.AddMudServices();
        
        // Setup JSInterop for MudBlazor components (especially MudChip keyboard interactions)
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void ValidationPanel_Should_Render_Successfully()
    {
        // Act
        var cut = RenderComponent<ValidationPanel>();

        // Assert
        cut.Should().NotBeNull();
        cut.Markup.Should().NotBeEmpty();
    }

    [Fact]
    public void ValidationPanel_Should_Display_No_Validation_Message_When_Null()
    {
        // Act
        var cut = RenderComponent<ValidationPanel>(parameters => parameters
            .Add(p => p.ValidationResult, null));

        // Assert
        cut.Markup.Should().Contain("No validation performed yet");
    }

    [Fact]
    public void ValidationPanel_Should_Display_Success_For_Valid_Result()
    {
        // Arrange
        var validResult = new ValidationResult();

        // Act
        var cut = RenderComponent<ValidationPanel>(parameters => parameters
            .Add(p => p.ValidationResult, validResult));

        // Assert
        cut.Markup.Should().Contain("No validation errors found");
        cut.Markup.Should().Contain("JSON is valid");
        cut.Markup.Should().Contain("Valid");
    }

    [Fact]
    public void ValidationPanel_Should_Display_Errors_For_Invalid_Result()
    {
        // Arrange
        var errors = new List<ValidationFailure>
        {
            new ValidationFailure("Name", "Name is required"),
            new ValidationFailure("Level", "Level must be greater than 0")
        };
        var invalidResult = new ValidationResult(errors);

        // Act
        var cut = RenderComponent<ValidationPanel>(parameters => parameters
            .Add(p => p.ValidationResult, invalidResult));

        // Assert
        cut.Markup.Should().Contain("Name");
        cut.Markup.Should().Contain("Name is required");
        cut.Markup.Should().Contain("Level");
        cut.Markup.Should().Contain("Level must be greater than 0");
        cut.Markup.Should().Contain("2 Error(s)");
    }

    [Fact]
    public void ValidationPanel_Should_Display_Error_Count_Chip()
    {
        // Arrange
        var errors = new List<ValidationFailure>
        {
            new ValidationFailure("Field1", "Error 1"),
            new ValidationFailure("Field2", "Error 2"),
            new ValidationFailure("Field3", "Error 3")
        };
        var invalidResult = new ValidationResult(errors);

        // Act
        var cut = RenderComponent<ValidationPanel>(parameters => parameters
            .Add(p => p.ValidationResult, invalidResult));

        // Assert
        cut.Markup.Should().Contain("3 Error(s)");
    }

    [Fact]
    public void ValidationPanel_Should_Show_Success_Chip_When_Valid()
    {
        // Arrange
        var validResult = new ValidationResult();

        // Act
        var cut = RenderComponent<ValidationPanel>(parameters => parameters
            .Add(p => p.ValidationResult, validResult));

        // Assert
        cut.Markup.Should().Contain("Valid");
        var mudChip = cut.FindComponent<MudChip<string>>();
        mudChip.Should().NotBeNull();
    }

    [Fact]
    public void ValidationPanel_Should_List_All_Validation_Errors()
    {
        // Arrange
        var errors = new List<ValidationFailure>
        {
            new ValidationFailure("Name", "Name cannot be empty"),
            new ValidationFailure("RarityWeight", "RarityWeight must be positive"),
            new ValidationFailure("Description", "Description is too long")
        };
        var invalidResult = new ValidationResult(errors);

        // Act
        var cut = RenderComponent<ValidationPanel>(parameters => parameters
            .Add(p => p.ValidationResult, invalidResult));

        // Assert
        var listItems = cut.FindComponents<MudListItem<string>>();
        listItems.Count.Should().Be(3);
        
        cut.Markup.Should().Contain("Name");
        cut.Markup.Should().Contain("RarityWeight");
        cut.Markup.Should().Contain("Description");
    }

    [Fact]
    public void ValidationPanel_Should_Display_Property_Names_And_Error_Messages_Separately()
    {
        // Arrange
        var errors = new List<ValidationFailure>
        {
            new ValidationFailure("TestProperty", "This is the error message")
        };
        var invalidResult = new ValidationResult(errors);

        // Act
        var cut = RenderComponent<ValidationPanel>(parameters => parameters
            .Add(p => p.ValidationResult, invalidResult));

        // Assert
        // Property name and error message should be in separate text elements
        var textElements = cut.FindComponents<MudText>();
        textElements.Should().Contain(t => t.Markup.Contains("TestProperty"));
        textElements.Should().Contain(t => t.Markup.Contains("This is the error message"));
    }
}
