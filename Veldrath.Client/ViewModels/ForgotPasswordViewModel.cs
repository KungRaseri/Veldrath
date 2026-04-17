using ReactiveUI;
using System.Reactive;
using Veldrath.Client.Services;

namespace Veldrath.Client.ViewModels;

/// <summary>ViewModel for the Forgot Password screen. Sends a reset email and returns the user to Login.</summary>
public class ForgotPasswordViewModel : ViewModelBase
{
    private readonly IAuthService        _auth;
    private readonly INavigationService  _navigation;

    private string _email = string.Empty;
    private bool   _emailSent;

    /// <summary>The email address to send the password-reset link to.</summary>
    public string Email
    {
        get => _email;
        set => this.RaiseAndSetIfChanged(ref _email, value);
    }

    /// <summary>True after a reset request has been submitted successfully, showing the confirmation banner.</summary>
    public bool EmailSent
    {
        get => _emailSent;
        set => this.RaiseAndSetIfChanged(ref _emailSent, value);
    }

    /// <summary>Sends the password-reset email.</summary>
    public ReactiveCommand<Unit, Unit> SendResetCommand { get; }

    /// <summary>Navigates back to the Login screen.</summary>
    public ReactiveCommand<Unit, Unit> BackCommand { get; }

    /// <summary>Initializes a new instance of <see cref="ForgotPasswordViewModel"/>.</summary>
    public ForgotPasswordViewModel(IAuthService auth, INavigationService navigation)
    {
        _auth       = auth;
        _navigation = navigation;

        var canSend = this.WhenAnyValue(
            x => x.Email, x => x.IsBusy, x => x.EmailSent,
            (email, busy, sent) => !string.IsNullOrWhiteSpace(email) && !busy && !sent);

        SendResetCommand = ReactiveCommand.CreateFromTask(DoSendResetAsync, canSend);
        BackCommand      = ReactiveCommand.Create(() => navigation.NavigateTo<LoginViewModel>());
    }

    internal async Task DoSendResetAsync()
    {
        IsBusy = true;
        ClearError();
        try
        {
            await _auth.ForgotPasswordAsync(Email);
            EmailSent = true;
        }
        catch
        {
            // ForgotPasswordAsync swallows exceptions internally; this is a belt-and-braces guard.
            EmailSent = true;
        }
        finally { IsBusy = false; }
    }
}
