using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Server.Features.Characters.Queries;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Tests for <see cref="GetLearnedPowersQueryHandler"/>, the step that turns a character's learned
/// slugs into something the game client can render without printing an identifier.
/// </summary>
public class GetLearnedPowersQueryTests
{
    private static Power Slash() => new()
    {
        Slug = "power-slash",
        Name = "power_slash",
        DisplayName = "Slash",
        Description = "A sweeping cut that opens a wound.",
        School = null,
        Type = PowerType.Talent,
        EffectType = PowerEffectType.Damage,
        ManaCost = 0,
        Cooldown = 1,
        BaseDamage = "1d8+STR",
        DamageType = "physical",
        Tier = 1,
        RequiredLevel = 1,
    };

    private static Power Fireball() => new()
    {
        Slug = "fireball",
        Name = "fireball",
        DisplayName = "Fireball",
        Description = "Hurls a bead of fire that bursts on impact.",
        School = "fire",
        Type = PowerType.Spell,
        EffectType = PowerEffectType.Damage,
        ManaCost = 12,
        Cooldown = 3,
        BaseDamage = "8d6",
        DamageType = "fire",
        Range = 30,
        MaxTargets = 4,
        Tier = 3,
        RequiredLevel = 5,
    };

    private static GetLearnedPowersQueryHandler Handler(FakePowerRepository repo) =>
        new(repo, NullLogger<GetLearnedPowersQueryHandler>.Instance);

    [Fact]
    public async Task Resolves_every_slug_in_the_order_it_was_learned()
    {
        var handler = Handler(new FakePowerRepository(Slash(), Fireball()));

        var result = await handler.Handle(
            new GetLearnedPowersQuery(["fireball", "power-slash"]),
            CancellationToken.None);

        result.Select(power => power.Slug).Should().Equal("fireball", "power-slash");
        result.Should().OnlyContain(power => power.Resolved);
    }

    [Fact]
    public async Task Copies_the_catalog_fields_the_client_renders()
    {
        var handler = Handler(new FakePowerRepository(Fireball()));

        var powers = await handler.Handle(new GetLearnedPowersQuery(["fireball"]), CancellationToken.None);

        var fireball = powers.Should().ContainSingle().Subject;
        fireball.DisplayName.Should().Be("Fireball");
        fireball.Description.Should().Contain("bead of fire");
        fireball.School.Should().Be("fire");
        fireball.PowerType.Should().Be("Spell");
        fireball.EffectType.Should().Be("Damage");
        fireball.ManaCost.Should().Be(12);
        fireball.Cooldown.Should().Be(3);
        fireball.BaseDamage.Should().Be("8d6");
        fireball.DamageType.Should().Be("fire");
        fireball.Range.Should().Be(30);
        fireball.MaxTargets.Should().Be(4);
        fireball.Tier.Should().Be(3);
        fireball.RequiredLevel.Should().Be(5);
    }

    [Fact]
    public async Task Falls_back_to_the_internal_name_when_a_power_has_no_display_name()
    {
        var nameless = Slash();
        nameless.DisplayName = "  ";
        var handler = Handler(new FakePowerRepository(nameless));

        var powers = await handler.Handle(new GetLearnedPowersQuery(["power-slash"]), CancellationToken.None);

        powers.Should().ContainSingle().Which.DisplayName.Should().Be("power_slash");
    }

    [Fact]
    public async Task Returns_a_slug_with_no_catalog_entry_as_unresolved_instead_of_dropping_it()
    {
        var handler = Handler(new FakePowerRepository(Slash()));

        var powers = await handler.Handle(
            new GetLearnedPowersQuery(["power-slash", "power-deleted"]),
            CancellationToken.None);

        powers.Should().HaveCount(2);
        var missing = powers[1];
        missing.Slug.Should().Be("power-deleted");
        missing.Resolved.Should().BeFalse();
        missing.DisplayName.Should().BeNull();
        missing.ManaCost.Should().Be(0);
    }

    [Fact]
    public async Task Ignores_blanks_and_collapses_duplicates()
    {
        var handler = Handler(new FakePowerRepository(Slash()));

        var powers = await handler.Handle(
            new GetLearnedPowersQuery(["power-slash", "  ", "", "POWER-SLASH", "power-slash"]),
            CancellationToken.None);

        powers.Should().ContainSingle().Which.Slug.Should().Be("power-slash");
    }

    [Fact]
    public async Task Trims_surrounding_whitespace_before_looking_up()
    {
        var handler = Handler(new FakePowerRepository(Slash()));

        var powers = await handler.Handle(new GetLearnedPowersQuery(["  power-slash  "]), CancellationToken.None);

        powers.Should().ContainSingle().Which.Slug.Should().Be("power-slash");
        powers[0].Resolved.Should().BeTrue();
    }

    [Fact]
    public async Task Returns_nothing_for_a_character_that_knows_nothing()
    {
        var repo = new FakePowerRepository();
        var handler = Handler(repo);

        var powers = await handler.Handle(new GetLearnedPowersQuery([]), CancellationToken.None);

        powers.Should().BeEmpty();
        repo.LookupCount.Should().Be(0);
    }

    [Fact]
    public async Task A_catalog_failure_marks_the_power_unresolved_rather_than_throwing()
    {
        var handler = Handler(new FakePowerRepository(Slash()) { ThrowOnLookup = true });

        var powers = await handler.Handle(new GetLearnedPowersQuery(["power-slash"]), CancellationToken.None);

        powers.Should().ContainSingle().Which.Resolved.Should().BeFalse();
    }
}
