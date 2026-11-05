using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Features.Socketing;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Socketing;

/// <summary>
/// Behavioural tests for <see cref="SocketService"/> — the validation guards (locked, filled, type
/// mismatch), the socket/unsocket transitions and their side effects on <see cref="Socket.Content"/>,
/// plus the link-bonus table.
/// </summary>
[Trait("Category", "Feature")]
public class SocketServiceOperationTests
{
    private static SocketService CreateService() => new(NullLogger<SocketService>.Instance);

    private static Gem MakeGem(string name = "Ruby") =>
        new() { Id = "gem-1", Name = name, SocketType = SocketType.Gem };

    private static Rune MakeRune(string name = "Shael") =>
        new() { Id = "rune-1", Name = name, SocketType = SocketType.Rune };

    // ── ValidateSocketing ─────────────────────────────────────────────────────

    [Fact]
    public void ValidateSocketing_Rejects_A_Locked_Socket()
    {
        var socket = new Socket { Type = SocketType.Gem, IsLocked = true };

        var result = CreateService().ValidateSocketing(socket, MakeGem());

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("locked");
    }

    [Fact]
    public void ValidateSocketing_Rejects_An_Already_Filled_Socket()
    {
        var socket = new Socket { Type = SocketType.Gem, Content = MakeGem("Sapphire") };

        var result = CreateService().ValidateSocketing(socket, MakeGem());

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("already filled");
    }

    [Fact]
    public void ValidateSocketing_Rejects_A_Socket_Type_Mismatch()
    {
        var socket = new Socket { Type = SocketType.Gem };

        var result = CreateService().ValidateSocketing(socket, MakeRune());

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Gem").And.Contain("Rune");
    }

    [Fact]
    public void ValidateSocketing_Prefers_The_Locked_Guard_Over_The_Filled_Guard()
    {
        // A locked socket that also holds content reports the lock first — it is not removable at all.
        var socket = new Socket { Type = SocketType.Gem, IsLocked = true, Content = MakeGem("Sapphire") };

        var result = CreateService().ValidateSocketing(socket, MakeGem());

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("locked");
    }

    [Fact]
    public void ValidateSocketing_Accepts_A_Matching_Empty_Unlocked_Socket()
    {
        var socket = new Socket { Type = SocketType.Gem };

        var result = CreateService().ValidateSocketing(socket, MakeGem());

        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeEmpty();
    }

    // ── SocketItem ────────────────────────────────────────────────────────────

    [Fact]
    public void SocketItem_Places_The_Item_In_The_Socket_And_Reports_Success()
    {
        var socket = new Socket { Type = SocketType.Gem };
        var gem = MakeGem();

        var result = CreateService().SocketItem(socket, gem);

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Ruby");
        socket.Content.Should().BeSameAs(gem);
    }

    [Fact]
    public void SocketItem_Leaves_The_Socket_Empty_When_Validation_Fails()
    {
        var socket = new Socket { Type = SocketType.Gem, IsLocked = true };
        var gem = MakeGem();

        var result = CreateService().SocketItem(socket, gem);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("locked");
        socket.Content.Should().BeNull("a rejected socket operation must not mutate the socket");
    }

    // ── RemoveSocketedItem ────────────────────────────────────────────────────

    [Fact]
    public void RemoveSocketedItem_Clears_The_Socket_And_Returns_The_Removed_Item()
    {
        var gem = MakeGem("Emerald");
        var socket = new Socket { Type = SocketType.Gem, Content = gem };

        var result = CreateService().RemoveSocketedItem(socket);

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Emerald");
        result.RemovedItem.Should().BeSameAs(gem);
        socket.Content.Should().BeNull();
    }

    [Fact]
    public void RemoveSocketedItem_Rejects_A_Locked_Socket_Without_Clearing_It()
    {
        var gem = MakeGem();
        var socket = new Socket { Type = SocketType.Gem, Content = gem, IsLocked = true };

        var result = CreateService().RemoveSocketedItem(socket);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("locked");
        result.RemovedItem.Should().BeNull();
        socket.Content.Should().BeSameAs(gem);
    }

    [Fact]
    public void RemoveSocketedItem_Rejects_An_Empty_Socket()
    {
        var socket = new Socket { Type = SocketType.Gem };

        var result = CreateService().RemoveSocketedItem(socket);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("already empty");
        result.RemovedItem.Should().BeNull();
    }

    // ── CalculateLinkBonus ────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(1, 1.0)]
    [InlineData(2, 1.05)]
    [InlineData(3, 1.10)]
    [InlineData(4, 1.20)]
    [InlineData(5, 1.30)]
    [InlineData(6, 1.50)]
    [InlineData(7, 1.0)]
    public void CalculateLinkBonus_Returns_The_Documented_Multiplier(int linkSize, double expected)
    {
        CreateService().CalculateLinkBonus(linkSize).Should().Be(expected);
    }
}
