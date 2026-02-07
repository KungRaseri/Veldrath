using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RealmEngine.Core;
using RealmEngine.Core.Features.CharacterCreation.Queries;
using RealmEngine.Data;
using RealmEngine.Shared.Abstractions;

namespace RealmEngine.Core.Tests.Features.CharacterCreation.Queries;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for GetCharacterClassHandler using real JSON data.
/// </summary>
public class GetCharacterClassHandlerTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IServiceCollection _services;

    public GetCharacterClassHandlerTests()
    {
        _services = new ServiceCollection();
        
        // Register logging
        _services.AddLogging();
        
        // Register RealmEngine services
        _services.AddRealmEngineData(GetDataPath());
        _services.AddRealmEngineCore();
        _services.AddRealmEngineMediatR();
        
        _serviceProvider = _services.BuildServiceProvider();
    }

    private static string GetDataPath()
    {
        // Start from test assembly location and navigate to Data/Json
        var assemblyPath = Path.GetDirectoryName(typeof(GetCharacterClassHandlerTests).Assembly.Location)!;
        var solutionRoot = Path.GetFullPath(Path.Combine(assemblyPath, "..", "..", "..", ".."));
        return Path.Combine(solutionRoot, "RealmEngine.Data", "Data", "Json");
    }

    public void Dispose()
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    [Fact]
    public async Task Handle_Should_Return_Found_True_For_Valid_Class()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICharacterClassRepository>();
        var handler = new GetCharacterClassHandler(repository);
        var query = new GetCharacterClassQuery { ClassName = "Fighter" }; // Using real catalog name

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Found.Should().BeTrue();
        result.CharacterClass.Should().NotBeNull();
        result.CharacterClass!.Name.Should().Be("Fighter");
    }

    [Fact]
    public async Task Handle_Should_Return_Found_False_For_Invalid_Class()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICharacterClassRepository>();
        var handler = new GetCharacterClassHandler(repository);
        var query = new GetCharacterClassQuery { ClassName = "InvalidClass" };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Found.Should().BeFalse();
        result.CharacterClass.Should().BeNull();
    }

    [Theory]
    [InlineData("Fighter")]
    [InlineData("Wizard")]
    [InlineData("Priest")]
    public async Task Handle_Should_Return_Valid_Class_Data(string className)
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICharacterClassRepository>();
        var handler = new GetCharacterClassHandler(repository);
        var query = new GetCharacterClassQuery { ClassName = className };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Found.Should().BeTrue();
        result.CharacterClass.Should().NotBeNull();
        result.CharacterClass!.Name.Should().Be(className);
        result.CharacterClass.Description.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_Should_Be_Case_Insensitive()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICharacterClassRepository>();
        var handler = new GetCharacterClassHandler(repository);
        var query = new GetCharacterClassQuery { ClassName = "fighter" }; // lowercase

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert - Repository is case-insensitive
        result.Should().NotBeNull();
        result.Found.Should().BeTrue("Repository should handle case-insensitive lookups");
        result.CharacterClass!.Name.Should().Be("Fighter");
    }

    [Fact]
    public async Task Handle_Should_Return_Null_CharacterClass_When_Not_Found()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICharacterClassRepository>();
        var handler = new GetCharacterClassHandler(repository);
        var query = new GetCharacterClassQuery { ClassName = "NonExistentClass" };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Found.Should().BeFalse();
        result.CharacterClass.Should().BeNull();
    }
}
