using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.CharacterCreation.Commands;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.CharacterCreation.Commands;

[Trait("Category", "Feature")]
public class FinalizeCreationSessionHandlerTests
{
    private readonly Mock<ICharacterCreationSessionStore> _sessionStoreMock = new();
    private readonly Mock<IMediator>                      _mediatorMock     = new();

    private FinalizeCreationSessionHandler CreateHandler() =>
        new(_sessionStoreMock.Object, _mediatorMock.Object,
            NullLogger<FinalizeCreationSessionHandler>.Instance);

    private static CharacterClass WarriorClass => new()
    {
        Name = "Warrior", Slug = "warrior",
        BonusStrength = 3, StartingHealth = 120, StartingMana = 20,
    };

    [Fact]
    public async Task Handle_SessionNotFound_ReturnsFailed()
    {
        _sessionStoreMock.Setup(s => s.GetSessionAsync(It.IsAny<Guid>())).ReturnsAsync((CharacterCreationSession?)null);

        var result = await CreateHandler().Handle(
            new FinalizeCreationSessionCommand { SessionId = Guid.NewGuid(), CharacterName = "Hero" }, default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_NoClassSelected_ReturnsFailed()
    {
        var session = new CharacterCreationSession(); // SelectedClass is null
        _sessionStoreMock.Setup(s => s.GetSessionAsync(session.SessionId)).ReturnsAsync(session);

        var result = await CreateHandler().Handle(
            new FinalizeCreationSessionCommand { SessionId = session.SessionId, CharacterName = "Hero" }, default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("class");
    }

    [Fact]
    public async Task Handle_BothNamesNull_ReturnsFailed()
    {
        var session = new CharacterCreationSession { SelectedClass = WarriorClass };
        _sessionStoreMock.Setup(s => s.GetSessionAsync(session.SessionId)).ReturnsAsync(session);

        var result = await CreateHandler().Handle(
            new FinalizeCreationSessionCommand { SessionId = session.SessionId, CharacterName = null }, default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("required");
    }

    [Fact]
    public async Task Handle_NameFromRequest_PassedToCreateCharacterCommand()
    {
        var session = new CharacterCreationSession { SelectedClass = WarriorClass };
        _sessionStoreMock.Setup(s => s.GetSessionAsync(session.SessionId)).ReturnsAsync(session);
        _sessionStoreMock.Setup(s => s.UpdateSessionAsync(session)).Returns(Task.CompletedTask);
        var capturedCommand = (CreateCharacterCommand?)null;
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateCharacterCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<CreateCharacterResult>, CancellationToken>((cmd, _) =>
                capturedCommand = (CreateCharacterCommand)cmd)
            .ReturnsAsync(new CreateCharacterResult
            {
                Success = true, Character = new Character { Name = "Hero" }
            });

        await CreateHandler().Handle(
            new FinalizeCreationSessionCommand { SessionId = session.SessionId, CharacterName = "Hero" }, default);

        capturedCommand.Should().NotBeNull();
        capturedCommand!.CharacterName.Should().Be("Hero");
    }

    [Fact]
    public async Task Handle_NameFromSession_UsedWhenRequestNameIsNull()
    {
        var session = new CharacterCreationSession
        {
            SelectedClass = WarriorClass,
            CharacterName = "SessionHero",
        };
        _sessionStoreMock.Setup(s => s.GetSessionAsync(session.SessionId)).ReturnsAsync(session);
        _sessionStoreMock.Setup(s => s.UpdateSessionAsync(session)).Returns(Task.CompletedTask);
        var capturedCommand = (CreateCharacterCommand?)null;
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateCharacterCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<CreateCharacterResult>, CancellationToken>((cmd, _) =>
                capturedCommand = (CreateCharacterCommand)cmd)
            .ReturnsAsync(new CreateCharacterResult
            {
                Success = true, Character = new Character { Name = "SessionHero" }
            });

        await CreateHandler().Handle(
            new FinalizeCreationSessionCommand { SessionId = session.SessionId, CharacterName = null }, default);

        capturedCommand!.CharacterName.Should().Be("SessionHero");
    }

    [Fact]
    public async Task Handle_Success_MarksSessionFinalized()
    {
        var session = new CharacterCreationSession { SelectedClass = WarriorClass };
        _sessionStoreMock.Setup(s => s.GetSessionAsync(session.SessionId)).ReturnsAsync(session);
        _sessionStoreMock.Setup(s => s.UpdateSessionAsync(session)).Returns(Task.CompletedTask);
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateCharacterCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateCharacterResult
            {
                Success = true, Character = new Character { Name = "Hero" }
            });

        await CreateHandler().Handle(
            new FinalizeCreationSessionCommand { SessionId = session.SessionId, CharacterName = "Hero" }, default);

        session.Status.Should().Be(CreationSessionStatus.Finalized);
    }

    [Fact]
    public async Task Handle_CreateCharacterFails_ReturnsFailureResult()
    {
        var session = new CharacterCreationSession { SelectedClass = WarriorClass };
        _sessionStoreMock.Setup(s => s.GetSessionAsync(session.SessionId)).ReturnsAsync(session);
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateCharacterCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateCharacterResult { Success = false, Message = "DB error" });

        var result = await CreateHandler().Handle(
            new FinalizeCreationSessionCommand { SessionId = session.SessionId, CharacterName = "Hero" }, default);

        result.Success.Should().BeFalse();
    }
}
