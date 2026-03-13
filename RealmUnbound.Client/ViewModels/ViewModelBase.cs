using ReactiveUI;

namespace RealmUnbound.Client.ViewModels;

public abstract class ViewModelBase : ReactiveObject
{
    private string _errorMessage = string.Empty;
    private string _errorDetails = string.Empty;
    private bool _isBusy;

    public string ErrorMessage
    {
        get => _errorMessage;
        set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }

    /// <summary>Optional technical detail surfaced when the server returns extra context.</summary>
    public string ErrorDetails
    {
        get => _errorDetails;
        set => this.RaiseAndSetIfChanged(ref _errorDetails, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => this.RaiseAndSetIfChanged(ref _isBusy, value);
    }

    protected void ClearError()
    {
        ErrorMessage = string.Empty;
        ErrorDetails = string.Empty;
    }
}
