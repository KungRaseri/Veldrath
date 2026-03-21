using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Repositories;

namespace RealmEngine.Data.Tests.Repositories;

[Trait("Category", "Repository")]
public class EfCoreDialogueRepositoryTests
{
    private static ContentDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Dialogue MakeDialogue(string slug, string? speaker = null, bool active = true) =>
        new() { Slug = slug, Speaker = speaker, IsActive = active, DisplayName = slug };

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyActiveDialogues()
    {
        await using var db = CreateDbContext();
        db.Dialogues.AddRange(
            MakeDialogue("greeting-01", active: true),
            MakeDialogue("deleted-line", active: false));
        await db.SaveChangesAsync();
        var repo = new EfCoreDialogueRepository(db, NullLogger<EfCoreDialogueRepository>.Instance);

        var result = await repo.GetAllAsync();

        result.Should().HaveCount(1).And.Contain(d => d.Slug == "greeting-01");
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsEntry_WhenActive()
    {
        await using var db = CreateDbContext();
        db.Dialogues.Add(MakeDialogue("merchant-intro", "Merchant"));
        await db.SaveChangesAsync();
        var repo = new EfCoreDialogueRepository(db, NullLogger<EfCoreDialogueRepository>.Instance);

        var result = await repo.GetBySlugAsync("merchant-intro");

        result.Should().NotBeNull();
        result!.Slug.Should().Be("merchant-intro");
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsNull_WhenInactive()
    {
        await using var db = CreateDbContext();
        db.Dialogues.Add(MakeDialogue("cut-scene", active: false));
        await db.SaveChangesAsync();
        var repo = new EfCoreDialogueRepository(db, NullLogger<EfCoreDialogueRepository>.Instance);

        (await repo.GetBySlugAsync("cut-scene")).Should().BeNull();
    }

    [Fact]
    public async Task GetBySpeakerAsync_FiltersOnSpeaker()
    {
        await using var db = CreateDbContext();
        db.Dialogues.AddRange(
            MakeDialogue("elder-greeting", "Elder"),
            MakeDialogue("guard-bark", "Guard"),
            MakeDialogue("elder-farewell", "Elder"));
        await db.SaveChangesAsync();
        var repo = new EfCoreDialogueRepository(db, NullLogger<EfCoreDialogueRepository>.Instance);

        var result = await repo.GetBySpeakerAsync("Elder");

        result.Should().HaveCount(2).And.OnlyContain(d => d.Speaker == "Elder");
    }
}
