namespace RealmEngine.Data.Entities;

/// <summary>
/// Dialogue entry. TypeKey = dialogue type (e.g. "greetings", "farewells", "responses", "styles").
/// Lines are stored in the Stats owned JSON entity.
/// </summary>
public class Dialogue : ContentBase
{
    /// <summary>NPC type or "player" — null means any speaker.</summary>
    public string? Speaker { get; set; }

    /// <summary>Tone, formality, and the list of dialogue lines.</summary>
    public DialogueStats Stats { get; set; } = new();
    /// <summary>Boolean trait flags classifying this dialogue entry.</summary>
    public DialogueTraits Traits { get; set; } = new();
}

/// <summary>Tone, formality, and dialogue line list owned by a Dialogue entry.</summary>
public class DialogueStats
{
    /// <summary>Tone index — e.g. 0 = neutral, 1 = stern, 2 = cheerful.</summary>
    public int? Tone { get; set; }
    /// <summary>Formality index — e.g. 0 = casual, 1 = formal, 2 = archaic.</summary>
    public int? Formality { get; set; }
    /// <summary>The dialogue lines the speaker can say in this entry.</summary>
    public List<string> Lines { get; set; } = [];
}

/// <summary>Boolean trait flags classifying a Dialogue entry's context.</summary>
public class DialogueTraits
{
    /// <summary>True if this dialogue is used in hostile/combat contexts.</summary>
    public bool? Hostile { get; set; }
    /// <summary>True if this dialogue is used in friendly/neutral contexts.</summary>
    public bool? Friendly { get; set; }
    /// <summary>True if this dialogue is associated with a merchant NPC.</summary>
    public bool? Merchant { get; set; }
    /// <summary>True if this dialogue is related to a quest interaction.</summary>
    public bool? QuestRelated { get; set; }
    /// <summary>True if this dialogue is a greeting on first approach.</summary>
    public bool? Greeting { get; set; }
    /// <summary>True if this dialogue is a farewell on departure.</summary>
    public bool? Farewell { get; set; }
}
