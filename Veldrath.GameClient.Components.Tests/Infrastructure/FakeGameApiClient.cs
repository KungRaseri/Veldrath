using Veldrath.Contracts.Characters;
using Veldrath.Contracts.Content;
using Veldrath.GameClient.Core.Abstractions;

namespace Veldrath.GameClient.Components.Tests.Infrastructure;

/// <summary>
/// Configurable stub for <see cref="IGameApiClient"/>.
/// Default behaviour: returns empty character list and a predefined set of classes.
/// Set properties to simulate specific API responses in tests.
/// </summary>
public sealed class FakeGameApiClient : IGameApiClient
{
    /// <summary>Gets or sets the character list returned by <see cref="GetCharactersAsync"/>.</summary>
    public List<CharacterDto> Characters { get; set; } = [];

    /// <summary>Gets or sets the result of <see cref="CreateCharacterAsync"/>.</summary>
    public CharacterDto? CreatedCharacter { get; set; } = new CharacterDto(
        Guid.NewGuid(), 1, "TestChar", "Warrior", 1, 0, DateTimeOffset.UtcNow, "fenwick-crossing");

    /// <summary>Gets or sets the result of <see cref="CheckCharacterNameAsync"/>.</summary>
    public CheckNameAvailabilityResponse? NameCheckResult { get; set; } = new CheckNameAvailabilityResponse(true, null);

    /// <summary>Gets or sets the class list returned by <see cref="GetClassesAsync"/>.</summary>
    public List<ActorClassDto> Classes { get; set; } =
    [
        new("warrior", "Warrior", "warriors", 10, "strength", 50),
        new("mage", "Mage", "casters", 6, "intelligence", 40),
        new("rogue", "Rogue", "rogues", 8, "dexterity", 30),
    ];

    /// <summary>Gets the number of times <see cref="GetCharactersAsync"/> was called.</summary>
    public int GetCharactersCallCount { get; private set; }

    /// <summary>Gets the number of times <see cref="CheckCharacterNameAsync"/> was called.</summary>
    public int CheckNameCallCount { get; private set; }

    /// <summary>Gets the last name passed to <see cref="CheckCharacterNameAsync"/>.</summary>
    public string? LastCheckedName { get; private set; }

    /// <summary>Gets the last parameters passed to <see cref="CreateCharacterAsync"/>.</summary>
    public (string Name, string ClassName, string DifficultyMode)? LastCreateParams { get; private set; }

    /// <inheritdoc />
    public Task<List<CharacterDto>> GetCharactersAsync(CancellationToken ct = default)
    {
        GetCharactersCallCount++;
        return Task.FromResult(new List<CharacterDto>(Characters));
    }

    /// <inheritdoc />
    public Task<CharacterDto?> CreateCharacterAsync(string name, string className, string difficultyMode = "normal", CancellationToken ct = default)
    {
        LastCreateParams = (name, className, difficultyMode);
        return Task.FromResult(CreatedCharacter);
    }

    /// <inheritdoc />
    public Task<CheckNameAvailabilityResponse?> CheckCharacterNameAsync(string name, CancellationToken ct = default)
    {
        CheckNameCallCount++;
        LastCheckedName = name;
        return Task.FromResult(NameCheckResult);
    }

    /// <inheritdoc />
    public Task<List<ActorClassDto>> GetClassesAsync(CancellationToken ct = default)
        => Task.FromResult(new List<ActorClassDto>(Classes));
}
