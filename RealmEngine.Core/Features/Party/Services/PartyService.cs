using RealmEngine.Shared.Models;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Party.Services;

/// <summary>
/// Service for managing party recruitment, dismissal, and party state.
/// </summary>
public class PartyService
{
    /// <summary>
    /// Creates a new party with the player as leader.
    /// </summary>
    public Shared.Models.Party CreateParty(Character leader, int maxSize = 4)
    {
        return new Shared.Models.Party
        {
            Leader = leader,
            MaxSize = maxSize
        };
    }

    /// <summary>
    /// Recruits an NPC to the party.
    /// </summary>
    public bool RecruitNPC(Shared.Models.Party party, NPC npc, out string errorMessage)
    {
        errorMessage = string.Empty;

        // Check if party is full
        if (party.IsFull)
        {
            errorMessage = $"Party is full! Maximum {party.MaxSize} members allowed.";
            return false;
        }

        // Check if already in party
        if (party.Members.Any(m => m.Id == npc.Id))
        {
            errorMessage = $"{npc.Name} is already in your party!";
            return false;
        }

        // Convert NPC to PartyMember
        var member = ConvertNPCToPartyMember(npc);
        
        // Add to party
        party.AddMember(member);
        
        _logger.LogInformation("NPC {Name} recruited to party (Role: {Role})", npc.Name, member.Role);
        return true;
    }

    /// <summary>
    /// Dismisses a party member.
    /// </summary>
    public bool DismissPartyMember(Shared.Models.Party party, string memberId, out string errorMessage)
    {
        errorMessage = string.Empty;

        var member = party.FindMember(memberId);
        if (member == null)
        {
            errorMessage = "Party member not found!";
            return false;
        }

        party.RemoveMember(memberId);
        _logger.LogInformation("Party member {Name} dismissed from party", member.Name);
        return true;
    }

    /// <summary>
    /// Heals all party members (e.g., after resting at inn).
    /// </summary>
    public void HealParty(Shared.Models.Party party)
    {
        party.Leader.Health = party.Leader.MaxHealth;
        party.Leader.Mana = party.Leader.MaxMana;

        foreach (var member in party.Members)
        {
            member.Health = member.MaxHealth;
            member.Mana = member.MaxMana;
        }

        _logger.LogInformation("Party fully healed and restored");
    }

    /// <summary>
    /// Distributes experience to all alive party members.
    /// </summary>
    public void DistributeExperience(Shared.Models.Party party, int totalXp)
    {
        var aliveCount = 1 + party.AliveMembers.Count;
        if (aliveCount == 0) return;

        var xpPerMember = totalXp / aliveCount;

        // Award to player
        party.Leader.GainExperience(xpPerMember);

        // Award to alive party members
        foreach (var member in party.AliveMembers)
        {
            if (member.GainExperience(xpPerMember))
            {
                _logger.LogInformation("Party member {Name} leveled up to {Level}!", member.Name, member.Level);
            }
        }

        _logger.LogInformation("Distributed {Xp} XP to {Count} party members", xpPerMember, aliveCount);
    }

    /// <summary>
    /// Distributes gold to the party leader.
    /// </summary>
    public void DistributeGold(Shared.Models.Party party, int totalGold)
    {
        party.Leader.Gold += totalGold;
        _logger.LogInformation("Party leader received {Gold} gold", totalGold);
    }

    /// <summary>
    /// Gets alive party members for combat.
    /// </summary>
    public List<PartyMember> GetAliveCombatants(Shared.Models.Party party)
    {
        return party.AliveMembers;
    }

    /// <summary>
    /// Checks if entire party is dead (game over condition).
    /// </summary>
    public bool IsEntirePartyDead(Shared.Models.Party party)
    {
        return party.Leader.Health <= 0 && party.AliveMembers.Count == 0;
    }

    /// <summary>
    /// Converts an NPC to a PartyMember.
    /// </summary>
    private PartyMember ConvertNPCToPartyMember(NPC npc)
    {
        // Determine role from NPC occupation or class
        var role = DetermineRole(npc.Occupation);

        var member = new PartyMember
        {
            Id = npc.Id,
            Name = npc.Name,
            ClassName = npc.Occupation,
            Level = DetermineLevel(npc),
            Health = 100,
            MaxHealth = 100,
            Mana = 50,
            MaxMana = 50,
            Strength = GetAttributeFromTraits(npc, "strength", 10),
            Dexterity = GetAttributeFromTraits(npc, "dexterity", 10),
            Constitution = GetAttributeFromTraits(npc, "constitution", 10),
            Intelligence = GetAttributeFromTraits(npc, "intelligence", 10),
            Wisdom = GetAttributeFromTraits(npc, "wisdom", 10),
            Charisma = GetAttributeFromTraits(npc, "charisma", 10),
            Experience = 0,
            AbilityIds = npc.AbilityIds.ToList(),
            Role = role,
            Behavior = AIBehavior.Balanced
        };

        // Calculate derived stats based on level
        member.MaxHealth = 100 + (member.Level * (10 + member.Constitution));
        member.MaxMana = 50 + (member.Level * (5 + member.Wisdom));
        member.Health = member.MaxHealth;
        member.Mana = member.MaxMana;

        return member;
    }

    /// <summary>
    /// Determines party role from NPC occupation.
    /// </summary>
    private PartyRole DetermineRole(string occupation)
    {
        var lower = occupation.ToLower();
        
        if (lower.Contains("tank") || lower.Contains("guard") || lower.Contains("knight") || lower.Contains("paladin"))
            return PartyRole.Tank;
        
        if (lower.Contains("heal") || lower.Contains("cleric") || lower.Contains("priest"))
            return PartyRole.Healer;
        
        if (lower.Contains("mage") || lower.Contains("support") || lower.Contains("bard"))
            return PartyRole.Support;
        
        // Default to DPS (rogue, warrior, ranger)
        return PartyRole.DPS;
    }

    /// <summary>
    /// Determines level from NPC traits or defaults to 1.
    /// </summary>
    private int DetermineLevel(NPC npc)
    {
        if (npc.Traits.TryGetValue("level", out var levelTrait))
        {
            return levelTrait.AsInt();
        }
        return 1;
    }

    /// <summary>
    /// Gets attribute value from NPC traits.
    /// </summary>
    private int GetAttributeFromTraits(NPC npc, string attributeName, int defaultValue)
    {
        if (npc.Traits.TryGetValue(attributeName, out var trait))
        {
            return trait.AsInt();
        }
        return defaultValue;
    }

    /// <summary>
    /// Equips an item to a party member.
    /// </summary>
    public bool EquipItem(PartyMember member, Item item, out string errorMessage)
    {
        errorMessage = string.Empty;

        // Check if item is in member's inventory
        if (!member.Inventory.Contains(item))
        {
            errorMessage = "Item not in party member's inventory!";
            return false;
        }

        // Equip based on item type
        if (item.Type == ItemType.Weapon)
        {
            member.EquippedWeapon = item;
            _logger.LogInformation("Party member {Name} equipped weapon {Item}", member.Name, item.Name);
            return true;
        }
        else if (item.Type == ItemType.Chest || item.Type == ItemType.Helmet || 
                 item.Type == ItemType.Legs || item.Type == ItemType.Boots ||
                 item.Type == ItemType.Shoulders || item.Type == ItemType.Bracers ||
                 item.Type == ItemType.Gloves || item.Type == ItemType.Belt)
        {
            member.EquippedArmor = item;
            _logger.LogInformation("Party member {Name} equipped armor {Item}", member.Name, item.Name);
            return true;
        }

        errorMessage = "Item cannot be equipped!";
        return false;
    }

    /// <summary>
    /// Gives an item to a party member.
    /// </summary>
    public void GiveItem(PartyMember member, Item item)
    {
        member.Inventory.Add(item);
        _logger.LogInformation("Gave {Item} to party member {Name}", item.Name, member.Name);
    }
}
