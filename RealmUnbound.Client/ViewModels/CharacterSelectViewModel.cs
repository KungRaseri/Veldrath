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
    private string _newCharacterName = string.Empty;

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

    public string NewCharacterName
    {
        get => _newCharacterName;
        set => this.RaiseAndSetIfChanged(ref _newCharacterName, value);
    }

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
            x => x.NewCharacterName, x => x.IsBusy,
            (name, busy) => !string.IsNullOrWhiteSpace(name) && !busy);

        SelectCommand       = ReactiveCommand.CreateFromTask<CharacterDto>(DoSelectAsync);
        ShowCreateCommand   = ReactiveCommand.Create(() => { IsCreating = true; ErrorMessage = string.Empty; });
        CancelCreateCommand = ReactiveCommand.Create(() => { IsCreating = false; NewCharacterName = string.Empty; ErrorMessage = string.Empty; });
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
        try
        {
            var zoneId = character.CurrentZoneId.Length > 0 ? character.CurrentZoneId : "starting-zone";

            await _connection.ConnectAsync(ServerUrl);

            // Subscribe to zone hub events before sending commands so no events are missed
            _connection.On<ZoneEnteredPayload>("ZoneEntered", payload =>
            {
                _gameVm.SetOccupants(payload.Occupants);
                _navigation.NavigateTo<GameViewModel>();
            });
            _connection.On<PlayerEventPayload>("PlayerEntered", payload =>
                _gameVm.OnPlayerEntered(payload.CharacterName));
            _connection.On<PlayerEventPayload>("PlayerLeft", payload =>
                _gameVm.OnPlayerLeft(payload.CharacterName));

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
    internal record ZoneEnteredPayload(string ZoneId, IEnumerable<string> Occupants);
    internal record PlayerEventPayload(string CharacterName);

    private async Task DoCreateAsync()
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var (character, error) = await _characters.CreateCharacterAsync(NewCharacterName, "Fighter");
            if (character is not null)
            {
                Characters.Add(character);
                NewCharacterName = string.Empty;
                IsCreating = false;
            }
            else
            {
                ErrorMessage = error ?? "Failed to create character.";
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
            ErrorMessage = error;
    }
}
