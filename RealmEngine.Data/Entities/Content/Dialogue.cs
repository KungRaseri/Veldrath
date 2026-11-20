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

/// <summary>Tone, formality, dialogue line list, and the choices the entry offers.</summary>
public class DialogueStats
{
    /// <summary>Tone index — e.g. 0 = neutral, 1 = stern, 2 = cheerful.</summary>
    public int? Tone { get; set; }
    /// <summary>Formality index — e.g. 0 = casual, 1 = formal, 2 = archaic.</summary>
    public int? Formality { get; set; }
    /// <summary>The dialogue lines the speaker can say in this entry.</summary>
    public List<string> Lines { get; set; } = [];

    /// <summary>
    /// The choices this entry offers, in authored order.
    /// </summary>
    /// <remarks>
    /// Empty means the entry is <em>spoken</em>, not answered: one of <see cref="Lines"/> is read out and the
    /// conversation ends there. That is what every seeded dialogue does today, so an existing row gains options by
    /// being edited, not by being migrated.
    /// </remarks>
    public List<DialogueOption> Options { get; set; } = [];
}

/// <summary>
/// One choice a player can make in a conversation, with what it asks and what it changes.
/// </summary>
/// <remarks>
/// <para>
/// Requirements and grants are authored as text blocks (<see cref="Requires"/>, <see cref="Grants"/>) and parsed by
/// <see cref="Content.DialogueOptionGrammar"/>. A nested list of lists is neither editable in the Foundry form nor
/// supported by the owned-JSON mapping this entity uses, so the authored column is text and the grammar is the
/// contract — the validator and the runtime parse it with the same code.
/// </para>
/// <para>
/// A conversation is a tree of options, not a general graph: <see cref="NextSlug"/> may point at one other dialogue
/// entry, which is enough for a two-step exchange. Cycles are refused by the validator.
/// </para>
/// </remarks>
public class DialogueOption
{
    /// <summary>
    /// Stable identifier the client sends back when the option is chosen, e.g. <c>ask-about-woods</c>.
    /// </summary>
    /// <remarks>
    /// Authored rather than generated: a client that reconnects mid-conversation, or a saved choice, refers to the
    /// option by a string that must not change when the entry is re-ordered.
    /// </remarks>
    public string Key { get; set; } = string.Empty;

    /// <summary>The player-facing choice text, e.g. "What happened in the wood?".</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>What the speaker says once this option is chosen.</summary>
    public string Response { get; set; } = string.Empty;

    /// <summary>Slug of the dialogue entry to continue into, or null to end the conversation.</summary>
    public string? NextSlug { get; set; }

    /// <summary>
    /// Whether this option may be chosen only once per character.
    /// </summary>
    /// <remarks>
    /// Unset means "decide by grants": an option that grants an item, gold or a quest is one-shot, because otherwise
    /// it is a money printer. An option that grants nothing can be repeated freely. Set it explicitly only to make a
    /// grant repeatable, which the validator then reports as a warning rather than blocking.
    /// </remarks>
    public bool? OneShot { get; set; }

    /// <summary>
    /// What the character must have before this option is available — one entry per line.
    /// </summary>
    /// <remarks>See <see cref="Content.DialogueOptionGrammar"/> for the grammar and the closed vocabulary.</remarks>
    public string? Requires { get; set; }

    /// <summary>
    /// What choosing this option changes — one entry per line.
    /// </summary>
    /// <remarks>See <see cref="Content.DialogueOptionGrammar"/> for the grammar and the closed vocabulary.</remarks>
    public string? Grants { get; set; }
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
