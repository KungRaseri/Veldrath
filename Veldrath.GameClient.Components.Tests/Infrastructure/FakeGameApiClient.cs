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

    // ── Session-based creation — configurable stubs ──────────────────────────────

    /// <summary>Gets or sets the result of <see cref="BeginCreationSessionAsync"/>.</summary>
    public BeginCreationSessionResponse? BeginSessionResult { get; set; } =
        new BeginCreationSessionResponse(Guid.NewGuid(), true);

    /// <summary>Gets or sets the result of <see cref="GetCreationPreviewAsync"/>.</summary>
    public CharacterPreviewDto? CreationPreview { get; set; }

    /// <summary>Gets or sets the result of <see cref="FinalizeCreationSessionAsync"/>.</summary>
    public CharacterDto? FinalizedCharacter { get; set; }

    /// <summary>Gets or sets the default response for all set-creation-choice operations.</summary>
    public SetCreationChoiceResponse? SetChoiceResult { get; set; } =
        new SetCreationChoiceResponse(true, "Ok");

    /// <summary>Gets or sets the result of <see cref="SetCreationAttributesAsync"/>.</summary>
    public AllocateCreationAttributesResponse? SetAttributesResult { get; set; } =
        new AllocateCreationAttributesResponse(true, "Attributes allocated.", 0);

    /// <summary>Gets or sets the species list returned by <see cref="GetSpeciesAsync"/>.</summary>
    public List<SpeciesDto> Species { get; set; } = [];

    /// <summary>Gets or sets the background list returned by <see cref="GetBackgroundsAsync"/>.</summary>
    public List<BackgroundDto> Backgrounds { get; set; } = [];

    // ── Call tracking ────────────────────────────────────────────────────────────

    /// <summary>Gets the number of times <see cref="BeginCreationSessionAsync"/> was called.</summary>
    public int BeginSessionCallCount { get; private set; }

    /// <summary>Gets the number of times <see cref="AbandonCreationSessionAsync"/> was called.</summary>
    public int AbandonSessionCallCount { get; private set; }

    /// <summary>Gets the last session ID passed to <see cref="SetCreationClassAsync"/>.</summary>
    public Guid? LastSetClassSessionId { get; private set; }

    /// <summary>Gets the last class name passed to <see cref="SetCreationClassAsync"/>.</summary>
    public string? LastSetClassName { get; private set; }

    /// <inheritdoc />
    public Task<List<CharacterDto>> GetCharactersAsync(CancellationToken ct = default)
    {
        GetCharactersCallCount++;
        return Task.FromResult(new List<CharacterDto>(Characters));
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

    // ── Session-based creation methods ───────────────────────────────────────────

    /// <inheritdoc />
    public Task<BeginCreationSessionResponse?> BeginCreationSessionAsync(CancellationToken ct = default)
    {
        BeginSessionCallCount++;
        return Task.FromResult(BeginSessionResult);
    }

    /// <inheritdoc />
    public Task<CharacterPreviewDto?> GetCreationPreviewAsync(Guid sessionId, CancellationToken ct = default)
        => Task.FromResult(CreationPreview);

    /// <inheritdoc />
    public Task<CharacterDto?> FinalizeCreationSessionAsync(Guid sessionId, FinalizeCreationSessionRequest request, CancellationToken ct = default)
        => Task.FromResult(FinalizedCharacter);

    /// <inheritdoc />
    public Task AbandonCreationSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        AbandonSessionCallCount++;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<SetCreationChoiceResponse?> SetCreationNameAsync(Guid sessionId, string characterName, CancellationToken ct = default)
        => Task.FromResult(SetChoiceResult);

    /// <inheritdoc />
    public Task<SetCreationChoiceResponse?> SetCreationClassAsync(Guid sessionId, string className, CancellationToken ct = default)
    {
        LastSetClassSessionId = sessionId;
        LastSetClassName = className;
        return Task.FromResult(SetChoiceResult);
    }

    /// <inheritdoc />
    public Task<SetCreationChoiceResponse?> SetCreationSpeciesAsync(Guid sessionId, string speciesSlug, CancellationToken ct = default)
        => Task.FromResult(SetChoiceResult);

    /// <inheritdoc />
    public Task<SetCreationChoiceResponse?> SetCreationBackgroundAsync(Guid sessionId, string backgroundId, CancellationToken ct = default)
        => Task.FromResult(SetChoiceResult);

    /// <inheritdoc />
    public Task<AllocateCreationAttributesResponse?> SetCreationAttributesAsync(Guid sessionId, Dictionary<string, int> allocations, CancellationToken ct = default)
        => Task.FromResult(SetAttributesResult);

    /// <inheritdoc />
    public Task<SetCreationChoiceResponse?> SetCreationEquipmentPreferencesAsync(Guid sessionId, string? armorType, string? weaponType, bool includeShield, CancellationToken ct = default)
        => Task.FromResult(SetChoiceResult);

    /// <inheritdoc />
    public Task<SetCreationChoiceResponse?> SetCreationLocationAsync(Guid sessionId, string locationId, CancellationToken ct = default)
        => Task.FromResult(SetChoiceResult);

    // ── Content lookups ──────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<List<SpeciesDto>> GetSpeciesAsync(CancellationToken ct = default)
        => Task.FromResult(new List<SpeciesDto>(Species));

    /// <inheritdoc />
    public Task<List<BackgroundDto>> GetBackgroundsAsync(CancellationToken ct = default)
        => Task.FromResult(new List<BackgroundDto>(Backgrounds));
}
