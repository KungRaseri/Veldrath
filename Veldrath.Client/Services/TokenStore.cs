using ReactiveUI;

namespace Veldrath.Client.Services;

/// <summary>
/// Singleton holding the current authenticated user's tokens.
/// Both <see cref="IAuthService"/> and <see cref="IServerConnectionService"/> read from here.
/// </summary>
public class TokenStore : ReactiveObject
{
    private string?                  _accessToken;
    private string?                  _refreshToken;
    private string?                  _username;
    private Guid?                    _accountId;
    private DateTimeOffset?          _accessTokenExpiry;
    private bool                     _isCurator;
    private IReadOnlyList<string>    _roles       = [];
    private IReadOnlyList<string>    _permissions = [];
    private Guid?                    _sessionId;

    public string? AccessToken
    {
        get => _accessToken;
        set => this.RaiseAndSetIfChanged(ref _accessToken, value);
    }

    public string? RefreshToken
    {
        get => _refreshToken;
        set => this.RaiseAndSetIfChanged(ref _refreshToken, value);
    }

    public string? Username
    {
        get => _username;
        set => this.RaiseAndSetIfChanged(ref _username, value);
    }

    public Guid? AccountId
    {
        get => _accountId;
        set => this.RaiseAndSetIfChanged(ref _accountId, value);
    }

    public DateTimeOffset? AccessTokenExpiry
    {
        get => _accessTokenExpiry;
        set => this.RaiseAndSetIfChanged(ref _accessTokenExpiry, value);
    }

    public bool IsCurator
    {
        get => _isCurator;
        set => this.RaiseAndSetIfChanged(ref _isCurator, value);
    }

    /// <summary>Gets or sets the role names held by the authenticated user.</summary>
    public IReadOnlyList<string> Roles
    {
        get => _roles;
        set => this.RaiseAndSetIfChanged(ref _roles, value);
    }

    /// <summary>Gets or sets the effective permission set for the authenticated user.</summary>
    public IReadOnlyList<string> Permissions
    {
        get => _permissions;
        set => this.RaiseAndSetIfChanged(ref _permissions, value);
    }

    /// <summary>Gets or sets the refresh-token session identifier.</summary>
    public Guid? SessionId
    {
        get => _sessionId;
        set => this.RaiseAndSetIfChanged(ref _sessionId, value);
    }

    public bool IsAuthenticated => AccessToken is not null;

    /// <summary>True when the access token expires within the next two minutes.</summary>
    public bool IsExpiringSoon =>
        AccessTokenExpiry.HasValue && (AccessTokenExpiry.Value - DateTimeOffset.UtcNow) < TimeSpan.FromMinutes(2);

    /// <summary>
    /// Atomically stores all auth fields. Supporting fields are set before <see cref="AccessToken"/> so
    /// observers of <c>AccessToken</c> (e.g. the persistence subscription) see a fully-populated store.
    /// </summary>
    public void Set(string accessToken, string refreshToken, string username, Guid accountId,
                    DateTimeOffset? expiry = null, bool isCurator = false,
                    IReadOnlyList<string>? roles = null, IReadOnlyList<string>? permissions = null,
                    Guid? sessionId = null)
    {
        RefreshToken       = refreshToken;
        Username           = username;
        AccountId          = accountId;
        AccessTokenExpiry  = expiry;
        IsCurator          = isCurator;
        Roles              = roles       ?? [];
        Permissions        = permissions ?? [];
        SessionId          = sessionId;
        AccessToken        = accessToken;
    }

    /// <summary>Resets all auth fields to their default (unauthenticated) state.</summary>
    public void Clear()
    {
        AccessToken       = null;
        RefreshToken      = null;
        Username          = null;
        AccountId         = null;
        AccessTokenExpiry = null;
        IsCurator         = false;
        Roles             = [];
        Permissions       = [];
        SessionId         = null;
    }
}
