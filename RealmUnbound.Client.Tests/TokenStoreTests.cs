using RealmUnbound.Client.Services;

namespace RealmUnbound.Client.Tests;

public class TokenStoreTests
{
    // Set
    [Fact]
    public void Set_Should_Store_All_Values()
    {
        var store = new TokenStore();
        var id    = Guid.NewGuid();

        store.Set("access-token", "refresh-token", "Alice", id);

        store.AccessToken.Should().Be("access-token");
        store.RefreshToken.Should().Be("refresh-token");
        store.Username.Should().Be("Alice");
        store.AccountId.Should().Be(id);
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

    // Clear
    [Fact]
    public void Clear_Should_Null_All_Values()
    {
        var store = new TokenStore();
        store.Set("access", "refresh", "User", Guid.NewGuid());

        store.Clear();

        store.AccessToken.Should().BeNull();
        store.RefreshToken.Should().BeNull();
        store.Username.Should().BeNull();
        store.AccountId.Should().BeNull();
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
    }
}
