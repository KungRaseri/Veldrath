using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive;
using RealmUnbound.Client.Services;

namespace RealmUnbound.Client.ViewModels;

public class CharacterSelectViewModel : ViewModelBase
{
    private readonly ICharacterService _characters;
    private readonly IServerConnectionService _connection;
    private readonly INavigationService _navigation;
    private readonly GameViewModel _gameVm;

    private bool _isBusy;
    private bool _isCreating;
    private string _errorMessage = string.Empty;
    private string _errorDetails = string.Empty;
    private string _newCharacterName = string.Empty;
    private string _selectedClass = string.Empty;

    // Hub subscriptions — stored so they can be disposed before re-subscribing on retry
    private IDisposable? _zoneEnteredSub;
    private IDisposable? _playerEnteredSub;
    private IDisposable? _playerLeftSub;
    private IDisposable? _hubErrorSub;

    public ObservableCollection<CharacterDto> Characters { get; } = [];

    public bool IsBusy
    {
        get => _isBusy;
        set => this.RaiseAndSetIfChanged(ref _isBusy, value);
    }

    public bool IsCreating
    {
        get => _isCreating;
        set
        {
            this.RaiseAndSetIfChanged(ref _isCreating, value);
            this.RaisePropertyChanged(nameof(PanelTitle));
        }
    }

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

    public string NewCharacterName
    {
        get => _newCharacterName;
        set => this.RaiseAndSetIfChanged(ref _newCharacterName, value);
    }

    public string SelectedClass
    {
        get => _selectedClass;
        set => this.RaiseAndSetIfChanged(ref _selectedClass, value);
    }

    public IReadOnlyList<string> AvailableClasses { get; } =
        ["Fighter", "Mage", "Rogue", "Cleric", "Ranger", "Paladin"];

    /// <summary>Drives the top bar title — changes when switching between list and create panels.</summary>
    public string PanelTitle => IsCreating ? "New Character" : "Select Your Character";

    public string ServerUrl { get; set; } = "http://localhost:8080";

    public ReactiveCommand<CharacterDto, Unit> SelectCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowCreateCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCreateCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateCommand { get; }
    public ReactiveCommand<CharacterDto, Unit> DeleteCommand { get; }
    public ReactiveCommand<Unit, Unit> LogoutCommand { get; }

    public CharacterSelectViewModel(
        ICharacterService characters,
        IServerConnectionService connection,
        INavigationService navigation,
        GameViewModel gameVm,
        IAuthService auth)
    {
        _characters = characters;
        _connection = connection;
        _navigation = navigation;
        _gameVm = gameVm;

        var canCreate = this.WhenAnyValue(
            x => x.NewCharacterName, x => x.IsBusy, x => x.SelectedClass,
            (name, busy, cls) => !string.IsNullOrWhiteSpace(name) && !busy && !string.IsNullOrWhiteSpace(cls));

        SelectCommand       = ReactiveCommand.CreateFromTask<CharacterDto>(DoSelectAsync);
        ShowCreateCommand   = ReactiveCommand.Create(() => { IsCreating = true; ErrorMessage = string.Empty; ErrorDetails = string.Empty; });
        CancelCreateCommand = ReactiveCommand.Create(() => { IsCreating = false; NewCharacterName = string.Empty; SelectedClass = string.Empty; ErrorMessage = string.Empty; ErrorDetails = string.Empty; });
        CreateCommand       = ReactiveCommand.CreateFromTask(DoCreateAsync, canCreate);
        DeleteCommand       = ReactiveCommand.CreateFromTask<CharacterDto>(DoDeleteAsync);
        LogoutCommand       = ReactiveCommand.CreateFromTask(async () =>
        {
            await auth.LogoutAsync();
            navigation.NavigateTo<MainMenuViewModel>();
        });

        // Load on construction
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        ErrorDetails = string.Empty;
        try
        {
            var list = await _characters.GetCharactersAsync();
            Characters.Clear();
            foreach (var c in list.OrderBy(x => x.SlotIndex))
                Characters.Add(c);
        }
        finally { IsBusy = false; }
    }

    private async Task DoSelectAsync(CharacterDto character)
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        ErrorDetails = string.Empty;
        try
        {
            var zoneId = character.CurrentZoneId.Length > 0 ? character.CurrentZoneId : "starting-zone";

            await _connection.ConnectAsync(ServerUrl);

            // Dispose previous subscriptions to prevent duplicate handlers on retry
            _zoneEnteredSub?.Dispose();
            _playerEnteredSub?.Dispose();
            _playerLeftSub?.Dispose();
            _hubErrorSub?.Dispose();

            // Subscribe to zone hub events before sending commands so no events are missed
            _zoneEnteredSub = _connection.On<ZoneEnteredPayload>("ZoneEntered", payload =>
            {
                _gameVm.SetOccupants(payload.Occupants.Select(o => o.CharacterName));
                _navigation.NavigateTo<GameViewModel>();
            });
            _playerEnteredSub = _connection.On<PlayerEventPayload>("PlayerEntered", payload =>
                _gameVm.OnPlayerEntered(payload.CharacterName));
            _playerLeftSub = _connection.On<PlayerEventPayload>("PlayerLeft", payload =>
                _gameVm.OnPlayerLeft(payload.CharacterName));
            _hubErrorSub = _connection.On<string>("Error", message =>
            {
                ErrorMessage = message;
                IsBusy = false;
            });

            await _connection.SendCommandAsync<object>("SelectCharacter", character.Id);
            await _gameVm.InitializeAsync(character.Name, zoneId);
            await _connection.SendCommandAsync<object>("EnterZone", zoneId);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to connect: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    // ── Payload shapes (matching server hub broadcasts) ────────────────────────
    internal record OccupantInfo(Guid CharacterId, string CharacterName, DateTimeOffset EnteredAt);
    internal record ZoneEnteredPayload(string Id, string Name, string Description, string ZoneType, IEnumerable<OccupantInfo> Occupants);
    internal record PlayerEventPayload(string CharacterName);

    private async Task DoCreateAsync()
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        ErrorDetails = string.Empty;
        try
        {
            var (character, error) = await _characters.CreateCharacterAsync(NewCharacterName, SelectedClass);
            if (character is not null)
            {
                Characters.Add(character);
                NewCharacterName = string.Empty;
                SelectedClass = string.Empty;
                IsCreating = false;
            }
            else
            {
                ErrorMessage = error?.Message ?? "Failed to create character.";
                ErrorDetails = error?.Details ?? string.Empty;
            }
        }
        finally { IsBusy = false; }
    }

    private async Task DoDeleteAsync(CharacterDto character)
    {
        var error = await _characters.DeleteCharacterAsync(character.Id);
        if (error is null)
            Characters.Remove(character);
        else
        {
            ErrorMessage = error.Message;
            ErrorDetails = error.Details ?? string.Empty;
        }
    }
}
