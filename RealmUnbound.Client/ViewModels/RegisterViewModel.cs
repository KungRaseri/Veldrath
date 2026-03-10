using ReactiveUI;
using System.Reactive;
using RealmUnbound.Client.Services;

namespace RealmUnbound.Client.ViewModels;

public class RegisterViewModel : ViewModelBase
{
    private readonly IAuthService _auth;
    private readonly INavigationService _navigation;

    private string _email = string.Empty;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;

    public string Email
    {
        get => _email;
        set => this.RaiseAndSetIfChanged(ref _email, value);
    }

    public string Username
    {
        get => _username;
        set => this.RaiseAndSetIfChanged(ref _username, value);
    }

    public string Password
    {
        get => _password;
        set => this.RaiseAndSetIfChanged(ref _password, value);
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set => this.RaiseAndSetIfChanged(ref _confirmPassword, value);
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

    public ReactiveCommand<Unit, Unit> RegisterCommand { get; }
    public ReactiveCommand<Unit, Unit> BackCommand { get; }

    public RegisterViewModel(IAuthService auth, INavigationService navigation)
    {
        _auth = auth;
        _navigation = navigation;

        var canSubmit = this.WhenAnyValue(
            x => x.Email, x => x.Username, x => x.Password, x => x.ConfirmPassword, x => x.IsBusy,
            (email, u, p, cp, busy) =>
                !string.IsNullOrWhiteSpace(email) &&
                !string.IsNullOrWhiteSpace(u) &&
                !string.IsNullOrWhiteSpace(p) &&
                p == cp &&
                !busy);

        RegisterCommand = ReactiveCommand.CreateFromTask(DoRegisterAsync, canSubmit);
        BackCommand     = ReactiveCommand.Create(() => navigation.NavigateTo<MainMenuViewModel>());
    }

    private async Task DoRegisterAsync()
    {
        if (Password != ConfirmPassword)
        {
            ErrorMessage = "Passwords do not match.";
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var (response, error) = await _auth.RegisterAsync(Email, Username, Password);
            if (response is not null)
                _navigation.NavigateTo<CharacterSelectViewModel>();
            else
                ErrorMessage = error ?? "Registration failed.";
        }
        finally { IsBusy = false; }
    }
}
