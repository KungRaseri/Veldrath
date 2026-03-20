using Microsoft.Extensions.Logging.Abstractions;
using RealmUnbound.Client.Services;
using RealmUnbound.Client.ViewModels;
using RealmUnbound.Contracts.Auth;
using RealmUnbound.Contracts.Characters;
using RealmUnbound.Contracts.Content;

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
        (new CharacterDto(Guid.NewGuid(), 1, "TestChar", "@classes/warriors:fighter", 1, 0, DateTimeOffset.UtcNow, "starting-zone"), null);

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

    /// <summary>Invokes a previously registered handler as if the server sent the event.</summary>
    public void FireEvent<T>(string method, T payload)
    {
        if (_handlers.TryGetValue(method, out var h))
            ((Action<T>)h)(payload);
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
        new ZoneDto("starting-zone", "The Starting Vale", "A peaceful valley for beginners.",
            "outdoor", 1, 50, true, 0);

    public List<ZoneDto> Zones { get; set; } = [];

    public Task<List<ZoneDto>> GetZonesAsync()
        => Task.FromResult(new List<ZoneDto>(Zones));

    public Task<ZoneDto?> GetZoneAsync(string zoneId)
        => Task.FromResult(ZoneToReturn);
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
        new("@classes/warriors:fighter", "Fighter", "class", 10, "Strength",     10),
        new("@classes/mages:mage",       "Mage",    "class",  6, "Intelligence", 10),
        new("@classes/rogues:rogue",     "Rogue",   "class",  8, "Dexterity",    10),
    ];

    public Task<List<AbilityDto>>    GetAbilitiesAsync()            => Task.FromResult(new List<AbilityDto>());
    public Task<AbilityDto?>         GetAbilityAsync(string slug)   => Task.FromResult<AbilityDto?>(null);
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
    public Task<List<SpellDto>>      GetSpellsAsync()               => Task.FromResult(new List<SpellDto>());
    public Task<SpellDto?>           GetSpellAsync(string slug)     => Task.FromResult<SpellDto?>(null);
    public Task<List<ActorClassDto>> GetClassesAsync()              => Task.FromResult(new List<ActorClassDto>(Classes));
    public Task<ActorClassDto?>      GetClassAsync(string slug)     => Task.FromResult(Classes.FirstOrDefault(c => c.Slug == slug));
    public Task<List<SpeciesDto>>    GetSpeciesAsync()              => Task.FromResult(new List<SpeciesDto>());
    public Task<SpeciesDto?>         GetSpeciesAsync(string slug)   => Task.FromResult<SpeciesDto?>(null);
    public Task<List<BackgroundDto>> GetBackgroundsAsync()          => Task.FromResult(new List<BackgroundDto>());
    public Task<BackgroundDto?>      GetBackgroundAsync(string slug)=> Task.FromResult<BackgroundDto?>(null);
    public Task<List<SkillDto>>      GetSkillsAsync()               => Task.FromResult(new List<SkillDto>());
    public Task<SkillDto?>           GetSkillAsync(string slug)     => Task.FromResult<SkillDto?>(null);
}

/// <summary>Factory that builds a <see cref="ContentCache"/> backed by a <see cref="FakeContentService"/>.</summary>
public static class FakeContentCache
{
    /// <summary>Creates a <see cref="ContentCache"/> instance wired to a <see cref="FakeContentService"/>.</summary>
    /// <param name="service">Optional custom service; defaults to a new <see cref="FakeContentService"/>.</param>
    public static ContentCache Create(FakeContentService? service = null) =>
        new(service ?? new FakeContentService(), NullLogger<ContentCache>.Instance);
}
