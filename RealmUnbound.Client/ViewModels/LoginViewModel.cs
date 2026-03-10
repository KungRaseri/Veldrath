using ReactiveUI;
using System.Reactive;
using RealmUnbound.Client.Services;

namespace RealmUnbound.Client.ViewModels;

public class LoginViewModel : ViewModelBase
{
    private readonly IAuthService _auth;
    private readonly INavigationService _navigation;

    private string _username = string.Empty;
    private string _password = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;

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

    public ReactiveCommand<Unit, Unit> LoginCommand { get; }
    public ReactiveCommand<Unit, Unit> BackCommand { get; }

    public LoginViewModel(IAuthService auth, INavigationService navigation)
    {
        _auth = auth;
        _navigation = navigation;

        var canSubmit = this.WhenAnyValue(
            x => x.Username, x => x.Password, x => x.IsBusy,
            (u, p, busy) => !string.IsNullOrWhiteSpace(u) && !string.IsNullOrWhiteSpace(p) && !busy);

        LoginCommand = ReactiveCommand.CreateFromTask(DoLoginAsync, canSubmit);
        BackCommand  = ReactiveCommand.Create(() => navigation.NavigateTo<MainMenuViewModel>());
    }

    private async Task DoLoginAsync()
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var (response, error) = await _auth.LoginAsync(Username, Password);
            if (response is not null)
                _navigation.NavigateTo<CharacterSelectViewModel>();
            else
                ErrorMessage = error ?? "Login failed.";
        }
        finally { IsBusy = false; }
    }


}
