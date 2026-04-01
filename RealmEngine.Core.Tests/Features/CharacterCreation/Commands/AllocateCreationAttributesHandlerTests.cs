using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.CharacterCreation.Commands;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.CharacterCreation.Commands;

[Trait("Category", "Feature")]
public class AllocateCreationAttributesHandlerTests
{
    private readonly Mock<ICharacterCreationSessionStore> _sessionStoreMock = new();

    private AllocateCreationAttributesHandler CreateHandler() =>
        new(_sessionStoreMock.Object, NullLogger<AllocateCreationAttributesHandler>.Instance);

    private static Dictionary<string, int> ValidAlloc(
        int str = 10, int dex = 10, int con = 10,
        int intel = 10, int wis = 10, int cha = 10) =>
        new()
        {
            ["Strength"] = str, ["Dexterity"] = dex, ["Constitution"] = con,
            ["Intelligence"] = intel, ["Wisdom"] = wis, ["Charisma"] = cha,
        };

    [Fact]
    public async Task Handle_SessionNotFound_ReturnsFailed()
    {
        _sessionStoreMock.Setup(s => s.GetSessionAsync(It.IsAny<Guid>())).ReturnsAsync((CharacterCreationSession?)null);

        var result = await CreateHandler().Handle(
            new AllocateCreationAttributesCommand(Guid.NewGuid(), ValidAlloc()), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_ValidAllocation_ReturnsSuccess_And_SetsAllocations()
    {
        var session = new CharacterCreationSession();
        _sessionStoreMock.Setup(s => s.GetSessionAsync(session.SessionId)).ReturnsAsync(session);
        _sessionStoreMock.Setup(s => s.UpdateSessionAsync(session)).Returns(Task.CompletedTask);
        var alloc = ValidAlloc();

        var result = await CreateHandler().Handle(
            new AllocateCreationAttributesCommand(session.SessionId, alloc), default);

        result.Success.Should().BeTrue();
        session.AttributeAllocations.Should().BeEquivalentTo(alloc);
    }

    [Fact]
    public async Task Handle_ValidAllocation_ReturnsCorrectRemainingPoints()
    {
        var session = new CharacterCreationSession();
        _sessionStoreMock.Setup(s => s.GetSessionAsync(session.SessionId)).ReturnsAsync(session);
        _sessionStoreMock.Setup(s => s.UpdateSessionAsync(session)).Returns(Task.CompletedTask);

        // cost: 10(2) × 6 = 12 spent, 27 - 12 = 15 remaining
        var result = await CreateHandler().Handle(
            new AllocateCreationAttributesCommand(session.SessionId, ValidAlloc()), default);

        result.RemainingPoints.Should().Be(15);
    }

    [Fact]
    public async Task Handle_OverBudget_ReturnsFailed()
    {
        var session = new CharacterCreationSession();
        _sessionStoreMock.Setup(s => s.GetSessionAsync(session.SessionId)).ReturnsAsync(session);

        // 15(9) × 6 = 54 total — far over budget of 27
        var result = await CreateHandler().Handle(
            new AllocateCreationAttributesCommand(session.SessionId, ValidAlloc(15, 15, 15, 15, 15, 15)), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("budget");
    }

    [Fact]
    public async Task Handle_ValueBelowMin_ReturnsFailed()
    {
        var session = new CharacterCreationSession();
        _sessionStoreMock.Setup(s => s.GetSessionAsync(session.SessionId)).ReturnsAsync(session);

        var result = await CreateHandler().Handle(
            new AllocateCreationAttributesCommand(session.SessionId, ValidAlloc(str: 7)), default);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ValueAboveMax_ReturnsFailed()
    {
        var session = new CharacterCreationSession();
        _sessionStoreMock.Setup(s => s.GetSessionAsync(session.SessionId)).ReturnsAsync(session);

        var result = await CreateHandler().Handle(
            new AllocateCreationAttributesCommand(session.SessionId, ValidAlloc(str: 16)), default);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_FinalizedSession_ReturnsFailed()
    {
        var session = new CharacterCreationSession { Status = CreationSessionStatus.Finalized };
        _sessionStoreMock.Setup(s => s.GetSessionAsync(session.SessionId)).ReturnsAsync(session);

        var result = await CreateHandler().Handle(
            new AllocateCreationAttributesCommand(session.SessionId, ValidAlloc()), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Finalized");
    }
}
