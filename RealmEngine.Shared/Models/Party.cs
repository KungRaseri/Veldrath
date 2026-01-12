namespace RealmEngine.Shared.Models;

/// <summary>
/// Represents a party of characters (player + recruited allies).
/// </summary>
public class Party
{
    /// <summary>
    /// Gets or sets the player character (party leader).
    /// </summary>
    public Character Leader { get; set; } = null!;

    /// <summary>
    /// Gets or sets the list of recruited party members (NPCs).
    /// </summary>
    public List<PartyMember> Members { get; set; } = new();

    /// <summary>
    /// Gets the maximum party size (leader + members).
    /// </summary>
    public int MaxSize { get; init; } = 4;

    /// <summary>
    /// Gets the current party size (including leader).
    /// </summary>
    public int CurrentSize => 1 + Members.Count;

    /// <summary>
    /// Gets whether the party is full.
    /// </summary>
    public bool IsFull => CurrentSize >= MaxSize;

    /// <summary>
    /// Gets all alive party members (for combat).
    /// </summary>
    public List<PartyMember> AliveMembers => Members.Where(m => m.IsAlive).ToList();

    /// <summary>
    /// Gets the total party level (for scaling difficulty).
    /// </summary>
    public int TotalLevel => Leader.Level + Members.Sum(m => m.Level);

    /// <summary>
    /// Adds a member to the party if there's space.
    /// </summary>
    public bool AddMember(PartyMember member)
    {
        if (IsFull) return false;
        
        Members.Add(member);
        return true;
    }

    /// <summary>
    /// Removes a member from the party.
    /// </summary>
    public bool RemoveMember(string memberId)
    {
        var member = Members.FirstOrDefault(m => m.Id == memberId);
        if (member == null) return false;
        
        Members.Remove(member);
        return true;
    }

    /// <summary>
    /// Finds a party member by ID.
    /// </summary>
    public PartyMember? FindMember(string memberId) => 
        Members.FirstOrDefault(m => m.Id == memberId);
}
