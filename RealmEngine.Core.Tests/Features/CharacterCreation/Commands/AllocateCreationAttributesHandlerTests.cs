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

    private static Dictionary<string, int> Alloc(
        int str = 10, int dex = 10, int con = 10,
        int intel = 10, int wis = 10, int cha = 10) =>
        new()
        {
            ["Strength"] = str,
            ["Dexterity"] = dex,
            ["Constitution"] = con,
            ["Intelligence"] = intel,
            ["Wisdom"] = wis,
            ["Charisma"] = cha,
        };

    /// <summary>
    /// An allocation that spends the whole 27-point budget: 15(9) + 14(7) + 12(4) + 11(3) + 10(2) + 10(2).
    /// </summary>
    private static Dictionary<string, int> FullAlloc() => Alloc(15, 14, 12, 11, 10, 10);

    private void SetupSession(CharacterCreationSession session)
    {
        _sessionStoreMock.Setup(s => s.GetSessionAsync(session.SessionId)).ReturnsAsync(session);
        _sessionStoreMock.Setup(s => s.UpdateSessionAsync(session)).Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Handle_SessionNotFound_ReturnsFailed()
    {
        _sessionStoreMock.Setup(s => s.GetSessionAsync(It.IsAny<Guid>())).ReturnsAsync((CharacterCreationSession?)null);

        var result = await CreateHandler().Handle(
            new AllocateCreationAttributesCommand(Guid.NewGuid(), FullAlloc()), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_FullAllocation_ReturnsSuccess_And_SetsAllocations()
    {
        var session = new CharacterCreationSession();
        SetupSession(session);
        var alloc = FullAlloc();

        var result = await CreateHandler().Handle(
            new AllocateCreationAttributesCommand(session.SessionId, alloc), default);

        result.Success.Should().BeTrue();
        result.RemainingPoints.Should().Be(0);
        session.AttributeAllocations.Should().BeEquivalentTo(alloc);
    }

    [Fact]
    public async Task Handle_PartialAllocation_ReturnsFailed_And_StoresNothing()
    {
        var session = new CharacterCreationSession();
        SetupSession(session);

        // All tens costs 12 of the 27 points, so 15 are still unspent.
        var result = await CreateHandler().Handle(
            new AllocateCreationAttributesCommand(session.SessionId, Alloc()), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("must be spent");
        result.Message.Should().Contain("15 of 27");
        result.RemainingPoints.Should().Be(15);
        session.AttributeAllocations.Should().BeNull("a refused allocation must not be stored");
        _sessionStoreMock.Verify(s => s.UpdateSessionAsync(It.IsAny<CharacterCreationSession>()), Times.Never);
    }

    [Fact]
    public async Task Handle_OverBudget_ReturnsFailed()
    {
        var session = new CharacterCreationSession();
        SetupSession(session);

        // 15(9) × 6 = 54 total — far over budget of 27
        var result = await CreateHandler().Handle(
            new AllocateCreationAttributesCommand(session.SessionId, Alloc(15, 15, 15, 15, 15, 15)), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("budget");
    }

    [Fact]
    public async Task Handle_ValueBelowMin_ReturnsFailed()
    {
        var session = new CharacterCreationSession();
        SetupSession(session);

        var result = await CreateHandler().Handle(
            new AllocateCreationAttributesCommand(session.SessionId, Alloc(str: 7)), default);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ValueAboveMax_ReturnsFailed()
    {
        var session = new CharacterCreationSession();
        SetupSession(session);

        var result = await CreateHandler().Handle(
            new AllocateCreationAttributesCommand(session.SessionId, Alloc(str: 16)), default);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_MissingStat_ReturnsFailed()
    {
        var session = new CharacterCreationSession();
        SetupSession(session);
        var partial = FullAlloc();
        partial.Remove("Charisma");

        var result = await CreateHandler().Handle(
            new AllocateCreationAttributesCommand(session.SessionId, partial), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Charisma");
    }

    [Fact]
    public async Task Handle_FinalizedSession_ReturnsFailed()
    {
        var session = new CharacterCreationSession { Status = CreationSessionStatus.Finalized };
        SetupSession(session);

        var result = await CreateHandler().Handle(
            new AllocateCreationAttributesCommand(session.SessionId, FullAlloc()), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Finalized");
    }
}
