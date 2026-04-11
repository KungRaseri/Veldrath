using Microsoft.Extensions.Logging.Abstractions;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Characters.Combat;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>Unit tests for <see cref="RespawnHubCommandHandler"/>.</summary>
public class RespawnHubCommandHandlerTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory = new();

    private RespawnHubCommandHandler MakeHandler(ApplicationDbContext db) =>
        new(new CharacterRepository(db), NullLogger<RespawnHubCommandHandler>.Instance);

    public void Dispose() => _dbFactory.Dispose();

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static async Task<Character> SeedCharacterAsync(
        ApplicationDbContext db,
        string?           attrsJson  = null,
        DateTimeOffset?   deletedAt  = null)
    {
        var account = new PlayerAccount { UserName = $"u_{Guid.NewGuid():N}" };
        account.NormalizedUserName = account.UserName.ToUpperInvariant();
        db.Users.Add(account);
        var character = new Character
        {
            AccountId  = account.Id,
            Name       = $"Char_{Guid.NewGuid():N}",
            ClassName  = "Warrior",
            SlotIndex  = 1,
            Attributes = attrsJson ?? "{}",
            DeletedAt  = deletedAt,
        };
        db.Characters.Add(character);
        await db.SaveChangesAsync();
        return character;
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Returns_Error_When_Character_Not_Found()
    {
        await using var db = _dbFactory.CreateContext();

        var result = await MakeHandler(db).Handle(new RespawnHubCommand(Guid.NewGuid()), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_Returns_Not_Found_For_Hardcore_Character_After_Death()
    {
        // After a HC character is killed, their record is soft-deleted.
        // GetByIdAsync filters deleted records, so respawn receives "not found".
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db, deletedAt: DateTimeOffset.UtcNow);

        var result = await MakeHandler(db).Handle(new RespawnHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_Returns_Error_When_Character_Is_Alive()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db, attrsJson: """{"CurrentHealth":50,"MaxHealth":100}""");

        var result = await MakeHandler(db).Handle(new RespawnHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not dead");
    }

    [Fact]
    public async Task Handle_Respawns_Dead_Character_With_Partial_Health()
    {
        await using var db = _dbFactory.CreateContext();
        // No Attributes → CurrentHealth defaults to 0 (dead), MaxHealth = Level * 10 = 10.
        var character = await SeedCharacterAsync(db);

        var result = await MakeHandler(db).Handle(new RespawnHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.CurrentHealth.Should().BeGreaterThanOrEqualTo(1);
        // 25% of 10 = 2 (max(1, 10/4) = 2)
        result.CurrentHealth.Should().BeLessThanOrEqualTo(10);
    }

    [Fact]
    public async Task Handle_Respawn_Removes_Combat_Session()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db);
        // Fake an active combat session for this character.
        CombatSessionStore.Set(
            character.Id,
            new ActiveCombatSession("zone", "loc", Guid.NewGuid(), false, 0, DateTimeOffset.UtcNow));

        await MakeHandler(db).Handle(new RespawnHubCommand(character.Id), CancellationToken.None);

        CombatSessionStore.IsInCombat(character.Id).Should().BeFalse();
    }
}
