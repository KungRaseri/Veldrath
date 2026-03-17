using FluentAssertions;
using RealmEngine.Shared.Models;
using RealmEngine.Core.Features.Party.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace RealmEngine.Core.Tests.Features.Party;

[Trait("Category", "Party")]
/// <summary>
/// Tests for PartyService.
/// </summary>
public class PartyServiceTests
{
    private readonly PartyService _partyService;

    public PartyServiceTests()
    {
        _partyService = new PartyService(NullLogger<PartyService>.Instance);
    }

    [Fact]
    public void CreateParty_Should_Create_Party_With_Leader()
    {
        // Arrange
        var leader = CreateTestCharacter("Hero");

        // Act
        var party = _partyService.CreateParty(leader, 4);

        // Assert
        party.Should().NotBeNull();
        party.Leader.Should().Be(leader);
        party.MaxSize.Should().Be(4);
        party.CurrentSize.Should().Be(1);
        party.IsFull.Should().BeFalse();
    }

    [Fact]
    public void RecruitNPC_Should_Add_Member_To_Party()
    {
        // Arrange
        var leader = CreateTestCharacter("Hero");
        var party = _partyService.CreateParty(leader);
        var npc = CreateTestNPC("Warrior", "Fighter");

        // Act
        var success = _partyService.RecruitNPC(party, npc, out string errorMessage);

        // Assert
        success.Should().BeTrue();
        errorMessage.Should().BeEmpty();
        party.Members.Should().HaveCount(1);
        party.Members[0].Name.Should().Be("Warrior");
        party.Members[0].Role.Should().Be(PartyRole.DPS);
    }

    [Fact]
    public void RecruitNPC_Should_Fail_When_Party_Is_Full()
    {
        // Arrange
        var leader = CreateTestCharacter("Hero");
        var party = _partyService.CreateParty(leader, 2); // Max 2 members
        var npc1 = CreateTestNPC("Warrior", "Fighter");
        var npc2 = CreateTestNPC("Mage", "Wizard");

        _partyService.RecruitNPC(party, npc1, out _);

        // Act
        var success = _partyService.RecruitNPC(party, npc2, out string errorMessage);

        // Assert
        success.Should().BeFalse();
        errorMessage.Should().Contain("Party is full");
        party.Members.Should().HaveCount(1);
    }

    [Fact]
    public void RecruitNPC_Should_Fail_When_NPC_Already_In_Party()
    {
        // Arrange
        var leader = CreateTestCharacter("Hero");
        var party = _partyService.CreateParty(leader);
        var npc = CreateTestNPC("Warrior", "Fighter");

        _partyService.RecruitNPC(party, npc, out _);

        // Act
        var success = _partyService.RecruitNPC(party, npc, out string errorMessage);

        // Assert
        success.Should().BeFalse();
        errorMessage.Should().Contain("already in your party");
    }

    [Fact]
    public void RecruitNPC_Should_Assign_Tank_Role_To_Knights()
    {
        // Arrange
        var leader = CreateTestCharacter("Hero");
        var party = _partyService.CreateParty(leader);
        var npc = CreateTestNPC("Knight", "Knight");

        // Act
        _partyService.RecruitNPC(party, npc, out _);

        // Assert
        party.Members[0].Role.Should().Be(PartyRole.Tank);
    }

    [Fact]
    public void RecruitNPC_Should_Assign_Healer_Role_To_Clerics()
    {
        // Arrange
        var leader = CreateTestCharacter("Hero");
        var party = _partyService.CreateParty(leader);
        var npc = CreateTestNPC("Cleric", "Cleric");

        // Act
        _partyService.RecruitNPC(party, npc, out _);

        // Assert
        party.Members[0].Role.Should().Be(PartyRole.Healer);
    }

    [Fact]
    public void DismissPartyMember_Should_Remove_Member()
    {
        // Arrange
        var leader = CreateTestCharacter("Hero");
        var party = _partyService.CreateParty(leader);
        var npc = CreateTestNPC("Warrior", "Fighter");
        _partyService.RecruitNPC(party, npc, out _);
        var memberId = party.Members[0].Id;

        // Act
        var success = _partyService.DismissPartyMember(party, memberId, out string errorMessage);

        // Assert
        success.Should().BeTrue();
        errorMessage.Should().BeEmpty();
        party.Members.Should().BeEmpty();
    }

    [Fact]
    public void DismissPartyMember_Should_Fail_When_Member_Not_Found()
    {
        // Arrange
        var leader = CreateTestCharacter("Hero");
        var party = _partyService.CreateParty(leader);

        // Act
        var success = _partyService.DismissPartyMember(party, "invalid-id", out string errorMessage);

        // Assert
        success.Should().BeFalse();
        errorMessage.Should().Contain("not found");
    }

    [Fact]
    public void HealParty_Should_Restore_All_Members_Health_And_Mana()
    {
        // Arrange
        var leader = CreateTestCharacter("Hero");
        leader.Health = 50; // Damaged
        leader.Mana = 25;   // Low mana

        var party = _partyService.CreateParty(leader);
        var npc = CreateTestNPC("Warrior", "Fighter");
        _partyService.RecruitNPC(party, npc, out _);
        
        var member = party.Members[0];
        member.Health = 50; // Damaged

        // Act
        _partyService.HealParty(party);

        // Assert
        leader.Health.Should().Be(leader.MaxHealth);
        leader.Mana.Should().Be(leader.MaxMana);
        member.Health.Should().Be(member.MaxHealth);
        member.Mana.Should().Be(member.MaxMana);
    }

    [Fact]
    public void DistributeExperience_Should_Split_XP_Among_Alive_Members()
    {
        // Arrange
        var leader = CreateTestCharacter("Hero");
        var party = _partyService.CreateParty(leader);
        var npc1 = CreateTestNPC("Warrior", "Fighter");
        var npc2 = CreateTestNPC("Mage", "Wizard");
        
        _partyService.RecruitNPC(party, npc1, out _);
        _partyService.RecruitNPC(party, npc2, out _);

        var initialLeaderXP = leader.Experience;

        // Act
        _partyService.DistributeExperience(party, 300);

        // Assert
        var xpPerMember = 300 / 3; // 3 alive members (leader + 2 NPCs)
        leader.Experience.Should().BeGreaterThan(initialLeaderXP);
        party.Members[0].Experience.Should().Be(xpPerMember);
        party.Members[1].Experience.Should().Be(xpPerMember);
    }

    [Fact]
    public void DistributeExperience_Should_Not_Give_XP_To_Dead_Members()
    {
        // Arrange
        var leader = CreateTestCharacter("Hero");
        var party = _partyService.CreateParty(leader);
        var npc1 = CreateTestNPC("Warrior", "Fighter");
        var npc2 = CreateTestNPC("Mage", "Wizard");
        
        _partyService.RecruitNPC(party, npc1, out _);
        _partyService.RecruitNPC(party, npc2, out _);

        // Kill second member
        party.Members[1].Health = 0;

        // Act
        _partyService.DistributeExperience(party, 200);

        // Assert
        var xpPerMember = 200 / 2; // 2 alive members (leader + 1 NPC)
        party.Members[0].Experience.Should().Be(xpPerMember);
        party.Members[1].Experience.Should().Be(0); // Dead member gets no XP
    }

    [Fact]
    public void DistributeGold_Should_Give_All_Gold_To_Leader()
    {
        // Arrange
        var leader = CreateTestCharacter("Hero");
        var party = _partyService.CreateParty(leader);
        var initialGold = leader.Gold;

        // Act
        _partyService.DistributeGold(party, 500);

        // Assert
        leader.Gold.Should().Be(initialGold + 500);
    }

    [Fact]
    public void GetAliveCombatants_Should_Return_Only_Alive_Members()
    {
        // Arrange
        var leader = CreateTestCharacter("Hero");
        var party = _partyService.CreateParty(leader);
        var npc1 = CreateTestNPC("Warrior", "Fighter");
        var npc2 = CreateTestNPC("Mage", "Wizard");
        var npc3 = CreateTestNPC("Rogue", "Thief");
        
        _partyService.RecruitNPC(party, npc1, out _);
        _partyService.RecruitNPC(party, npc2, out _);
        _partyService.RecruitNPC(party, npc3, out _);

        // Kill one member
        party.Members[1].Health = 0;

        // Act
        var alive = _partyService.GetAliveCombatants(party);

        // Assert
        alive.Should().HaveCount(2);
        alive.Should().Contain(m => m.Name == "Warrior");
        alive.Should().Contain(m => m.Name == "Rogue");
        alive.Should().NotContain(m => m.Name == "Mage");
    }

    [Fact]
    public void IsEntirePartyDead_Should_Return_True_When_All_Dead()
    {
        // Arrange
        var leader = CreateTestCharacter("Hero");
        leader.Health = 0;
        
        var party = _partyService.CreateParty(leader);
        var npc = CreateTestNPC("Warrior", "Fighter");
        _partyService.RecruitNPC(party, npc, out _);
        party.Members[0].Health = 0;

        // Act
        var isDead = _partyService.IsEntirePartyDead(party);

        // Assert
        isDead.Should().BeTrue();
    }

    [Fact]
    public void IsEntirePartyDead_Should_Return_False_When_Leader_Alive()
    {
        // Arrange
        var leader = CreateTestCharacter("Hero");
        var party = _partyService.CreateParty(leader);
        var npc = CreateTestNPC("Warrior", "Fighter");
        _partyService.RecruitNPC(party, npc, out _);
        party.Members[0].Health = 0; // Only NPC dead

        // Act
        var isDead = _partyService.IsEntirePartyDead(party);

        // Assert
        isDead.Should().BeFalse();
    }

    [Fact]
    public void EquipItem_Should_Equip_Weapon_To_Member()
    {
        // Arrange
        var leader = CreateTestCharacter("Hero");
        var party = _partyService.CreateParty(leader);
        var npc = CreateTestNPC("Warrior", "Fighter");
        _partyService.RecruitNPC(party, npc, out _);
        
        var member = party.Members[0];
        var weapon = CreateTestWeapon("Iron Sword");
        member.Inventory.Add(weapon);

        // Act
        var success = _partyService.EquipItem(member, weapon, out string errorMessage);

        // Assert
        success.Should().BeTrue();
        member.EquippedWeapon.Should().Be(weapon);
    }

    // Helper methods
    private Character CreateTestCharacter(string name)
    {
        return new Character
        {
            Name = name,
            Level = 5,
            Health = 100,
            MaxHealth = 100,
            Mana = 50,
            MaxMana = 50,
            Gold = 100,
            Experience = 0
        };
    }

    private NPC CreateTestNPC(string name, string occupation)
    {
        return new NPC
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Occupation = occupation,
            Age = 30,
            IsFriendly = true,
            Traits = new Dictionary<string, TraitValue>
            {
                ["strength"] = new TraitValue(12, TraitType.Number),
                ["dexterity"] = new TraitValue(10, TraitType.Number),
                ["constitution"] = new TraitValue(14, TraitType.Number),
                ["intelligence"] = new TraitValue(8, TraitType.Number),
                ["wisdom"] = new TraitValue(8, TraitType.Number),
                ["charisma"] = new TraitValue(10, TraitType.Number),
                ["level"] = new TraitValue(3, TraitType.Number)
            }
        };
    }

    private Item CreateTestWeapon(string name)
    {
        return new Item
        {
            Name = name,
            Type = ItemType.Weapon,
            Price = 50,
            Traits = new Dictionary<string, TraitValue>
            {
                ["attack"] = new TraitValue(10, TraitType.Number)
            }
        };
    }
}
