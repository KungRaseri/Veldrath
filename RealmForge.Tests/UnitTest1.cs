using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace RealmForge.Tests;

/// <summary>
/// Phase 1 smoke tests to verify infrastructure setup
/// </summary>
public class InfrastructureTests : TestContext
{
    public InfrastructureTests()
    {
        // Register MudBlazor services for component tests
        Services.AddMudServices();
    }

    [Fact]
    public void bUnit_Test_Context_Should_Be_Available()
    {
        // Arrange & Act
        var context = this;

        // Assert
        context.Should().NotBeNull("bUnit TestContext should be initialized");
        context.Services.Should().NotBeNull("Service provider should be available");
    }

    [Fact]
    public void MudBlazor_Services_Should_Register_Successfully()
    {
        // Arrange & Act
        var serviceProvider = Services.BuildServiceProvider();

        // Assert - verify services are registered without errors
        serviceProvider.Should().NotBeNull("Service provider should build successfully");
    }

    [Fact]
    public void FluentAssertions_Should_Work()
    {
        // Arrange
        var testValue = "Phase 1 Infrastructure";

        // Act & Assert
        testValue.Should().NotBeNullOrEmpty();
        testValue.Should().Contain("Infrastructure");
    }
}
