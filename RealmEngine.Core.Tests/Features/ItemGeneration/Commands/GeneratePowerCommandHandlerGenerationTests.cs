using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.ItemGeneration.Commands;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Core.Tests.Infrastructure;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.ItemGeneration.Commands;

/// <summary>
/// Execution-path tests for <see cref="GeneratePowerCommandHandler"/> (source file
/// <c>GenerateAbilityCommandHandler.cs</c>). The wave-1 suite covers the category/subcategory
/// validation arms; these tests cover named lookup, random generation, empty results and failure.
/// </summary>
[Trait("Category", "Feature")]
public class GeneratePowerCommandHandlerGenerationTests
{
    private static Power MakePower(string slug, string displayName, int rarityWeight = 10) => new()
    {
        Id = slug,
        Slug = slug,
        Name = slug,
        DisplayName = displayName,
        RarityWeight = rarityWeight
    };

    private static (GeneratePowerCommandHandler Handler, Mock<IPowerRepository> Repository) CreateHandler()
    {
        var repository = new Mock<IPowerRepository>();
        var generator = new PowerGenerator(repository.Object, NullLogger<PowerGenerator>.Instance);
        return (new GeneratePowerCommandHandler(generator, NullLogger<GeneratePowerCommandHandler>.Instance), repository);
    }

    private static GeneratePowerCommand Command(string? powerName = null, int count = 1) => new()
    {
        Category = "active",
        Subcategory = "offensive",
        PowerName = powerName,
        Count = count
    };

    [Fact]
    public async Task Handle_Returns_The_Named_Power_When_It_Exists()
    {
        var (handler, repository) = CreateHandler();
        var fireball = MakePower("fireball", "Fireball");
        repository.Setup(r => r.GetBySlugAsync("fireball")).ReturnsAsync(fireball);

        var result = await handler.Handle(Command("fireball"), default);

        result.Success.Should().BeTrue();
        result.Power.Should().BeSameAs(fireball);
        result.Powers.Should().ContainSingle().Which.Should().BeSameAs(fireball);
        result.ErrorMessage.Should().BeNull();
        repository.Verify(r => r.GetBySlugAsync("fireball"), Times.Once);
        repository.Verify(r => r.GetByTypeAsync(It.IsAny<string>()), Times.Never,
            "a named lookup must not fall through to random generation");
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_The_Named_Power_Does_Not_Exist()
    {
        var (handler, repository) = CreateHandler();
        repository.Setup(r => r.GetBySlugAsync(It.IsAny<string>())).ReturnsAsync((Power?)null);

        var result = await handler.Handle(Command("wish"), default);

        result.Success.Should().BeFalse();
        result.Power.Should().BeNull();
        result.Powers.Should().BeEmpty();
        result.ErrorMessage.Should().Be("Power not found: active/offensive/wish");
    }

    [Fact]
    public async Task Handle_Generates_The_Requested_Number_Of_Random_Powers()
    {
        var (handler, repository) = CreateHandler();
        repository.Setup(r => r.GetByTypeAsync("active/offensive")).ReturnsAsync(
        [
            MakePower("power-strike", "Power Strike", rarityWeight: 30),
            MakePower("cleave", "Cleave", rarityWeight: 0)
        ]);

        var result = await handler.Handle(Command(count: 3), default);

        result.Success.Should().BeTrue();
        result.Powers.Should().HaveCount(3);
        result.Power.Should().BeSameAs(result.Powers[0], "the first generated power is exposed as the primary result");
        result.Powers.Should().OnlyContain(p => p.Slug == "power-strike" || p.Slug == "cleave");
        repository.Verify(r => r.GetByTypeAsync("active/offensive"), Times.Once);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_The_Subcategory_Has_No_Powers()
    {
        var (handler, repository) = CreateHandler();
        repository.Setup(r => r.GetByTypeAsync(It.IsAny<string>())).ReturnsAsync([]);

        var result = await handler.Handle(Command(count: 2), default);

        result.Success.Should().BeFalse();
        result.Power.Should().BeNull();
        result.Powers.Should().BeEmpty();
        result.ErrorMessage.Should().Be("No powers found in active/offensive");
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_The_Repository_Yields_No_List()
    {
        var (handler, repository) = CreateHandler();
        repository.Setup(r => r.GetByTypeAsync(It.IsAny<string>())).ReturnsAsync((List<Power>)null!);

        var result = await handler.Handle(Command(), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("No powers found in active/offensive");
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_The_Repository_Throws_For_A_Random_Request()
    {
        // PowerGenerator swallows catalog exceptions and returns an empty list, so the handler
        // reports the "no powers" contract rather than an exception message.
        var (handler, repository) = CreateHandler();
        repository.Setup(r => r.GetByTypeAsync(It.IsAny<string>()))
                  .ThrowsAsync(new InvalidOperationException("catalog offline"));

        var result = await handler.Handle(Command(count: 2), default);

        result.Success.Should().BeFalse();
        result.Powers.Should().BeEmpty();
        result.ErrorMessage.Should().Be("No powers found in active/offensive");
    }

    [Fact]
    public async Task Handle_Reports_An_Unexpected_Failure_Instead_Of_Propagating_It()
    {
        // Arrange — the generation log throws, which exercises the handler's error boundary.
        var repository = new Mock<IPowerRepository>();
        repository.Setup(r => r.GetBySlugAsync(It.IsAny<string>())).ReturnsAsync(MakePower("fireball", "Fireball"));
        var handler = new GeneratePowerCommandHandler(
            new PowerGenerator(repository.Object, NullLogger<PowerGenerator>.Instance),
            ThrowingInformationLogger<GeneratePowerCommandHandler>.Instance);

        // Act
        var result = await handler.Handle(Command("fireball"), default);

        // Assert
        result.Success.Should().BeFalse();
        result.Power.Should().BeNull();
        result.ErrorMessage.Should().StartWith("Generation failed: ").And.Contain("log sink unavailable");
    }
}
