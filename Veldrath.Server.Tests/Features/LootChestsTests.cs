using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RealmEngine.Core.Services;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Data.Seeders;
using Veldrath.Server.Features.Characters;
using Veldrath.Server.Features.Characters.Chests;
using Veldrath.Server.Settings;
using Veldrath.Server.Tests.Infrastructure;
using Character = Veldrath.Server.Data.Entities.Character;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Covers the authored chest seed and the open path: the loot lands in the character's bag, the chest is claimed
/// so only one caller can win it, and a chest is only openable where the character is standing.
/// </summary>
public class LootChestsTests : IDisposable
{
    private const string ForestChestLootRef = "@loot-tables/chests/forest:forest-chest-loot";

    private readonly TestDbContextFactory _factory = new();

    /// <inheritdoc />
    public void Dispose() => _factory.Dispose();

    // ── Seeder ───────────────────────────────────────────────────────────────

    [Fact]
    public void The_Authored_Chests_Are_Unique_In_One_Zone_And_Name_A_Seeded_Table()
    {
        var authored = LootChestsSeeder.Authors();

        authored.Select(chest => chest.ChestId).Should().OnlyHaveUniqueItems();
        authored.Should().OnlyContain(chest => chest.Kind == LootChestKind.Authored);
        authored.Should().OnlyContain(chest => chest.ZoneId == "ashlen-wood");
        authored.Should().OnlyContain(chest => chest.LootTableRefs.Contains("forest-chest-loot"));
        authored.Should().Contain(chest => chest.LocationSlug == "old-clearing");
    }

    [Fact]
    public async Task Seeding_Is_Idempotent_And_Keeps_An_Opened_Chest_Opened()
    {
        await using var db = _factory.CreateContext();

        await LootChestsSeeder.SeedAsync(db);
        await LootChestsSeeder.SeedAsync(db);

        db.LootChests.Count().Should().Be(LootChestsSeeder.Authors().Length, "a restart never duplicates a chest");

        var chest = await db.LootChests.FirstAsync();
        chest.IsOpened = true;
        chest.ReopenAt = DateTimeOffset.UtcNow.AddHours(1);
        await db.SaveChangesAsync();

        await LootChestsSeeder.SeedAsync(db);

        (await db.LootChests.AsNoTracking().FirstAsync(row => row.Id == chest.Id)).IsOpened
            .Should().BeTrue("live state belongs to the world, not to the seed");
    }

    // ── Open path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Opening_A_Chest_Stores_The_Loot_And_Sets_Its_Reopen_Timer()
    {
        await using var db = _factory.CreateContext();
        var (character, _) = await SeedStandingAsync(db, "ashlen-wood", "old-clearing");
        var chest = await SeedChestAsync(db, "ashlen-wood", "old-clearing");

        var result = await Handler(db).Handle(new OpenChestHubCommand(character.Id, chest.Id), CancellationToken.None);

        result.Success.Should().BeTrue(result.ErrorMessage);
        result.Items.Should().ContainSingle().Which.ItemRef.Should().Be("@items/general:health-potion");
        result.Message.Should().Contain("Mossy Strongbox");

        (await db.Characters.AsNoTracking().FirstAsync(row => row.Id == character.Id)).InventoryBlob
            .Should().Contain("health-potion", "the bag the client reads is InventoryBlob");

        var stored = await db.LootChests.AsNoTracking().FirstAsync(row => row.Id == chest.Id);
        stored.IsOpened.Should().BeTrue();
        stored.OpenedAt.Should().NotBeNull();
        stored.ReopenAt.Should().NotBeNull();
        stored.OpenedByCharacterId.Should().Be(character.Id);
    }

    [Fact]
    public async Task A_Chest_That_Was_Already_Opened_Refuses_And_Loses_Its_Race()
    {
        await using var db = _factory.CreateContext();
        var (character, _) = await SeedStandingAsync(db, "ashlen-wood", "old-clearing");
        var chest = await SeedChestAsync(db, "ashlen-wood", "old-clearing");
        var handler = Handler(db);

        await handler.Handle(new OpenChestHubCommand(character.Id, chest.Id), CancellationToken.None);
        var second = await handler.Handle(new OpenChestHubCommand(character.Id, chest.Id), CancellationToken.None);

        second.Success.Should().BeFalse();
        second.ErrorMessage.Should().Contain("already been emptied");
    }

    [Fact]
    public async Task A_Chest_At_Another_Location_Is_Refused()
    {
        await using var db = _factory.CreateContext();
        var (character, _) = await SeedStandingAsync(db, "ashlen-wood", "old-clearing");
        var chest = await SeedChestAsync(db, "ashlen-wood", "wood-road");

        var result = await Handler(db).Handle(new OpenChestHubCommand(character.Id, chest.Id), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not standing where");
        (await db.LootChests.AsNoTracking().FirstAsync(row => row.Id == chest.Id)).IsOpened.Should().BeFalse();
    }

    [Fact]
    public async Task A_Chest_From_Another_Run_Is_Refused()
    {
        await using var db = _factory.CreateContext();
        var (character, _) = await SeedStandingAsync(db, "ashlen-wood", "old-clearing");

        // The chest belongs to a delve run; the character is standing in the shared zone, so it is not theirs.
        var run = new ZoneInstance
        {
            ZoneId = "ashlen-wood",
            CreatedByCharacterId = character.Id,
            ZoneGroup = $"zone:ashlen-wood#i1:{Guid.NewGuid():D}",
        };
        db.ZoneInstances.Add(run);
        await db.SaveChangesAsync();

        var chest = await SeedChestAsync(db, "ashlen-wood", "old-clearing", zoneInstanceId: run.Id);

        var result = await Handler(db).Handle(new OpenChestHubCommand(character.Id, chest.Id), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("another run");
    }

    // ── Refusals and edge cases ──────────────────────────────────────────────

    [Fact]
    public async Task Opening_Without_A_Character_Refuses()
    {
        await using var db = _factory.CreateContext();

        var result = await Handler(db).Handle(
            new OpenChestHubCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("No character to open a chest with.");
    }

    [Fact]
    public async Task Opening_A_Chest_That_Is_No_Longer_There_Refuses()
    {
        await using var db = _factory.CreateContext();
        var (character, _) = await SeedStandingAsync(db, "ashlen-wood", "old-clearing");

        var result = await Handler(db).Handle(
            new OpenChestHubCommand(character.Id, Guid.NewGuid()), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("That chest is no longer there.");
    }

    [Fact]
    public async Task Opening_A_Chest_From_Another_Zone_Is_Refused()
    {
        await using var db = _factory.CreateContext();
        var (character, _) = await SeedStandingAsync(db, "ashlen-wood", "old-clearing");
        var chest = await SeedChestAsync(db, "the-droveway", "old-clearing");

        var result = await Handler(db).Handle(new OpenChestHubCommand(character.Id, chest.Id), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not standing where");
        (await db.LootChests.AsNoTracking().FirstAsync(row => row.Id == chest.Id)).IsOpened.Should().BeFalse();
    }

    [Fact]
    public async Task Opening_A_Chest_With_No_Loot_Tables_Refuses()
    {
        await using var db = _factory.CreateContext();
        var (character, _) = await SeedStandingAsync(db, "ashlen-wood", "old-clearing");
        var chest = await SeedChestAsync(db, "ashlen-wood", "old-clearing", lootTableRefs: string.Empty);

        var result = await Handler(db).Handle(new OpenChestHubCommand(character.Id, chest.Id), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("The Mossy Strongbox is empty.");
        (await db.LootChests.AsNoTracking().FirstAsync(row => row.Id == chest.Id)).IsOpened
            .Should().BeFalse("a chest nobody can loot must not be burned");
    }

    [Fact]
    public async Task Opening_A_Chest_With_Unreadable_Loot_Tables_Refuses()
    {
        await using var db = _factory.CreateContext();
        var (character, _) = await SeedStandingAsync(db, "ashlen-wood", "old-clearing");
        var chest = await SeedChestAsync(db, "ashlen-wood", "old-clearing", lootTableRefs: "{not json");

        var result = await Handler(db).Handle(new OpenChestHubCommand(character.Id, chest.Id), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("The Mossy Strongbox is empty.");
    }

    [Fact]
    public async Task Opening_A_Chest_With_No_Rolls_Opens_It_And_Says_It_Was_Empty()
    {
        await using var db = _factory.CreateContext();
        var (character, _) = await SeedStandingAsync(db, "ashlen-wood", "old-clearing");
        var chest = await SeedChestAsync(db, "ashlen-wood", "old-clearing");
        var table = new LootTableData
        {
            Slug = "forest-chest-loot",
            Name = "Forest Chest Loot",
            Context = "chests",
            IsChest = true,
            Entries = [],
        };

        var result = await Handler(db, new FixedLootTables(table)).Handle(
            new OpenChestHubCommand(character.Id, chest.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Items.Should().BeEmpty();
        result.Gold.Should().Be(0);
        result.Message.Should().Be("The Mossy Strongbox was empty.");
    }

    [Fact]
    public async Task Opening_A_Chest_With_Gold_Pays_It_Through_AddGold_And_Counts_The_Stacks()
    {
        await using var db = _factory.CreateContext();
        var (character, _) = await SeedStandingAsync(db, "ashlen-wood", "old-clearing");
        var chest = await SeedChestAsync(db, "ashlen-wood", "old-clearing");

        var table = new LootTableData
        {
            Slug = "forest-chest-loot",
            Name = "Forest Chest Loot",
            Context = "chests",
            IsChest = true,
            Entries =
            [
                new LootTableEntryData
                {
                    ItemDomain = "items/general",
                    ItemSlug = "health-potion",
                    DropWeight = 100,
                    QuantityMin = 2,
                    QuantityMax = 2,
                    IsGuaranteed = true,
                },
                new LootTableEntryData
                {
                    ItemDomain = "gold",
                    ItemSlug = "gold",
                    DropWeight = 100,
                    QuantityMin = 5,
                    QuantityMax = 5,
                    IsGuaranteed = true,
                },
            ],
        };

        var sender = new Mock<ISender>();
        sender.Returns(new AddGoldHubResult { Success = true, GoldAdded = 5, NewGoldTotal = 47 });

        var result = await Handler(db, new FixedLootTables(table), sender).Handle(
            new OpenChestHubCommand(character.Id, chest.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Gold.Should().Be(5);
        result.NewGoldTotal.Should().Be(47,
            "the hub announces the award from the writer's own total, not by re-adding the delta");
        result.Items.Should().ContainSingle()
            .Which.Should().Be(new ChestLootItem("@items/general:health-potion", "Health Potion", 2));
        result.Message.Should().Be("Opened the Mossy Strongbox: Health Potion x2 and 5 gold.");

        sender.Verify(
            s => s.Send(
                It.Is<AddGoldHubCommand>(command =>
                    command.CharacterId == character.Id &&
                    command.Amount == 5 &&
                    command.Source == $"Chest:{chest.ChestId}"),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "gold lives in the attributes blob and has exactly one writer");
    }

    [Fact]
    public async Task Opening_A_Chest_Reports_No_New_Total_When_The_Gold_Award_Failed()
    {
        await using var db = _factory.CreateContext();
        var (character, _) = await SeedStandingAsync(db, "ashlen-wood", "old-clearing");
        var chest = await SeedChestAsync(db, "ashlen-wood", "old-clearing");
        var table = new LootTableData
        {
            Slug = "forest-chest-gold-only",
            Name = "Forest Chest Loot",
            Context = "chests",
            IsChest = true,
            Entries =
            [
                new LootTableEntryData
                {
                    ItemDomain = "gold",
                    ItemSlug = "gold",
                    DropWeight = 100,
                    QuantityMin = 5,
                    QuantityMax = 5,
                    IsGuaranteed = true,
                },
            ],
        };

        var sender = new Mock<ISender>();
        sender.Returns(new AddGoldHubResult { Success = false, ErrorMessage = "Character not found" });

        var result = await Handler(db, new FixedLootTables(table), sender).Handle(
            new OpenChestHubCommand(character.Id, chest.Id), CancellationToken.None);

        result.Success.Should().BeTrue("the chest itself was claimed and emptied");
        result.NewGoldTotal.Should().BeNull(
            "there is no authoritative total to announce when the award failed");
    }

    [Fact]
    public async Task Opening_A_Chest_Reports_The_Chests_Still_Standing_Here()
    {
        await using var db = _factory.CreateContext();
        var (character, _) = await SeedStandingAsync(db, "ashlen-wood", "old-clearing");
        var chest = await SeedChestAsync(db, "ashlen-wood", "old-clearing");
        var neighbour = await SeedChestAsync(db, "ashlen-wood", "old-clearing", displayName: "Ironbound Coffer");
        await SeedChestAsync(db, "ashlen-wood", "wood-road", displayName: "Road Strongbox");

        var result = await Handler(db).Handle(new OpenChestHubCommand(character.Id, chest.Id), CancellationToken.None);

        result.Success.Should().BeTrue(result.ErrorMessage);
        result.RemainingChests.Should().ContainSingle().Which.Id.Should().Be(neighbour.Id);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static OpenChestHubCommandHandler Handler(
        ApplicationDbContext db,
        ILootTableRepository? tables = null,
        Mock<ISender>? sender = null)
    {
        var lootTables = new LootTableService(
            NullLogger<LootTableService>.Instance,
            tables ?? new ChestLootTables(),
            new Mock<IDbContextFactory<ContentDbContext>>().Object,
            seed: 11);

        return new OpenChestHubCommandHandler(
            new CharacterRepository(db),
            new PlayerSessionRepository(db),
            db,
            lootTables,
            (sender ?? new Mock<ISender>()).Object,
            Options.Create(new ChestOptions()),
            NullLogger<OpenChestHubCommandHandler>.Instance);
    }

    private static async Task<(Character Character, PlayerSession Session)> SeedStandingAsync(
        ApplicationDbContext db,
        string zoneId,
        string locationSlug)
    {
        var account = await HubHarness.SeedAccountAsync(db);
        var character = await HubHarness.SeedCharacterAsync(db, account.Id);
        character.CurrentZoneId = zoneId;
        character.CurrentZoneLocationSlug = locationSlug;
        await db.SaveChangesAsync();

        var session = await HubHarness.SeedSessionAsync(db, character, "conn-chest", zoneId);
        return (character, session);
    }

    private static async Task<LootChest> SeedChestAsync(
        ApplicationDbContext db,
        string zoneId,
        string locationSlug,
        Guid? zoneInstanceId = null,
        string displayName = "Mossy Strongbox",
        string lootTableRefs = $"[\"{ForestChestLootRef}\"]")
    {
        var chest = new LootChest
        {
            ChestId = $"test-{Guid.NewGuid():N}",
            Kind = LootChestKind.Authored,
            ZoneId = zoneId,
            LocationSlug = locationSlug,
            ZoneInstanceId = zoneInstanceId,
            DisplayName = displayName,
            LootTableRefs = lootTableRefs,
            SpawnedAt = DateTimeOffset.UtcNow,
        };

        db.LootChests.Add(chest);
        await db.SaveChangesAsync();
        return chest;
    }

    /// <summary>A chest table set supplied by the test, used for the gold and empty-roll paths.</summary>
    private sealed class FixedLootTables(params LootTableData[] tables) : ILootTableRepository
    {
        private readonly List<LootTableData> _tables = [.. tables];

        public Task<List<LootTableData>> GetAllAsync() => Task.FromResult(_tables.ToList());

        public Task<LootTableData?> GetBySlugAsync(string slug) =>
            Task.FromResult(_tables.FirstOrDefault(table => table.Slug == slug));

        public Task<List<LootTableData>> GetByContextAsync(string context) =>
            Task.FromResult(_tables.Where(table => table.Context == context).ToList());
    }

    /// <summary>A chest table whose one guaranteed entry is a potion, so a roll is deterministic.</summary>
    private sealed class ChestLootTables : ILootTableRepository
    {
        private static readonly LootTableData Table = new()
        {
            Slug = "forest-chest-loot",
            Name = "Forest Chest Loot",
            Context = "chests",
            RarityWeight = 50,
            IsChest = true,
            Entries =
            [
                new LootTableEntryData
                {
                    ItemDomain = "items/general",
                    ItemSlug = "health-potion",
                    DropWeight = 100,
                    QuantityMin = 1,
                    QuantityMax = 2,
                    IsGuaranteed = true,
                },
            ],
        };

        public Task<List<LootTableData>> GetAllAsync() => Task.FromResult(new List<LootTableData> { Table });

        public Task<LootTableData?> GetBySlugAsync(string slug) =>
            Task.FromResult(slug == Table.Slug ? Table : null);

        public Task<List<LootTableData>> GetByContextAsync(string context) =>
            Task.FromResult(context == Table.Context ? new List<LootTableData> { Table } : []);
    }
}
