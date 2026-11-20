namespace Veldrath.Server.Data.Entities;

/// <summary>
/// Records that a character has already taken a one-shot dialogue option.
/// </summary>
/// <remarks>
/// <para>
/// An authored option may grant an item, gold or a quest. Without a record of what has been taken, that option is a
/// money printer: the player clicks it, the pipe closes, and they click it again. The dialogue content already says
/// which options are one-shot (<c>DialogueOption.OneShot</c>, defaulting to "yes, if it grants anything"), so all
/// that was missing was somewhere durable to remember the answer.
/// </para>
/// <para>
/// The row key <em>is</em> the fact — character, speaker, option — so a second attempt is a duplicate insert rather
/// than a state to reconcile, and the choice survives a re-login by construction.
/// </para>
/// </remarks>
public class CharacterDialogueChoice
{
    /// <summary>Gets or sets the character who made the choice.</summary>
    public Guid CharacterId { get; set; }

    /// <summary>Gets or sets the speaker's archetype slug, which is the dialogue's speaker key.</summary>
    public string SpeakerSlug { get; set; } = string.Empty;

    /// <summary>Gets or sets the authored option key, stable across re-ordering of the entry.</summary>
    public string OptionKey { get; set; } = string.Empty;

    /// <summary>Gets or sets when the option was taken (UTC).</summary>
    public DateTimeOffset ChosenAt { get; set; }

    /// <summary>Gets or sets the character row, so the choices are removed with it.</summary>
    public Character? Character { get; set; }
}
