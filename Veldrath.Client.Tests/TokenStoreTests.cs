using Veldrath.Client.Services;

namespace Veldrath.Client.Tests;

public class TokenStoreTests
{
    // Set
    [Fact]
    public void Set_Should_Store_All_Values()
    {
        var store     = new TokenStore();
        var id        = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        store.Set("access-token", "refresh-token", "Alice", id,
                  DateTimeOffset.UtcNow.AddHours(1), true, ["Admin"], ["ban_players"], sessionId);

        store.AccessToken.Should().Be("access-token");
        store.RefreshToken.Should().Be("refresh-token");
        store.Username.Should().Be("Alice");
        store.AccountId.Should().Be(id);
        store.IsCurator.Should().BeTrue();
        store.Roles.Should().BeEquivalentTo(["Admin"]);
        store.Permissions.Should().BeEquivalentTo(["ban_players"]);
        store.SessionId.Should().Be(sessionId);
    }

    [Fact]
    public void Set_Should_Overwrite_Previous_Values()
    {
        var store = new TokenStore();
        store.Set("old-access", "old-refresh", "OldUser", Guid.NewGuid());

        var newId = Guid.NewGuid();
        store.Set("new-access", "new-refresh", "NewUser", newId);

        store.AccessToken.Should().Be("new-access");
        store.Username.Should().Be("NewUser");
        store.AccountId.Should().Be(newId);
    }

    [Fact]
    public void Set_Omitting_Optional_Params_Defaults_RolesAndPermissions_To_Empty()
    {
        var store = new TokenStore();
        store.Set("access", "refresh", "Bob", Guid.NewGuid());

        store.Roles.Should().BeEmpty();
        store.Permissions.Should().BeEmpty();
        store.SessionId.Should().BeNull();
    }

    // Clear
    [Fact]
    public void Clear_Should_Null_All_Values()
    {
        var store = new TokenStore();
        store.Set("access", "refresh", "User", Guid.NewGuid(),
                  sessionId: Guid.NewGuid(), roles: ["Player"], permissions: ["p:view"]);

        store.Clear();

        store.AccessToken.Should().BeNull();
        store.RefreshToken.Should().BeNull();
        store.Username.Should().BeNull();
        store.AccountId.Should().BeNull();
        store.Roles.Should().BeEmpty();
        store.Permissions.Should().BeEmpty();
        store.SessionId.Should().BeNull();
    }

    // IsAuthenticated
    [Fact]
    public void IsAuthenticated_Should_Be_False_Initially()
    {
        var store = new TokenStore();
        store.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public void IsAuthenticated_Should_Be_True_After_Set()
    {
        var store = new TokenStore();
        store.Set("access", "refresh", "User", Guid.NewGuid());
        store.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public void IsAuthenticated_Should_Be_False_After_Clear()
    {
        var store = new TokenStore();
        store.Set("access", "refresh", "User", Guid.NewGuid());
        store.Clear();
        store.IsAuthenticated.Should().BeFalse();
    }

    // Default state
    [Fact]
    public void All_Fields_Should_Be_Null_Initially()
    {
        var store = new TokenStore();

        store.AccessToken.Should().BeNull();
        store.RefreshToken.Should().BeNull();
        store.Username.Should().BeNull();
        store.AccountId.Should().BeNull();
        store.Roles.Should().BeEmpty();
        store.Permissions.Should().BeEmpty();
        store.SessionId.Should().BeNull();
    }
}
