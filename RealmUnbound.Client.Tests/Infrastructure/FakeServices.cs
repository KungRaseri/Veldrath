using Microsoft.Extensions.Logging.Abstractions;
using RealmUnbound.Assets;
using RealmUnbound.Client.Services;
using RealmUnbound.Client.ViewModels;
using RealmUnbound.Contracts.Auth;
using RealmUnbound.Contracts.Characters;
using RealmUnbound.Contracts.Content;
using RealmUnbound.Contracts.Zones;

namespace RealmUnbound.Client.Tests.Infrastructure;

// ── Auth service stub ─────────────────────────────────────────────────────────

/// <summary>
/// Configurable stub for <see cref="IAuthService"/>.
/// Default behaviour: returns success with a fake token.
/// </summary>
public class FakeAuthService : IAuthService
{
    public (AuthResponse? Response, AppError? Error) RegisterResult { get; set; } =
        (new AuthResponse("access", "refresh", DateTimeOffset.UtcNow.AddMinutes(15), Guid.NewGuid(), "TestUser"), null);

    public (AuthResponse? Response, AppError? Error) LoginResult { get; set; } =
        (new AuthResponse("access", "refresh", DateTimeOffset.UtcNow.AddMinutes(15), Guid.NewGuid(), "TestUser"), null);

    public bool RefreshResult { get; set; } = true;

    public int RegisterCallCount { get; private set; }
    public int LoginCallCount    { get; private set; }
    public int RefreshCallCount  { get; private set; }
    public int LogoutCallCount   { get; private set; }

    public Task<(AuthResponse? Response, AppError? Error)> RegisterAsync(string email, string username, string password)
    {
        RegisterCallCount++;
        return Task.FromResult(RegisterResult);
    }

    public Task<(AuthResponse? Response, AppError? Error)> LoginAsync(string email, string password)
    {
        LoginCallCount++;
        return Task.FromResult(LoginResult);
    }

    public Task<(AuthResponse? Response, AppError? Error)> LoginExternalAsync(
        string provider, CancellationToken ct = default)
        => Task.FromResult(LoginResult);

    public Task<bool> RefreshAsync()
    {
        RefreshCallCount++;
        return Task.FromResult(RefreshResult);
    }

    public Task LogoutAsync()
    {
        LogoutCallCount++;
        return Task.CompletedTask;
    }
}

// ── Character service stub ────────────────────────────────────────────────────

public class FakeCharacterService : ICharacterService
{
    public List<CharacterDto> Characters { get; set; } = [];

    public (CharacterDto? Character, AppError? Error) CreateResult { get; set; } =
        (new CharacterDto(Guid.NewGuid(), 1, "TestChar", "Warrior", 1, 0, DateTimeOffset.UtcNow, "starting-zone"), null);

    public AppError? DeleteError { get; set; } = null;

    public int GetCallCount    { get; private set; }
    public int CreateCallCount { get; private set; }
    public int DeleteCallCount { get; private set; }

    public Task<List<CharacterDto>> GetCharactersAsync()
    {
        GetCallCount++;
        return Task.FromResult(new List<CharacterDto>(Characters));
    }

    public Task<(CharacterDto? Character, AppError? Error)> CreateCharacterAsync(CreateCharacterRequest request)
    {
        CreateCallCount++;
        if (CreateResult.Character is not null)
        {
            var c = CreateResult.Character with { Name = request.Name };
            return Task.FromResult<(CharacterDto?, AppError?)>((c, null));
        }
        return Task.FromResult<(CharacterDto?, AppError?)>((null, CreateResult.Error));
    }

    public Task<AppError?> DeleteCharacterAsync(Guid id)
    {
        DeleteCallCount++;
        return Task.FromResult(DeleteError);
    }
}

// ── Navigation service stub ───────────────────────────────────────────────────

public class FakeNavigationService : INavigationService
{
    public List<Type> NavigationLog { get; } = [];
    public ViewModelBase? LastNavigatedTo { get; private set; }

    public event Action<ViewModelBase>? CurrentPageChanged;

    public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase
    {
        NavigationLog.Add(typeof(TViewModel));
        CurrentPageChanged?.Invoke(LastNavigatedTo!);
    }

    public void NavigateTo(ViewModelBase viewModel)
    {
        LastNavigatedTo = viewModel;
        NavigationLog.Add(viewModel.GetType());
        CurrentPageChanged?.Invoke(viewModel);
    }
}

// ── Server connection service stub ────────────────────────────────────────────

public class FakeServerConnectionService : IServerConnectionService
{
    private ConnectionState _state = ConnectionState.Disconnected;
    private readonly Dictionary<string, object> _handlers = new();

    public ConnectionState State => _state;
    public bool ConnectShouldThrow { get; set; }

    /// <summary>Records every (method, arg) pair sent via <see cref="SendCommandAsync{TResult}"/>.</summary>
    public List<(string Method, object? Arg)> SentCommands { get; } = [];

    /// <summary>When set, returned by <see cref="SendCommandAsync{TResult}(string)"/> for <c>GetActiveCharacters</c>.</summary>
    public IEnumerable<Guid>? ActiveCharacterIds { get; set; }

    public event Action<ConnectionState>? StateChanged;

    public Task ConnectAsync(string serverUrl, CancellationToken cancellationToken = default)
    {
        if (ConnectShouldThrow)
            throw new InvalidOperationException("Connection failed");
        _state = ConnectionState.Connected;
        StateChanged?.Invoke(_state);
        return Task.CompletedTask;
    }

    public IDisposable On<T>(string method, Action<T> handler)
    {
        _handlers[method] = handler;
        return DummyDisposable.Instance;
    }

    public IDisposable On(string method, Action handler)
    {
        _handlers[method] = handler;
        return DummyDisposable.Instance;
    }

    /// <summary>Invokes a previously registered handler as if the server sent the event.</summary>
    public void FireEvent<T>(string method, T payload)
    {
        if (_handlers.TryGetValue(method, out var h))
            ((Action<T>)h)(payload);
    }

    /// <summary>Invokes a previously registered parameterless handler as if the server sent the event.</summary>
    public void FireEvent(string method)
    {
        if (_handlers.TryGetValue(method, out var h))
            ((Action)h)();
    }

    public Task SendCommandAsync(string method)
    {
        SentCommands.Add((method, null));
        return Task.CompletedTask;
    }

    public Task<TResult?> SendCommandAsync<TResult>(string method)
    {
        SentCommands.Add((method, null));
        if (method == "GetActiveCharacters" && ActiveCharacterIds is TResult result)
            return Task.FromResult<TResult?>(result);
        return Task.FromResult(default(TResult));
    }

    public Task<TResult?> SendCommandAsync<TResult>(string method, object command)
    {
        SentCommands.Add((method, command));
        return Task.FromResult(default(TResult));
    }

    public Task DisconnectAsync()
    {
        _state = ConnectionState.Disconnected;
        StateChanged?.Invoke(_state);
        return Task.CompletedTask;
    }

    private sealed class DummyDisposable : IDisposable
    {
        public static readonly DummyDisposable Instance = new();
        public void Dispose() { }
    }
}

// ── Session store helper ────────────────────────────────────────────────────────

public static class SessionStoreFactory
{
    /// <summary>Creates a <see cref="SessionStore"/> backed by a NullLogger and an isolated temp file for use in unit tests.</summary>
    public static SessionStore Create() =>
        new SessionStore(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SessionStore>.Instance,
            Path.Combine(Path.GetTempPath(), $"realm-test-{Guid.NewGuid()}.json"));
}

// ── Zone service stub ─────────────────────────────────────────────────────────

public class FakeZoneService : IZoneService
{
    public ZoneDto? ZoneToReturn { get; set; } =
        new ZoneDto("fenwick-crossing", "Fenwick Crossing", "The starting town of Fenwick Crossing.",
            "Town", 1, 50, true, 0, null, HasInn: true, HasMerchant: true);

    public List<ZoneDto> Zones { get; set; } = [];
    public List<RegionDto> Regions { get; set; } = [];
    public List<WorldDto> Worlds { get; set; } = [];

    public Task<List<ZoneDto>> GetZonesAsync()
        => Task.FromResult(new List<ZoneDto>(Zones));

    public Task<ZoneDto?> GetZoneAsync(string zoneId)
        => Task.FromResult(ZoneToReturn);

    public Task<List<ZoneDto>> GetZonesByRegionAsync(string regionId)
        => Task.FromResult(Zones.Where(z => z.RegionId == regionId).ToList());

    public Task<List<RegionDto>> GetRegionsAsync()
        => Task.FromResult(new List<RegionDto>(Regions));

    public Task<RegionDto?> GetRegionAsync(string regionId)
        => Task.FromResult(Regions.FirstOrDefault(r => r.Id == regionId));

    public Task<List<RegionDto>> GetRegionConnectionsAsync(string regionId)
        => Task.FromResult<List<RegionDto>>([]);

    public Task<List<WorldDto>> GetWorldsAsync()
        => Task.FromResult(new List<WorldDto>(Worlds));

    public Task<WorldDto?> GetWorldAsync(string worldId)
        => Task.FromResult(Worlds.FirstOrDefault(w => w.Id == worldId));
}

// ── Content service stub ──────────────────────────────────────────────────────

/// <summary>
/// Configurable stub for <see cref="IContentService"/>.
/// Default behaviour: returns empty lists for all catalog queries.
/// Set <see cref="Classes"/> to non-empty values to simulate a populated class catalog.
/// </summary>
public class FakeContentService : IContentService
{
    /// <summary>Gets or sets the list of classes returned by <see cref="GetClassesAsync"/>.</summary>
    public List<ActorClassDto> Classes { get; set; } =
    [
        new("warrior", "Warrior", "warriors", 10, "strength",     50),
        new("mage",    "Mage",    "casters",   6, "intelligence", 40),
        new("rogue",   "Rogue",   "rogues",    8, "dexterity",    30),
    ];

    public Task<List<PowerDto>>     GetAbilitiesAsync()            => Task.FromResult(new List<PowerDto>());
    public Task<PowerDto?>           GetAbilityAsync(string slug)   => Task.FromResult<PowerDto?>(null);
    public Task<List<EnemyDto>>      GetEnemiesAsync()              => Task.FromResult(new List<EnemyDto>());
    public Task<EnemyDto?>           GetEnemyAsync(string slug)     => Task.FromResult<EnemyDto?>(null);
    public Task<List<NpcDto>>        GetNpcsAsync()                 => Task.FromResult(new List<NpcDto>());
    public Task<NpcDto?>             GetNpcAsync(string slug)       => Task.FromResult<NpcDto?>(null);
    public Task<List<QuestDto>>      GetQuestsAsync()               => Task.FromResult(new List<QuestDto>());
    public Task<QuestDto?>           GetQuestAsync(string slug)     => Task.FromResult<QuestDto?>(null);
    public Task<List<RecipeDto>>     GetRecipesAsync()              => Task.FromResult(new List<RecipeDto>());
    public Task<RecipeDto?>          GetRecipeAsync(string slug)    => Task.FromResult<RecipeDto?>(null);
    public Task<List<LootTableDto>>  GetLootTablesAsync()           => Task.FromResult(new List<LootTableDto>());
    public Task<LootTableDto?>       GetLootTableAsync(string slug) => Task.FromResult<LootTableDto?>(null);
    public Task<List<PowerDto>>      GetSpellsAsync()               => Task.FromResult(new List<PowerDto>());
    public Task<PowerDto?>           GetSpellAsync(string slug)     => Task.FromResult<PowerDto?>(null);
    public Task<List<ActorClassDto>> GetClassesAsync()              => Task.FromResult(new List<ActorClassDto>(Classes));
    public Task<ActorClassDto?>      GetClassAsync(string slug)     => Task.FromResult(Classes.FirstOrDefault(c => c.Slug == slug));
    public Task<List<SpeciesDto>>    GetSpeciesAsync()              => Task.FromResult(new List<SpeciesDto>());
    public Task<SpeciesDto?>         GetSpeciesAsync(string slug)   => Task.FromResult<SpeciesDto?>(null);
    public Task<List<BackgroundDto>> GetBackgroundsAsync()          => Task.FromResult(new List<BackgroundDto>());
    public Task<BackgroundDto?>      GetBackgroundAsync(string slug)=> Task.FromResult<BackgroundDto?>(null);
    public Task<List<SkillDto>>      GetSkillsAsync()               => Task.FromResult(new List<SkillDto>());
    public Task<SkillDto?>           GetSkillAsync(string slug)     => Task.FromResult<SkillDto?>(null);
    public Task<List<OrganizationDto>>    GetOrganizationsAsync()            => Task.FromResult(new List<OrganizationDto>());
    public Task<OrganizationDto?>         GetOrganizationAsync(string slug)  => Task.FromResult<OrganizationDto?>(null);
    public Task<List<WorldLocationDto>>   GetWorldLocationsAsync()           => Task.FromResult(new List<WorldLocationDto>());
    public Task<WorldLocationDto?>        GetWorldLocationAsync(string slug) => Task.FromResult<WorldLocationDto?>(null);
    public Task<List<DialogueDto>>        GetDialoguesAsync()                => Task.FromResult(new List<DialogueDto>());
    public Task<DialogueDto?>             GetDialogueAsync(string slug)      => Task.FromResult<DialogueDto?>(null);
    public Task<List<ActorInstanceDto>>   GetActorInstancesAsync()           => Task.FromResult(new List<ActorInstanceDto>());
    public Task<ActorInstanceDto?>        GetActorInstanceAsync(string slug) => Task.FromResult<ActorInstanceDto?>(null);
    public Task<List<MaterialPropertyDto>> GetMaterialPropertiesAsync()              => Task.FromResult(new List<MaterialPropertyDto>());
    public Task<MaterialPropertyDto?>      GetMaterialPropertyAsync(string slug)     => Task.FromResult<MaterialPropertyDto?>(null);
    public Task<List<TraitDefinitionDto>> GetTraitDefinitionsAsync()         => Task.FromResult(new List<TraitDefinitionDto>());
    public Task<TraitDefinitionDto?>      GetTraitDefinitionAsync(string key)=> Task.FromResult<TraitDefinitionDto?>(null);
    public Task<List<ItemDto>>            GetItemsAsync()                    => Task.FromResult(new List<ItemDto>());
    public Task<ItemDto?>                 GetItemAsync(string slug)          => Task.FromResult<ItemDto?>(null);
    public Task<List<EnchantmentDto>>     GetEnchantmentsAsync()             => Task.FromResult(new List<EnchantmentDto>());
    public Task<EnchantmentDto?>          GetEnchantmentAsync(string slug)   => Task.FromResult<EnchantmentDto?>(null);
    public Task<List<MaterialDto>>        GetMaterialsAsync()                => Task.FromResult(new List<MaterialDto>());
    public Task<MaterialDto?>             GetMaterialAsync(string slug)      => Task.FromResult<MaterialDto?>(null);
}

/// <summary>Factory that builds a <see cref="ContentCache"/> backed by a <see cref="FakeContentService"/>.</summary>
public static class FakeContentCache
{
    /// <summary>Creates a <see cref="ContentCache"/> instance wired to a <see cref="FakeContentService"/>.</summary>
    /// <param name="service">Optional custom service; defaults to a new <see cref="FakeContentService"/>.</param>
    public static ContentCache Create(FakeContentService? service = null) =>
        new(service ?? new FakeContentService(), NullLogger<ContentCache>.Instance);
}

// ── Audio player stub ─────────────────────────────────────────────────────────

/// <summary>Configurable stub for <see cref="IAudioPlayer"/>. Records calls for test assertions.</summary>
public class FakeAudioPlayer : IAudioPlayer
{
    /// <summary>Gets the last music volume set via <see cref="SetMusicVolume"/>.</summary>
    public int  MusicVolume { get; private set; } = 80;

    /// <summary>Gets the last SFX volume set via <see cref="SetSfxVolume"/>.</summary>
    public int  SfxVolume   { get; private set; } = 100;

    /// <summary>Gets whether the audio is currently muted.</summary>
    public bool Muted       { get; private set; }

    /// <inheritdoc />
    public Task PlayMusicAsync(string filePath) => Task.CompletedTask;

    /// <inheritdoc />
    public void PlaySfx(string filePath) { }

    /// <inheritdoc />
    public void StopMusic() { }

    /// <inheritdoc />
    public void SetMusicVolume(int volume) => MusicVolume = volume;

    /// <inheritdoc />
    public void SetSfxVolume(int volume) => SfxVolume = volume;

    /// <inheritdoc />
    public void SetMuted(bool muted) => Muted = muted;

    /// <inheritdoc />
    public void Dispose() => GC.SuppressFinalize(this);
}

// ── Asset store stub ──────────────────────────────────────────────────────────

/// <summary>
/// No-op stub for <see cref="IAssetStore"/>.
/// Returns <see langword="null"/> for images, <see langword="null"/> for audio paths,
/// empty sequences for category listings, and <see langword="false"/> for existence checks.
/// </summary>
public class FakeAssetStore : IAssetStore
{
    /// <inheritdoc />
    public Task<byte[]?> LoadImageAsync(string relativePath, CancellationToken cancellationToken = default)
        => Task.FromResult<byte[]?>(null);

    /// <inheritdoc />
    public string? ResolveAudioPath(string relativePath) => null;

    /// <inheritdoc />
    public IEnumerable<string> GetPaths(AssetCategory category) => [];

    /// <inheritdoc />
    public bool Exists(string relativePath) => false;
}
