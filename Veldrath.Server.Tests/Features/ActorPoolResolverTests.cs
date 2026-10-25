using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Server.Features.Characters.Combat;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Tests for <see cref="ActorPoolResolver"/>'s failsafe behaviour: missing content is reported and never substituted.
/// </summary>
public class ActorPoolResolverTests
{
    private static ActorPoolResolver CreateResolver(Mock<IEnemyRepository> repo) =>
        new(
            new EnemyGenerator(repo.Object, NullLogger<EnemyGenerator>.Instance),
            NullLogger<ActorPoolResolver>.Instance);

    [Fact]
    public async Task SpawnRosterAsync_Returns_An_Empty_Roster_When_A_Location_Has_No_Pool()
    {
        var repo = new Mock<IEnemyRepository>(MockBehavior.Strict);
        var resolver = CreateResolver(repo);

        var roster = await resolver.SpawnRosterAsync(null);

        roster.Should().BeEmpty("no pool means nothing spawns here — a safe town, not an error");
        repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SpawnRosterAsync_Spawns_The_Configured_Archetype()
    {
        var repo = new Mock<IEnemyRepository>();
        repo.Setup(r => r.GetBySlugAsync("wolf"))
            .ReturnsAsync(new Enemy { Slug = "wolf", Name = "Wolf", Level = 2, MaxHealth = 20 });

        var roster = await CreateResolver(repo).SpawnRosterAsync([new ActorPoolEntry("wolf", 1)]);

        roster.Should().NotBeEmpty();
        roster.Should().OnlyContain(enemy => enemy.ArchetypeSlug == "wolf" && enemy.MaxHealth == 20);
    }

    [Fact]
    public async Task SpawnRosterAsync_Does_Not_Substitute_Anything_For_An_Unknown_Archetype()
    {
        var repo = new Mock<IEnemyRepository>();
        repo.Setup(r => r.GetBySlugAsync(It.IsAny<string>())).ReturnsAsync((Enemy?)null);

        var roster = await CreateResolver(repo).SpawnRosterAsync([new ActorPoolEntry("spectre", 1)]);

        roster.Should().BeEmpty("missing content must fail loudly, not be replaced by a default archetype");
    }

    [Fact]
    public async Task ResolveArchetypeAsync_Returns_Null_For_Missing_Content()
    {
        var repo = new Mock<IEnemyRepository>();
        repo.Setup(r => r.GetBySlugAsync("spectre")).ReturnsAsync((Enemy?)null);

        var template = await CreateResolver(repo).ResolveArchetypeAsync("spectre");

        template.Should().BeNull();
    }
}
