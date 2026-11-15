using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Server.Features.Characters.Queries;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Tests for <see cref="GetCharacterSkillsQueryHandler"/>, the step that turns the
/// <c>Skill_{id}_XP</c> keys in the attributes blob into something the skill sheet can draw.
/// </summary>
public class GetCharacterSkillsQueryTests
{
    private static SkillDefinition Swordsmanship() => new()
    {
        SkillId = "swordsmanship",
        Name = "swordsmanship",
        DisplayName = "Swordsmanship",
        Description = "Skill with a blade.",
        Category = "combat",
        GoverningAttribute = "strength",
        MaxRank = 50,
    };

    private static SkillDefinition Herbalism() => new()
    {
        SkillId = "herbalism",
        Name = "herbalism",
        DisplayName = "Herbalism",
        Description = "Finding and using plants.",
        Category = "profession",
        GoverningAttribute = "wisdom",
        MaxRank = 30,
    };

    private static GetCharacterSkillsQueryHandler Handler(FakeSkillRepository repo) =>
        new(repo, NullLogger<GetCharacterSkillsQueryHandler>.Instance);

    private const string Blob =
        """{"Strength":10,"Skill_swordsmanship_XP":250,"Skill_swordsmanship_Rank":2,"Skill_herbalism_XP":40,"Gold":100}""";

    [Fact]
    public async Task Resolves_each_practised_skill_with_its_rank_and_progress()
    {
        var handler = Handler(new FakeSkillRepository(Swordsmanship(), Herbalism()));

        var skills = await handler.Handle(new GetCharacterSkillsQuery(Blob), CancellationToken.None);

        skills.Should().HaveCount(2);

        var swordsmanship = skills.Single(skill => skill.SkillId == "swordsmanship");
        swordsmanship.Resolved.Should().BeTrue();
        swordsmanship.Name.Should().Be("Swordsmanship");
        swordsmanship.Category.Should().Be("combat");
        swordsmanship.GoverningAttribute.Should().Be("strength");
        swordsmanship.Rank.Should().Be(2);
        swordsmanship.TotalXp.Should().Be(250);
        swordsmanship.XpIntoRank.Should().Be(50);
        swordsmanship.XpToNextRank.Should().Be(100);
        swordsmanship.MaxRank.Should().Be(50);
    }

    [Fact]
    public async Task Ignores_attributes_that_are_not_skills()
    {
        var handler = Handler(new FakeSkillRepository(Swordsmanship(), Herbalism()));

        var skills = await handler.Handle(new GetCharacterSkillsQuery(Blob), CancellationToken.None);

        skills.Select(skill => skill.SkillId).Should().BeEquivalentTo(["swordsmanship", "herbalism"]);
    }

    [Fact]
    public async Task Orders_by_rank_then_name()
    {
        var handler = Handler(new FakeSkillRepository(Swordsmanship(), Herbalism()));

        var skills = await handler.Handle(new GetCharacterSkillsQuery(Blob), CancellationToken.None);

        skills.Select(skill => skill.SkillId).Should().Equal("swordsmanship", "herbalism");
    }

    [Fact]
    public async Task Treats_a_rank_key_without_xp_as_practised_at_zero()
    {
        var handler = Handler(new FakeSkillRepository(Herbalism()));

        var skills = await handler.Handle(
            new GetCharacterSkillsQuery("""{"Skill_herbalism_Rank":1}"""),
            CancellationToken.None);

        var herbalism = skills.Should().ContainSingle().Subject;
        herbalism.TotalXp.Should().Be(0);
        herbalism.Rank.Should().Be(0);
        herbalism.XpIntoRank.Should().Be(0);
    }

    [Fact]
    public async Task Returns_an_unresolved_entry_when_the_catalog_has_no_row()
    {
        var handler = Handler(new FakeSkillRepository(Swordsmanship()));

        var skills = await handler.Handle(new GetCharacterSkillsQuery(Blob), CancellationToken.None);

        var missing = skills.Single(skill => skill.SkillId == "herbalism");
        missing.Resolved.Should().BeFalse();
        missing.Name.Should().BeNull();
        missing.TotalXp.Should().Be(40);
        missing.MaxRank.Should().Be(0);
    }

    [Fact]
    public async Task Falls_back_to_the_internal_name_when_a_definition_has_no_display_name()
    {
        var nameless = Swordsmanship();
        nameless.DisplayName = "  ";
        var handler = Handler(new FakeSkillRepository(nameless));

        var skills = await handler.Handle(new GetCharacterSkillsQuery(Blob), CancellationToken.None);

        skills.Single(skill => skill.SkillId == "swordsmanship").Name.Should().Be("swordsmanship");
    }

    [Fact]
    public async Task Returns_nothing_for_a_character_that_has_practised_nothing()
    {
        var repo = new FakeSkillRepository(Swordsmanship());
        var handler = Handler(repo);

        (await handler.Handle(new GetCharacterSkillsQuery("""{"Strength":10}"""), CancellationToken.None))
            .Should().BeEmpty();
        (await handler.Handle(new GetCharacterSkillsQuery("{}"), CancellationToken.None)).Should().BeEmpty();
        (await handler.Handle(new GetCharacterSkillsQuery(null), CancellationToken.None)).Should().BeEmpty();
        repo.LookupCount.Should().Be(0);
    }

    [Fact]
    public async Task A_malformed_blob_is_treated_as_no_skills_instead_of_throwing()
    {
        var handler = Handler(new FakeSkillRepository(Swordsmanship()));

        var skills = await handler.Handle(new GetCharacterSkillsQuery("{not json"), CancellationToken.None);

        skills.Should().BeEmpty();
    }

    [Fact]
    public async Task A_catalog_failure_marks_the_skill_unresolved_rather_than_throwing()
    {
        var handler = Handler(new FakeSkillRepository(Swordsmanship()) { ThrowOnLookup = true });

        var skills = await handler.Handle(new GetCharacterSkillsQuery(Blob), CancellationToken.None);

        skills.Should().HaveCount(2);
        skills.Should().OnlyContain(skill => skill.Resolved == false);
    }
}
