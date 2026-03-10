using ReactiveUI;

namespace RealmUnbound.Client.Services;

/// <summary>
/// Singleton holding the current authenticated user's tokens.
/// Both <see cref="IAuthService"/> and <see cref="IServerConnectionService"/> read from here.
/// </summary>
public class TokenStore : ReactiveObject
{
    private string? _accessToken;
    private string? _refreshToken;
    private string? _username;
    private Guid? _accountId;

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

    public bool IsAuthenticated => AccessToken is not null;

    public void Set(string accessToken, string refreshToken, string username, Guid accountId)
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        Username = username;
        AccountId = accountId;
    }

    public void Clear()
    {
        AccessToken = null;
        RefreshToken = null;
        Username = null;
        AccountId = null;
    }
}
