using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using RealmEngine.Core;
using RealmEngine.Core.Features.Characters.Queries;

namespace RealmEngine.Core.Tests.Features.Characters.Queries;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for GetBackgroundsHandler using real JSON data
/// </summary>
public class GetBackgroundsHandlerTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IServiceCollection _services;

    public GetBackgroundsHandlerTests()
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
    public async Task Handle_ShouldReturnAllBackgrounds()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var query = new GetBackgroundsQuery();

        // Act
        var result = await mediator.Send(query);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(12);
    }

    [Theory]
    [InlineData("strength", 2)]
    [InlineData("dexterity", 2)]
    [InlineData("constitution", 2)]
    [InlineData("intelligence", 2)]
    [InlineData("wisdom", 2)]
    [InlineData("charisma", 2)]
    public async Task Handle_WithAttributeFilter_ShouldReturnFilteredBackgrounds(string attribute, int expectedCount)
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var query = new GetBackgroundsQuery(attribute);

        // Act
        var result = await mediator.Send(query);

        // Assert
        result.Should().HaveCount(expectedCount);
        result.Should().AllSatisfy(b => b.PrimaryAttribute.Should().Be(attribute));
    }

    [Fact]
    public async Task Handle_ShouldReturnBackgroundsWithCorrectBonuses()
    {using var scope = _serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var query = new GetBackgroundsQuery();

        // Act
        var result = await mediator.Send(query);

        // Assert
        result.Should().AllSatisfy(b =>
        {
            b.PrimaryBonus.Should().Be(2);
            b.SecondaryBonus.Should().Be(1);
            b.PrimaryAttribute.Should().NotBeNullOrEmpty();
            b.SecondaryAttribute.Should().NotBeNullOrEmpty();
        });
    }

    [Fact]
    public async Task Handle_ShouldReturnBackgroundsWithLocationRecommendations()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var query = new GetBackgroundsQuery();

        // Act
        var result = await mediator.Send(query);

        // Assert
        result.Should().AllSatisfy(b =>
        {
            b.RecommendedLocationTypes.Should().HaveCount(3);
            b.RecommendedLocationTypes.Should().Contain(type => 
                type == "settlement" || type == "wilderness" || type == "dungeon");
        });
    }

    [Theory]
    [InlineData("soldier")]
    [InlineData("criminal")]
    [InlineData("scholar")]
    [InlineData("noble")]
    public async Task GetBackground_BySlug_ShouldReturnCorrectBackground(string slug)
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var query = new GetBackgroundQuery(slug);

        // Act
        var result = await mediator.Send(query);

        // Assert
        result.Should().NotBeNull();
        result!.Slug.Should().Be(slug);
    }

    [Fact]
    public async Task GetBackground_WithInvalidSlug_ShouldReturnNull()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var query = new GetBackgroundQuery("nonexistent-background");

        // Act
        var result = await mediator.Send(query);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnBackgroundsWithUniqueNames()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var query = new GetBackgroundsQuery();

        // Act
        var result = await mediator.Send(query);

        // Assert
        result.Select(b => b.Name).Should().OnlyHaveUniqueItems();
        result.Select(b => b.Slug).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Background_ApplyBonuses_ShouldIncreaseCharacterAttributes()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var query = new GetBackgroundQuery("soldier");
        var background = await mediator.Send(query);
        var character = new RealmEngine.Shared.Models.Character
        {
            Name = "TestHero",
            Strength = 10,
            Constitution = 10,
            Dexterity = 10,
            Intelligence = 10,
            Wisdom = 10,
            Charisma = 10
        };

        // Act
        background!.ApplyBonuses(character);

        // Assert
        character.Strength.Should().Be(12); // +2 from soldier primary
        character.Constitution.Should().Be(11); // +1 from soldier secondary
    }

    [Fact]
    public async Task Handle_ShouldReturnExpectedBackgrounds()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var query = new GetBackgroundsQuery();
        var expectedBackgrounds = new[]
        {
            "soldier", "laborer", "criminal", "entertainer",
            "folk-hero", "outlander", "scholar", "sage",
            "acolyte", "hermit", "noble", "charlatan"
        };

        // Act
        var result = await mediator.Send(query);

        // Assert
        result.Select(b => b.Slug).Should().BeEquivalentTo(expectedBackgrounds);
    }
}
