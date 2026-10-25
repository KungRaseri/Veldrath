using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Progression.Commands;

/// <summary>
/// Command to initialize all skills for a new character.
/// </summary>
public record InitializeCharacterSkillsCommand : IRequest<InitializeCharacterSkillsResult>
{
    /// <summary>Gets the character to initialize skills for.</summary>
    public required Character Character { get; init; }
}

/// <summary>
/// Result of initializing character skills.
/// </summary>
public record InitializeCharacterSkillsResult
{
    /// <summary>Gets the number of skills initialized.</summary>
    public int SkillsInitialized { get; init; }
    /// <summary>Gets the list of initialized skill IDs.</summary>
    public List<string> SkillIds { get; init; } = new();
}
