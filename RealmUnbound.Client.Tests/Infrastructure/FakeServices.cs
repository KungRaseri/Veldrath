using RealmUnbound.Client.Services;
using RealmUnbound.Client.ViewModels;
using RealmUnbound.Contracts.Auth;
using RealmUnbound.Contracts.Characters;

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
        => Task.FromResult(default(TResult));

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
