using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Data;

public class PlayerAccountRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task CreateAsync_Should_Persist_Player()
    {
        await using var db = _factory.CreateContext();
        var repo = new PlayerAccountRepository(db);

        var account = new PlayerAccount { UserName = "Aragorn", NormalizedUserName = "ARAGORN" };
        await repo.CreateAsync(account);

        var found = await repo.FindByUsernameAsync("Aragorn");
        found.Should().NotBeNull();
        found!.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task FindByUsernameAsync_Should_Return_Null_When_Not_Found()
    {
        await using var db = _factory.CreateContext();
        var repo = new PlayerAccountRepository(db);

        var result = await repo.FindByUsernameAsync("Legolas");
        result.Should().BeNull();
    }

    [Fact]
    public async Task ExistsAsync_Should_Return_True_For_Existing_Username()
    {
        await using var db = _factory.CreateContext();
        var repo = new PlayerAccountRepository(db);

        await repo.CreateAsync(new PlayerAccount { UserName = "Gandalf", NormalizedUserName = "GANDALF" });

        var exists = await repo.ExistsAsync("Gandalf");
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_Should_Return_False_For_Unknown_Username()
    {
        await using var db = _factory.CreateContext();
        var repo = new PlayerAccountRepository(db);

        var exists = await repo.ExistsAsync("Saruman");
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_Should_Persist_Changes()
    {
        await using var db = _factory.CreateContext();
        var repo = new PlayerAccountRepository(db);

        var account = await repo.CreateAsync(new PlayerAccount { UserName = "Frodo", NormalizedUserName = "FRODO" });

        account.LastSeenAt = DateTimeOffset.UtcNow;
        await repo.UpdateAsync(account);

        var updated = await repo.FindByIdAsync(account.Id);
        updated!.LastSeenAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Username_Should_Be_Unique()
    {
        await using var db = _factory.CreateContext();
        var repo = new PlayerAccountRepository(db);

        await repo.CreateAsync(new PlayerAccount { UserName = "Bilbo", NormalizedUserName = "BILBO" });

        var act = async () => await repo.CreateAsync(new PlayerAccount { UserName = "Bilbo", NormalizedUserName = "BILBO" });
        await act.Should().ThrowAsync<Exception>();
    }
}
