using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RealmEngine.Core;
using RealmEngine.Core.Features.CharacterCreation.Queries;
using RealmEngine.Data;
using RealmEngine.Data.Repositories;
using RealmEngine.Shared.Abstractions;

namespace RealmEngine.Core.Tests.Features.CharacterCreation.Queries;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for GetCharacterClassesHandler using real JSON data.
/// </summary>
public class GetCharacterClassesHandlerTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IServiceCollection _services;

    public GetCharacterClassesHandlerTests()
    {
        _services = new ServiceCollection();
        
        // Register logging
        _services.AddLogging();
        
        // Register RealmEngine services
        _services.AddRealmEngineData("c:\\code\\console-game\\RealmEngine.Data\\Data\\Json");
        _services.AddRealmEngineCore();
        _services.AddRealmEngineMediatR();
        
        _serviceProvider = _services.BuildServiceProvider();
    }

    public void Dispose()
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    [Fact]
    public async Task Handle_Should_Return_All_Character_Classes()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICharacterClassRepository>();
        var handler = new GetCharacterClassesHandler(repository);
        var query = new GetCharacterClassesQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Classes.Should().NotBeNull();
        result.Classes.Should().NotBeEmpty();
        result.Classes.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task Handle_Should_Return_Expected_Character_Classes()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICharacterClassRepository>();
        var handler = new GetCharacterClassesHandler(repository);
        var query = new GetCharacterClassesQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert - Check for actual class names from catalog.json (Fighter, Priest, Wizard, etc.)
        var classNames = result.Classes.Select(c => c.Name).ToList();
        classNames.Should().Contain("Fighter", "Real class names from catalog should be returned");
        classNames.Should().Contain("Priest", "Real class names from catalog should be returned");
        classNames.Should().Contain("Wizard", "Real class names from catalog should be returned");
    }

    [Fact]
    public async Task Handle_Should_Return_Classes_With_Valid_Data()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICharacterClassRepository>();
        var handler = new GetCharacterClassesHandler(repository);
        var query = new GetCharacterClassesQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Classes.Should().AllSatisfy(c =>
        {
            c.Name.Should().NotBeNullOrEmpty();
            c.Description.Should().NotBeNullOrEmpty();
        });
    }

    [Fact]
    public async Task Handle_Should_Complete_Successfully()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICharacterClassRepository>();
        var handler = new GetCharacterClassesHandler(repository);
        var query = new GetCharacterClassesQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert - should complete successfully
        result.Should().NotBeNull();
        result.Classes.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_Should_Return_Subclass_Info()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICharacterClassRepository>();
        var handler = new GetCharacterClassesHandler(repository);
        var query = new GetCharacterClassesQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert - Paladin should be a subclass
        var paladin = result.Classes.FirstOrDefault(c => c.Name == "Paladin");
        paladin.Should().NotBeNull("Paladin should exist in catalog");
        paladin!.IsSubclass.Should().BeTrue("Paladin should be marked as a subclass");
        paladin.ParentClassId.Should().NotBeNullOrEmpty("Paladin should have a parent class");
    }
}
