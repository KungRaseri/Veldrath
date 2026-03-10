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

    private bool _isBusy;
    private string _errorMessage = string.Empty;
    private string _newCharacterName = string.Empty;
    private string _newCharacterClass = "Fighter";

    public ObservableCollection<CharacterDto> Characters { get; } = [];

    public bool IsBusy
    {
        get => _isBusy;
        set => this.RaiseAndSetIfChanged(ref _isBusy, value);
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

    public string NewCharacterClass
    {
        get => _newCharacterClass;
        set => this.RaiseAndSetIfChanged(ref _newCharacterClass, value);
    }

    public string ServerUrl { get; set; } = "http://localhost:8080";

    public ReactiveCommand<CharacterDto, Unit> SelectCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateCommand { get; }
    public ReactiveCommand<CharacterDto, Unit> DeleteCommand { get; }

    public CharacterSelectViewModel(
        ICharacterService characters,
        IServerConnectionService connection,
        INavigationService navigation)
    {
        _characters = characters;
        _connection = connection;
        _navigation = navigation;

        var canCreate = this.WhenAnyValue(
            x => x.NewCharacterName, x => x.IsBusy,
            (name, busy) => !string.IsNullOrWhiteSpace(name) && !busy);

        SelectCommand = ReactiveCommand.CreateFromTask<CharacterDto>(DoSelectAsync);
        CreateCommand = ReactiveCommand.CreateFromTask(DoCreateAsync, canCreate);
        DeleteCommand = ReactiveCommand.CreateFromTask<CharacterDto>(DoDeleteAsync);

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
            await _connection.ConnectAsync(ServerUrl);
            await _connection.SendCommandAsync<object>("SelectCharacter", character.Id);
            // TODO: navigate to game view once it exists
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to connect: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    private async Task DoCreateAsync()
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var (character, error) = await _characters.CreateCharacterAsync(NewCharacterName, NewCharacterClass);
            if (character is not null)
            {
                Characters.Add(character);
                NewCharacterName = string.Empty;
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
