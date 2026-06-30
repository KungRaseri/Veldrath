using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Veldrath.Web.Services;
using Veldrath.Contracts.Content;

namespace Veldrath.Web.Components.Pages.Game;

/// <summary>
/// Character creation wizard — guides the player through class selection, naming,
/// and confirmation.  Redirects to <c>/Login</c> if not authenticated.
/// </summary>
public sealed partial class CreateCharacter
{
    [Inject] private AuthStateService Auth { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;
    [Inject] private VeldrathApiClient Api { get; set; } = null!;
    [Inject] private ILogger<CreateCharacter> Logger { get; set; } = null!;

    private List<ActorClassDto> _availableClasses = [];
    private ActorClassDto? _selectedClass;
    private string _characterName = string.Empty;
    private string? _nameError;
    private bool _nameChecking;
    private bool? _nameAvailable;
    private bool _isLoading = true;
    private bool _isCreating;
    private string? _errorMessage;
    private int _currentStep = 1;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        if (!Auth.IsLoggedIn)
        {
            Navigation.NavigateTo("/Login");
            return;
        }

        try
        {
            _availableClasses = await Api.GetClassesAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load character classes.");
            _errorMessage = "Failed to load available classes. Please try again.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void SelectClass(ActorClassDto cls)
    {
        _selectedClass = cls;
    }

    private void GoToStep1() => _currentStep = 1;
    private void GoToStep2() => _currentStep = 2;
    private void GoToStep3() => _currentStep = 3;

    private async Task OnNameChanged()
    {
        _nameError = null;
        _nameAvailable = null;

        var trimmed = _characterName?.Trim() ?? string.Empty;
        if (trimmed.Length < 2)
        {
            _nameError = "Name must be at least 2 characters.";
            return;
        }
        if (trimmed.Length > 30)
        {
            _nameError = "Name must be at most 30 characters.";
            return;
        }

        _nameChecking = true;

        try
        {
            var result = await Api.CheckCharacterNameAsync(trimmed);
            if (result is null)
            {
                _nameError = "Could not verify name availability. Please try again.";
            }
            else if (!result.Available)
            {
                _nameError = result.Error ?? "Name is not available.";
                _nameAvailable = false;
            }
            else
            {
                _nameAvailable = true;
                _characterName = trimmed;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to check character name availability.");
            _nameError = "Could not verify name availability. Please try again.";
        }
        finally
        {
            _nameChecking = false;
        }
    }

    private async Task ConfirmCreate()
    {
        if (_selectedClass is null || string.IsNullOrWhiteSpace(_characterName))
            return;

        _isCreating = true;
        _errorMessage = null;

        try
        {
            var result = await Api.CreateCharacterAsync(_characterName, _selectedClass.DisplayName);

            if (result is null)
            {
                _errorMessage = "Failed to create character. The name may already be taken.";
            }
            else
            {
                Navigation.NavigateTo("/Game/CharacterSelect");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to create character.");
            _errorMessage = "Failed to create character. Please try again.";
        }
        finally
        {
            _isCreating = false;
        }
    }

    private void ResetWizard()
    {
        _errorMessage = null;
        _currentStep = 1;
    }
}
