using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.CharacterCreation.Commands;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.CharacterCreation.Commands;

[Trait("Category", "Feature")]
public class AbandonCreationSessionHandlerTests
{
    private readonly Mock<ICharacterCreationSessionStore> _sessionStoreMock = new();

    private AbandonCreationSessionHandler CreateHandler() =>
        new(_sessionStoreMock.Object, NullLogger<AbandonCreationSessionHandler>.Instance);

    [Fact]
    public async Task Handle_SessionNotFound_ReturnsFailed()
    {
        _sessionStoreMock.Setup(s => s.GetSessionAsync(It.IsAny<Guid>())).ReturnsAsync((CharacterCreationSession?)null);

        var result = await CreateHandler().Handle(new AbandonCreationSessionCommand(Guid.NewGuid()), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_ValidSession_RemovesSessionAndReturnsSuccess()
    {
        var session = new CharacterCreationSession();
        _sessionStoreMock.Setup(s => s.GetSessionAsync(session.SessionId)).ReturnsAsync(session);
        _sessionStoreMock.Setup(s => s.RemoveSessionAsync(session.SessionId)).Returns(Task.CompletedTask);

        var result = await CreateHandler().Handle(new AbandonCreationSessionCommand(session.SessionId), default);

        result.Success.Should().BeTrue();
        _sessionStoreMock.Verify(s => s.RemoveSessionAsync(session.SessionId), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidSession_MarksStatusAbandoned()
    {
        var session = new CharacterCreationSession();
        _sessionStoreMock.Setup(s => s.GetSessionAsync(session.SessionId)).ReturnsAsync(session);
        _sessionStoreMock.Setup(s => s.RemoveSessionAsync(session.SessionId)).Returns(Task.CompletedTask);

        await CreateHandler().Handle(new AbandonCreationSessionCommand(session.SessionId), default);

        session.Status.Should().Be(CreationSessionStatus.Abandoned);
    }
}
