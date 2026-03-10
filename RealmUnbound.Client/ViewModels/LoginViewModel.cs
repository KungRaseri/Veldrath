using ReactiveUI;
using System.Reactive;
using RealmUnbound.Client.Services;

namespace RealmUnbound.Client.ViewModels;

public class LoginViewModel : ViewModelBase
{
    private readonly IAuthService _auth;
    private readonly INavigationService _navigation;
    private readonly SessionStore _sessionStore;

    private string _email = string.Empty;
    private string _password = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;
    private bool _rememberEmail;

    public string Email
    {
        get => _email;
        set => this.RaiseAndSetIfChanged(ref _email, value);
    }

    public string Password
    {
        get => _password;
        set => this.RaiseAndSetIfChanged(ref _password, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => this.RaiseAndSetIfChanged(ref _isBusy, value);
    }

    /// <summary>When true the email is saved to disk so it can be pre-filled next launch.</summary>
    public bool RememberEmail
    {
        get => _rememberEmail;
        set => this.RaiseAndSetIfChanged(ref _rememberEmail, value);
    }

    public ReactiveCommand<Unit, Unit> LoginCommand { get; }
    public ReactiveCommand<Unit, Unit> BackCommand { get; }

    public LoginViewModel(IAuthService auth, INavigationService navigation, SessionStore sessionStore)
    {
        _auth = auth;
        _navigation = navigation;
        _sessionStore = sessionStore;

        // Pre-fill email if the user previously chose to save it
        if (sessionStore.HasSavedEmail)
        {
            Email = sessionStore.SavedEmail!;
            RememberEmail = true;
        }

        var canSubmit = this.WhenAnyValue(
            x => x.Email, x => x.Password, x => x.IsBusy,
            (e, p, busy) => !string.IsNullOrWhiteSpace(e) && !string.IsNullOrWhiteSpace(p) && !busy);

        LoginCommand = ReactiveCommand.CreateFromTask(DoLoginAsync, canSubmit);
        BackCommand  = ReactiveCommand.Create(() => navigation.NavigateTo<MainMenuViewModel>());
    }

    private async Task DoLoginAsync()
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var (response, error) = await _auth.LoginAsync(Email, Password);
            if (response is not null)
            {
                if (RememberEmail)
                    _sessionStore.SaveEmail(Email);
                else
                    _sessionStore.ClearEmail();

                _navigation.NavigateTo<CharacterSelectViewModel>();
            }
            else
            {
                ErrorMessage = error ?? "Login failed.";
            }
        }
        finally { IsBusy = false; }
    }


}
