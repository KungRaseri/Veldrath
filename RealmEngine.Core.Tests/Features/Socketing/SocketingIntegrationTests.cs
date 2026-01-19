using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Features.Socketing;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Data.Services;
using RealmEngine.Shared.Models;
using Xunit;

namespace RealmEngine.Core.Tests.Features.Socketing;

/// <summary>
/// Integration tests for the complete socketing workflow.
/// Tests item generation → socket generation → socketable generation → socketing flow.
/// </summary>
public class SocketingIntegrationTests
{
    private readonly GameDataCache _dataCache;
    private readonly SocketService _socketService;

    public SocketingIntegrationTests()
    {
        var basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "RealmEngine.Data", "Data", "Json");
        _dataCache = new GameDataCache(basePath, null);
        _dataCache.LoadAllData();
        _socketService = new SocketService();
    }

    [Fact]
    public void Should_Socket_Gem_Into_Empty_Socket()
    {
        // Arrange
        var gemGenerator = new GemGenerator(_dataCache, new NullLogger<GemGenerator>());
        var gem = gemGenerator.Generate("red");
        gem.Should().NotBeNull();

        var socket = new Socket
        {
            Type = SocketType.Gem,
            Content = null,
            IsLocked = false,
            LinkGroup = -1
        };

        // Act
        var result = _socketService.SocketItem(socket, gem!);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Successfully socketed");
        socket.Content.Should().Be(gem);
        socket.Content!.SocketType.Should().Be(SocketType.Gem);
    }

    [Fact]
    public void Should_Reject_Socketing_Into_Filled_Socket()
    {
        // Arrange
        var gemGenerator = new GemGenerator(_dataCache, new NullLogger<GemGenerator>());
        var gem1 = gemGenerator.Generate();
        var gem2 = gemGenerator.Generate();

        var socket = new Socket
        {
            Type = SocketType.Gem,
            Content = gem1,
            IsLocked = false
        };

        // Act
        var result = _socketService.SocketItem(socket, gem2!);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("already filled");
        socket.Content.Should().Be(gem1); // Original gem remains
    }

    [Fact]
    public void Should_Reject_Socketing_Into_Locked_Socket()
    {
        // Arrange
        var gemGenerator = new GemGenerator(_dataCache, new NullLogger<GemGenerator>());
        var gem = gemGenerator.Generate();

        var socket = new Socket
        {
            Type = SocketType.Gem,
            Content = null,
            IsLocked = true
        };

        // Act
        var result = _socketService.SocketItem(socket, gem!);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("locked");
        socket.Content.Should().BeNull();
    }

    [Fact]
    public void Should_Reject_Wrong_Socket_Type()
    {
        // Arrange
        var runeGenerator = new RuneGenerator(_dataCache, new NullLogger<RuneGenerator>());
        var rune = runeGenerator.Generate();

        var gemSocket = new Socket
        {
            Type = SocketType.Gem, // Gem socket
            Content = null,
            IsLocked = false
        };

        // Act
        var result = _socketService.SocketItem(gemSocket, rune!); // Trying to socket a Rune

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("type mismatch");
    }

    [Fact]
    public void Should_Remove_Socketed_Item_Successfully()
    {
        // Arrange
        var gemGenerator = new GemGenerator(_dataCache, new NullLogger<GemGenerator>());
        var gem = gemGenerator.Generate();

        var socket = new Socket
        {
            Type = SocketType.Gem,
            Content = gem,
            IsLocked = false
        };

        // Act
        var result = _socketService.RemoveSocketedItem(socket);

        // Assert
        result.Success.Should().BeTrue();
        result.RemovedItem.Should().Be(gem);
        socket.Content.Should().BeNull();
    }

    [Fact]
    public void Should_Reject_Removal_From_Empty_Socket()
    {
        // Arrange
        var socket = new Socket
        {
            Type = SocketType.Gem,
            Content = null,
            IsLocked = false
        };

        // Act
        var result = _socketService.RemoveSocketedItem(socket);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("already empty");
    }

    [Fact]
    public void Should_Reject_Removal_From_Locked_Socket()
    {
        // Arrange
        var gemGenerator = new GemGenerator(_dataCache, new NullLogger<GemGenerator>());
        var gem = gemGenerator.Generate();

        var socket = new Socket
        {
            Type = SocketType.Gem,
            Content = gem,
            IsLocked = true
        };

        // Act
        var result = _socketService.RemoveSocketedItem(socket);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("locked");
        socket.Content.Should().Be(gem); // Gem remains
    }

    [Fact]
    public void Should_Socket_All_Four_Socketable_Types()
    {
        // Arrange
        var gemGenerator = new GemGenerator(_dataCache, new NullLogger<GemGenerator>());
        var runeGenerator = new RuneGenerator(_dataCache, new NullLogger<RuneGenerator>());
        var crystalGenerator = new CrystalGenerator(_dataCache, new NullLogger<CrystalGenerator>());
        var orbGenerator = new OrbGenerator(_dataCache, new NullLogger<OrbGenerator>());

        var gem = gemGenerator.Generate();
        var rune = runeGenerator.Generate();
        var crystal = crystalGenerator.Generate();
        var orb = orbGenerator.Generate();

        var gemSocket = new Socket { Type = SocketType.Gem, Content = null, IsLocked = false };
        var runeSocket = new Socket { Type = SocketType.Rune, Content = null, IsLocked = false };
        var crystalSocket = new Socket { Type = SocketType.Crystal, Content = null, IsLocked = false };
        var orbSocket = new Socket { Type = SocketType.Orb, Content = null, IsLocked = false };

        // Act & Assert
        _socketService.SocketItem(gemSocket, gem!).Success.Should().BeTrue();
        gemSocket.Content.Should().NotBeNull();
        gemSocket.Content!.SocketType.Should().Be(SocketType.Gem);

        _socketService.SocketItem(runeSocket, rune!).Success.Should().BeTrue();
        runeSocket.Content.Should().NotBeNull();
        runeSocket.Content!.SocketType.Should().Be(SocketType.Rune);

        _socketService.SocketItem(crystalSocket, crystal!).Success.Should().BeTrue();
        crystalSocket.Content.Should().NotBeNull();
        crystalSocket.Content!.SocketType.Should().Be(SocketType.Crystal);

        _socketService.SocketItem(orbSocket, orb!).Success.Should().BeTrue();
        orbSocket.Content.Should().NotBeNull();
        orbSocket.Content!.SocketType.Should().Be(SocketType.Orb);
    }

    [Theory]
    [InlineData(2, 1.05)]  // 2-link = 5% bonus
    [InlineData(3, 1.10)]  // 3-link = 10% bonus
    [InlineData(4, 1.20)]  // 4-link = 20% bonus
    [InlineData(5, 1.30)]  // 5-link = 30% bonus
    [InlineData(6, 1.50)]  // 6-link = 50% bonus
    public void Should_Calculate_Correct_Link_Bonus(int linkSize, double expectedBonus)
    {
        // Act
        var bonus = _socketService.CalculateLinkBonus(linkSize);

        // Assert
        bonus.Should().Be(expectedBonus);
    }

    [Fact]
    public void Should_Apply_Trait_Values_From_Socketed_Gem()
    {
        // Arrange
        var gemGenerator = new GemGenerator(_dataCache, new NullLogger<GemGenerator>());
        var gem = gemGenerator.Generate("red"); // Red gems typically add Strength
        gem.Should().NotBeNull();

        var socket = new Socket
        {
            Type = SocketType.Gem,
            Content = null,
            IsLocked = false
        };

        // Act
        _socketService.SocketItem(socket, gem!);

        // Assert
        socket.Content.Should().NotBeNull();
        socket.Content!.Traits.Should().NotBeEmpty("Gem should provide trait bonuses");
    }
}
