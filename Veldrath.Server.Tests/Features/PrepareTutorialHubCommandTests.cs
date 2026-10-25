using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RealmEngine.Shared.Abstractions;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Quest;
using Veldrath.Server.Settings;
using Veldrath.Server.Tests.Infrastructure;
using ZoneLocationEntry = RealmEngine.Shared.Models.ZoneLocationEntry;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Tests for the A3 tutorial handoff: placement on the authored tutorial start location, the auto-accepted quest,
/// and the loud degradations when the content is missing.
/// </summary>
public class PrepareTutorialHubCommandTests : IDisposable
{
    private const string Slug = "first-steps";

    private readonly TestDbContextFactory _dbFactory = new();

    public void Dispose() => _dbFactory.Dispose();

    private static async Task<Character> SeedCharacterAsync(
        ApplicationDbContext db,
        string? attributes = null,
        string? questBlob = null)
    {
        var account = new PlayerAccount { UserName = $"u_{Guid.NewGuid():N}" };
        account.NormalizedUserName = account.UserName.ToUpperInvariant();
        db.Users.Add(account);

        var character = new Character
        {
            AccountId = account.Id,
            Name = $"Char_{Guid.NewGuid():N}",
            ClassName = "Warrior",
            SlotIndex = 1,
            Attributes = attributes ?? "{}",
            QuestBlob = questBlob ?? string.Empty,
        };
        db.Characters.Add(character);
        await db.SaveChangesAsync();
        return character;
    }

    private static ZoneLocationEntry TutorialStart() =>
        new("tutorial-glade", "The Glade", "locations", "zone-tutorial", 10, null, null, IsTutorialStart: true);

    private static PrepareTutorialHubCommandHandler MakeHandler(
        ApplicationDbContext db,
        IZoneLocationRepository zoneLocations,
        ISender? mediator = null) =>
        new(
            new CharacterRepository(db),
            zoneLocations,
            mediator ?? AcceptedQuestSender(),
            Options.Create(new TutorialOptions { TutorialQuestSlug = Slug }),
            NullLogger<PrepareTutorialHubCommandHandler>.Instance);

    private static ISender AcceptedQuestSender()
    {
        var mediator = new Mock<ISender>();
        mediator
            .Setup(sender => sender.Send(It.IsAny<AcceptQuestHubCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AcceptQuestHubResult { Success = true });
        return mediator.Object;
    }

    private static IZoneLocationRepository LocationsWith(params ZoneLocationEntry[] locations)
    {
        var zones = new Mock<IZoneLocationRepository>();
        zones.Setup(repo => repo.GetAllAsync()).ReturnsAsync([.. locations]);
        return zones.Object;
    }

    [Fact]
    public async Task Handle_Places_A_New_Character_On_The_Tutorial_Start_And_Accepts_The_Quest()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db);
        var mediator = new Mock<ISender>();
        mediator
            .Setup(sender => sender.Send(It.IsAny<AcceptQuestHubCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AcceptQuestHubResult { Success = true });

        var result = await MakeHandler(db, LocationsWith(TutorialStart()), mediator.Object)
            .Handle(new PrepareTutorialHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Placed.Should().BeTrue();
        result.ZoneId.Should().Be("zone-tutorial");
        result.LocationSlug.Should().Be("tutorial-glade");
        result.Warning.Should().BeNull();

        mediator.Verify(
            sender => sender.Send(
                It.Is<AcceptQuestHubCommand>(command => command.CharacterId == character.Id && command.Slug == Slug),
                It.IsAny<CancellationToken>()),
            Times.Once);

        var stored = await _dbFactory.CreateContext().Characters.AsNoTracking().SingleAsync(c => c.Id == character.Id);
        stored.CurrentZoneId.Should().Be("zone-tutorial", "the placement must be persisted, not just reported");
        stored.CurrentZoneLocationSlug.Should().Be("tutorial-glade");
        stored.Attributes.Should().Contain(PrepareTutorialHubCommandHandler.StartedAttributeKey);
    }

    [Fact]
    public async Task Handle_Skips_A_Character_Who_Finished_The_Tutorial()
    {
        await using var db = _dbFactory.CreateContext();
        var journal = QuestJournal.Parse(null);
        journal.Add(new CharacterQuestEntry(
            Slug, "First steps", "Welcome.", CharacterQuestEntry.StatusCompleted, 0, 0,
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            DateTimeOffset.UtcNow));
        var character = await SeedCharacterAsync(db, questBlob: QuestJournal.Serialize(journal));

        var result = await MakeHandler(db, LocationsWith(TutorialStart()))
            .Handle(new PrepareTutorialHubCommand(character.Id), CancellationToken.None);

        result.Skipped.Should().BeTrue("the tutorial is finished");
        result.Placed.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Skips_A_Character_Who_Already_Started()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(
            db, attributes: $$"""{"{{PrepareTutorialHubCommandHandler.StartedAttributeKey}}":1}""");

        var result = await MakeHandler(db, LocationsWith(TutorialStart()))
            .Handle(new PrepareTutorialHubCommand(character.Id), CancellationToken.None);

        result.Skipped.Should().BeTrue("a started tutorial must not be re-run on every login");
        result.Placed.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Warns_And_Does_Not_Move_When_No_Tutorial_Start_Is_Authored()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db);

        var result = await MakeHandler(db, LocationsWith())
            .Handle(new PrepareTutorialHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeTrue("onboarding degrades, it does not block the login");
        result.Placed.Should().BeFalse();
        result.Warning.Should().Contain("tutorial has not been set up");

        var stored = await _dbFactory.CreateContext().Characters.AsNoTracking().SingleAsync(c => c.Id == character.Id);
        stored.CurrentZoneId.Should().Be(character.CurrentZoneId, "the character keeps the normal starting point");
    }

    [Fact]
    public async Task Handle_Warns_And_Does_Not_Move_When_The_Tutorial_Quest_Is_Missing()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db);
        var mediator = new Mock<ISender>();
        mediator
            .Setup(sender => sender.Send(It.IsAny<AcceptQuestHubCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AcceptQuestHubResult { Success = false, ErrorMessage = "Quest 'first-steps' does not exist." });

        var result = await MakeHandler(db, LocationsWith(TutorialStart()), mediator.Object)
            .Handle(new PrepareTutorialHubCommand(character.Id), CancellationToken.None);

        result.Placed.Should().BeFalse("there is no point moving the player for a quest that does not exist");
        result.Warning.Should().Contain("unavailable");

        var stored = await _dbFactory.CreateContext().Characters.AsNoTracking().SingleAsync(c => c.Id == character.Id);
        stored.CurrentZoneId.Should().Be(character.CurrentZoneId, "the character keeps the normal starting point");
    }
}
