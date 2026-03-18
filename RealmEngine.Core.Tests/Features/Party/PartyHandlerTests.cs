using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.Party.Commands;
using RealmEngine.Core.Features.Party.Queries;
using RealmEngine.Core.Features.Party.Services;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Party;

public class DismissPartyMemberHandlerTests
{
    private static PartyService CreatePartyService() =>
        new(NullLogger<PartyService>.Instance);

    private static DismissPartyMemberHandler CreateHandler(
        ISaveGameService? saveGameService = null,
        PartyService? partyService = null) =>
        new(
            saveGameService ?? Mock.Of<ISaveGameService>(),
            partyService ?? CreatePartyService(),
            NullLogger<DismissPartyMemberHandler>.Instance);

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNoActiveSave()
    {
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);
        var handler = CreateHandler(mockSave.Object);

        var result = await handler.Handle(new DismissPartyMemberCommand { MemberId = "npc-1" }, default);

        result.Success.Should().BeFalse();
        result.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNoPartyExists()
    {
        var saveGame = new SaveGame { Character = new Character { Name = "Hero" }, Party = null };
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns(saveGame);
        var handler = CreateHandler(mockSave.Object);

        var result = await handler.Handle(new DismissPartyMemberCommand { MemberId = "npc-1" }, default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("party");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenMemberNotFound()
    {
        var partyService = CreatePartyService();
        var leader = new Character { Name = "Hero" };
        var party = partyService.CreateParty(leader);
        var saveGame = new SaveGame { Character = leader, Party = party };
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns(saveGame);
        var handler = CreateHandler(mockSave.Object, partyService);

        var result = await handler.Handle(new DismissPartyMemberCommand { MemberId = "no-such-member" }, default);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_DismissesExistingMember_ReturnsSuccess()
    {
        var partyService = CreatePartyService();
        var leader = new Character { Name = "Hero" };
        var party = partyService.CreateParty(leader);
        var npc = new NPC { Id = "npc-1", Name = "Sidekick", IsFriendly = true };
        partyService.RecruitNPC(party, npc, out _);
        var memberId = party.Members[0].Id;

        var saveGame = new SaveGame { Character = leader, Party = party };
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns(saveGame);
        var handler = CreateHandler(mockSave.Object, partyService);

        var result = await handler.Handle(new DismissPartyMemberCommand { MemberId = memberId }, default);

        result.Success.Should().BeTrue();
        party.Members.Should().BeEmpty();
    }
}

public class RecruitNPCHandlerTests
{
    private static PartyService CreatePartyService() =>
        new(NullLogger<PartyService>.Instance);

    private static RecruitNPCHandler CreateHandler(
        ISaveGameService? saveGameService = null,
        PartyService? partyService = null) =>
        new(
            saveGameService ?? Mock.Of<ISaveGameService>(),
            partyService ?? CreatePartyService(),
            NullLogger<RecruitNPCHandler>.Instance);

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNoActiveSave()
    {
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);
        var handler = CreateHandler(mockSave.Object);

        var result = await handler.Handle(new RecruitNPCCommand { NpcId = "npc-1" }, default);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNPCNotFound()
    {
        var saveGame = new SaveGame { Character = new Character { Name = "Hero" } };
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns(saveGame);
        var handler = CreateHandler(mockSave.Object);

        var result = await handler.Handle(new RecruitNPCCommand { NpcId = "ghost-npc" }, default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNPCIsHostile()
    {
        var npc = new NPC { Id = "npc-bad", Name = "Bandit", IsFriendly = false };
        var saveGame = new SaveGame
        {
            Character = new Character { Name = "Hero" },
            KnownNPCs = [npc]
        };
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns(saveGame);
        var handler = CreateHandler(mockSave.Object);

        var result = await handler.Handle(new RecruitNPCCommand { NpcId = "npc-bad" }, default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("hostile");
    }

    [Fact]
    public async Task Handle_CreatesPartyAndRecruits_WhenNoPartyExists()
    {
        var npc = new NPC { Id = "npc-1", Name = "Ranger", IsFriendly = true };
        var leader = new Character { Name = "Hero" };
        var saveGame = new SaveGame
        {
            Character = leader,
            Party = null,
            KnownNPCs = [npc]
        };
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns(saveGame);
        var partyService = CreatePartyService();
        var handler = CreateHandler(mockSave.Object, partyService);

        var result = await handler.Handle(new RecruitNPCCommand { NpcId = "npc-1" }, default);

        result.Success.Should().BeTrue();
        result.Member.Should().NotBeNull();
        saveGame.Party.Should().NotBeNull();
        saveGame.Party!.Members.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_RecruitsToExistingParty()
    {
        var partyService = CreatePartyService();
        var leader = new Character { Name = "Hero" };
        var party = partyService.CreateParty(leader);
        var npc = new NPC { Id = "npc-1", Name = "Mage", IsFriendly = true };
        var saveGame = new SaveGame
        {
            Character = leader,
            Party = party,
            KnownNPCs = [npc]
        };
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns(saveGame);
        var handler = CreateHandler(mockSave.Object, partyService);

        var result = await handler.Handle(new RecruitNPCCommand { NpcId = "npc-1" }, default);

        result.Success.Should().BeTrue();
        party.Members.Should().HaveCount(1);
    }
}

public class GetPartyHandlerTests
{
    private static GetPartyHandler CreateHandler(ISaveGameService? saveGameService = null) =>
        new(saveGameService ?? Mock.Of<ISaveGameService>());

    [Fact]
    public async Task Handle_ReturnsNoParty_WhenNoActiveSave()
    {
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);
        var handler = CreateHandler(mockSave.Object);

        var result = await handler.Handle(new GetPartyQuery(), default);

        result.HasParty.Should().BeFalse();
        result.Members.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsNoParty_WhenSaveHasNoParty()
    {
        var saveGame = new SaveGame { Character = new Character(), Party = null };
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns(saveGame);
        var handler = CreateHandler(mockSave.Object);

        var result = await handler.Handle(new GetPartyQuery(), default);

        result.HasParty.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ReturnsPartyInfo_WhenPartyExists()
    {
        var partyService = new PartyService(NullLogger<PartyService>.Instance);
        var leader = new Character { Name = "Paladin", Level = 5 };
        var party = partyService.CreateParty(leader, 4);
        var npc = new NPC { Id = "n1", Name = "Rogue", IsFriendly = true };
        partyService.RecruitNPC(party, npc, out _);

        var saveGame = new SaveGame { Character = leader, Party = party };
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns(saveGame);
        var handler = CreateHandler(mockSave.Object);

        var result = await handler.Handle(new GetPartyQuery(), default);

        result.HasParty.Should().BeTrue();
        result.LeaderName.Should().Be("Paladin");
        result.LeaderLevel.Should().Be(5);
        result.CurrentSize.Should().Be(2); // leader + 1 member
        result.MaxSize.Should().Be(4);
        result.Members.Should().HaveCount(1);
        result.Members[0].Name.Should().Be("Rogue");
    }
}
