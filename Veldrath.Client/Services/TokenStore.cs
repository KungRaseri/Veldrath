using ReactiveUI;

namespace Veldrath.Client.Services;

/// <summary>
/// Singleton holding the current authenticated user's tokens.
/// Both <see cref="IAuthService"/> and <see cref="IServerConnectionService"/> read from here.
/// </summary>
public class TokenStore : ReactiveObject
{
    private string?         _accessToken;
    private string?         _refreshToken;
    private string?         _username;
    private Guid?           _accountId;
    private DateTimeOffset? _accessTokenExpiry;
    private bool            _isCurator;

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

    public bool IsAuthenticated => AccessToken is not null;

    /// <summary>True when the access token expires within the next two minutes.</summary>
    public bool IsExpiringSoon =>
        AccessTokenExpiry.HasValue && (AccessTokenExpiry.Value - DateTimeOffset.UtcNow) < TimeSpan.FromMinutes(2);

    public void Set(string accessToken, string refreshToken, string username, Guid accountId,
                    DateTimeOffset? expiry = null, bool isCurator = false)
    {
        // Set all supporting fields first so that observers of AccessToken (e.g. the
        // token-persistence subscription in App.axaml.cs) see a fully-populated store
        // when AccessToken raises PropertyChanged.
        RefreshToken       = refreshToken;
        Username           = username;
        AccountId          = accountId;
        AccessTokenExpiry  = expiry;
        IsCurator          = isCurator;
        AccessToken        = accessToken;
    }

    public void Clear()
    {
        AccessToken       = null;
        RefreshToken      = null;
        Username          = null;
        AccountId         = null;
        AccessTokenExpiry = null;
        IsCurator         = false;
    }
}
