using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Media.Imaging;
using ReactiveUI;
using RealmUnbound.Assets;
using RealmUnbound.Assets.Manifest;
using RealmUnbound.Client.Services;
using RealmUnbound.Contracts.Characters;
using RealmUnbound.Contracts.Content;

namespace RealmUnbound.Client.ViewModels;

/// <summary>Wizard view model that drives the guided character creation flow.</summary>
public class CreateCharacterViewModel : ViewModelBase
{
    private readonly ICharacterCreationService _creationService;
    private readonly ContentCache _content;
    private readonly INavigationService _navigation;
    private readonly IAssetStore? _assetStore;

    private Guid? _sessionId;
    private string _name = string.Empty;
    private string _selectedClass = string.Empty;
    private IReadOnlyList<string> _availableClasses = ["Warrior"];
    private bool _isHardcoreCreate;
    private Bitmap? _selectedClassIcon;
    private IReadOnlyList<SpeciesDto> _speciesList = [];
    private IReadOnlyList<string> _availableSpecies = [];
    private string _selectedSpecies = string.Empty;
    private IReadOnlyList<BackgroundDto> _backgroundList = [];
    private IReadOnlyList<string> _availableBackgrounds = [];
    private string _selectedBackground = string.Empty;

    // D&D 5e point-buy cost table: index = stat value - 8
    private static readonly int[] StatCosts = [0, 1, 2, 3, 4, 5, 7, 9];
    private const int PointBuyTotal = 27;
    private const int StatMin = 8;
    private const int StatMax = 15;
    private int _strength = 8;
    private int _dexterity = 8;
    private int _constitution = 8;
    private int _intelligence = 8;
    private int _wisdom = 8;
    private int _charisma = 8;

    // Equipment preferences
    private static readonly IReadOnlyList<string> ArmorTypes =
        ["Light Armor", "Medium Armor", "Heavy Armor", "Unarmored"];
    private static readonly IReadOnlyList<string> WeaponTypes =
        ["Sword", "Axe", "Dagger", "Staff", "Bow", "Mace"];

    private string _selectedArmorType = string.Empty;
    private string _selectedWeaponType = string.Empty;
    private bool _includeShield;

    // Starting location
    private IReadOnlyList<ZoneLocationDto> _locationList = [];
    private IReadOnlyList<string> _availableLocations = [];
    private string _selectedLocation = string.Empty;

    /// <summary>Gets or sets the character name entered by the player.</summary>
    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    /// <summary>Gets or sets the display name of the currently selected class.</summary>
    public string SelectedClass
    {
        get => _selectedClass;
        set => this.RaiseAndSetIfChanged(ref _selectedClass, value);
    }

    /// <summary>Gets the list of available class display names loaded from the content catalog.</summary>
    public IReadOnlyList<string> AvailableClasses
    {
        get => _availableClasses;
        private set => this.RaiseAndSetIfChanged(ref _availableClasses, value);
    }

    /// <summary>Gets or sets a value indicating whether the character should be created in hardcore (permadeath) mode.</summary>
    public bool IsHardcoreCreate
    {
        get => _isHardcoreCreate;
        set => this.RaiseAndSetIfChanged(ref _isHardcoreCreate, value);
    }

    /// <summary>Gets the class badge icon for the currently selected class, or <see langword="null"/> when unavailable.</summary>
    public Bitmap? SelectedClassIcon
    {
        get => _selectedClassIcon;
        private set => this.RaiseAndSetIfChanged(ref _selectedClassIcon, value);
    }

    /// <summary>Gets the list of available species display names loaded from the content catalog.</summary>
    public IReadOnlyList<string> AvailableSpecies
    {
        get => _availableSpecies;
        private set => this.RaiseAndSetIfChanged(ref _availableSpecies, value);
    }

    /// <summary>Gets or sets the display name of the currently selected species.</summary>
    public string SelectedSpecies
    {
        get => _selectedSpecies;
        set => this.RaiseAndSetIfChanged(ref _selectedSpecies, value);
    }

    /// <summary>Gets the list of available background display names loaded from the content catalog.</summary>
    public IReadOnlyList<string> AvailableBackgrounds
    {
        get => _availableBackgrounds;
        private set => this.RaiseAndSetIfChanged(ref _availableBackgrounds, value);
    }

    /// <summary>Gets or sets the display name of the currently selected background.</summary>
    public string SelectedBackground
    {
        get => _selectedBackground;
        set => this.RaiseAndSetIfChanged(ref _selectedBackground, value);
    }

    /// <summary>Gets or sets the Strength base stat value (8–15).</summary>
    public int Strength
    {
        get => _strength;
        set { this.RaiseAndSetIfChanged(ref _strength, value); this.RaisePropertyChanged(nameof(RemainingPoints)); }
    }

    /// <summary>Gets or sets the Dexterity base stat value (8–15).</summary>
    public int Dexterity
    {
        get => _dexterity;
        set { this.RaiseAndSetIfChanged(ref _dexterity, value); this.RaisePropertyChanged(nameof(RemainingPoints)); }
    }

    /// <summary>Gets or sets the Constitution base stat value (8–15).</summary>
    public int Constitution
    {
        get => _constitution;
        set { this.RaiseAndSetIfChanged(ref _constitution, value); this.RaisePropertyChanged(nameof(RemainingPoints)); }
    }

    /// <summary>Gets or sets the Intelligence base stat value (8–15).</summary>
    public int Intelligence
    {
        get => _intelligence;
        set { this.RaiseAndSetIfChanged(ref _intelligence, value); this.RaisePropertyChanged(nameof(RemainingPoints)); }
    }

    /// <summary>Gets or sets the Wisdom base stat value (8–15).</summary>
    public int Wisdom
    {
        get => _wisdom;
        set { this.RaiseAndSetIfChanged(ref _wisdom, value); this.RaisePropertyChanged(nameof(RemainingPoints)); }
    }

    /// <summary>Gets or sets the Charisma base stat value (8–15).</summary>
    public int Charisma
    {
        get => _charisma;
        set { this.RaiseAndSetIfChanged(ref _charisma, value); this.RaisePropertyChanged(nameof(RemainingPoints)); }
    }

    /// <summary>Gets the number of point-buy points remaining.</summary>
    public int RemainingPoints =>
        PointBuyTotal - (StatCosts[Strength - StatMin] + StatCosts[Dexterity - StatMin] +
                         StatCosts[Constitution - StatMin] + StatCosts[Intelligence - StatMin] +
                         StatCosts[Wisdom - StatMin] + StatCosts[Charisma - StatMin]);

    /// <summary>Increases the named stat by one point, if the player has enough point-buy budget.</summary>
    public ReactiveCommand<string, Unit> IncreaseStatCommand { get; }

    /// <summary>Decreases the named stat by one point, down to the minimum of 8.</summary>
    public ReactiveCommand<string, Unit> DecreaseStatCommand { get; }

    /// <summary>Gets the static list of available armor type choices.</summary>
    public IReadOnlyList<string> AvailableArmorTypes => ArmorTypes;

    /// <summary>Gets the static list of available weapon type choices.</summary>
    public IReadOnlyList<string> AvailableWeaponTypes => WeaponTypes;

    /// <summary>Gets or sets the selected armor type display name, or empty string for none.</summary>
    public string SelectedArmorType
    {
        get => _selectedArmorType;
        set => this.RaiseAndSetIfChanged(ref _selectedArmorType, value);
    }

    /// <summary>Gets or sets the selected weapon type display name, or empty string for none.</summary>
    public string SelectedWeaponType
    {
        get => _selectedWeaponType;
        set => this.RaiseAndSetIfChanged(ref _selectedWeaponType, value);
    }

    /// <summary>Gets or sets a value indicating whether to include a shield in starting equipment.</summary>
    public bool IncludeShield
    {
        get => _includeShield;
        set => this.RaiseAndSetIfChanged(ref _includeShield, value);
    }

    /// <summary>Gets the list of available starting location display names.</summary>
    public IReadOnlyList<string> AvailableLocations
    {
        get => _availableLocations;
        private set => this.RaiseAndSetIfChanged(ref _availableLocations, value);
    }

    /// <summary>Gets or sets the display name of the selected starting location.</summary>
    public string SelectedLocation
    {
        get => _selectedLocation;
        set => this.RaiseAndSetIfChanged(ref _selectedLocation, value);
    }

    /// <summary>Confirms all selections, calls the wizard API, and navigates back to character select on success.</summary>
    public ReactiveCommand<Unit, Unit> ConfirmCommand { get; }

    /// <summary>Abandons the current session and navigates back to character select without creating a character.</summary>
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    /// <summary>Initializes a new instance of <see cref="CreateCharacterViewModel"/>.</summary>
    public CreateCharacterViewModel(
        ICharacterCreationService creationService,
        ContentCache content,
        INavigationService navigation,
        IAssetStore? assetStore = null)
    {
        _creationService = creationService;
        _content = content;
        _navigation = navigation;
        _assetStore = assetStore;

        if (assetStore is not null)
            this.WhenAnyValue(x => x.SelectedClass)
                .Subscribe(cls => _ = LoadSelectedClassIconAsync(cls));

        var canConfirm = this.WhenAnyValue(
            x => x.Name, x => x.IsBusy, x => x.SelectedClass,
            (name, busy, cls) => !string.IsNullOrWhiteSpace(name) && !busy && !string.IsNullOrWhiteSpace(cls));

        ConfirmCommand = ReactiveCommand.CreateFromTask(DoFinalizeAsync, canConfirm);
        CancelCommand  = ReactiveCommand.CreateFromTask(DoAbandonAsync);
        IncreaseStatCommand = ReactiveCommand.Create<string>(IncreaseStat);
        DecreaseStatCommand = ReactiveCommand.Create<string>(DecreaseStat);

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            var sessionTask       = _creationService.BeginSessionAsync();
            var classesTask       = _content.GetClassesAsync();
            var speciesTask       = _content.GetSpeciesAsync();
            var backgroundsTask   = _content.GetBackgroundsAsync();
            var locationsTask     = _content.GetZoneLocationsAsync();

            await Task.WhenAll(sessionTask, classesTask, speciesTask, backgroundsTask, locationsTask);

            _sessionId = await sessionTask;
            if (_sessionId is null)
                ErrorMessage = "Could not start creation session. Please check your connection.";

            var classes = await classesTask;
            if (classes.Count > 0)
                AvailableClasses = classes.Select(c => c.DisplayName).ToArray();

            _speciesList = await speciesTask;
            if (_speciesList.Count > 0)
                AvailableSpecies = _speciesList.Select(s => s.DisplayName).ToArray();

            _backgroundList = await backgroundsTask;
            if (_backgroundList.Count > 0)
                AvailableBackgrounds = _backgroundList.Select(b => b.DisplayName).ToArray();

            _locationList = await locationsTask;
            if (_locationList.Count > 0)
                AvailableLocations = _locationList.Select(l => l.DisplayName).ToArray();
        }
        catch
        {
            ErrorMessage = "Could not start creation session. Please check your connection.";
        }
        finally { IsBusy = false; }
    }

    private async Task DoFinalizeAsync()
    {
        if (_sessionId is null)
        {
            ErrorMessage = "No active creation session. Please cancel and try again.";
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            await _creationService.SetNameAsync(_sessionId.Value, Name);
            await _creationService.SetClassAsync(_sessionId.Value, SelectedClass);

            if (!string.IsNullOrEmpty(SelectedSpecies))
            {
                var speciesSlug = _speciesList.FirstOrDefault(s => s.DisplayName == SelectedSpecies)?.Slug ?? SelectedSpecies;
                await _creationService.SetSpeciesAsync(_sessionId.Value, speciesSlug);
            }

            if (!string.IsNullOrEmpty(SelectedBackground))
            {
                var backgroundId = _backgroundList.FirstOrDefault(b => b.DisplayName == SelectedBackground)?.Slug ?? SelectedBackground;
                await _creationService.SetBackgroundAsync(_sessionId.Value, backgroundId);
            }

            var allocations = new Dictionary<string, int>
            {
                ["Strength"] = Strength, ["Dexterity"] = Dexterity, ["Constitution"] = Constitution,
                ["Intelligence"] = Intelligence, ["Wisdom"] = Wisdom, ["Charisma"] = Charisma
            };
            await _creationService.SetAttributesAsync(_sessionId.Value, allocations);

            if (!string.IsNullOrEmpty(SelectedArmorType) || !string.IsNullOrEmpty(SelectedWeaponType) || IncludeShield)
            {
                var armorSlug  = string.IsNullOrEmpty(SelectedArmorType)  ? null : SelectedArmorType.ToLowerInvariant().Replace(' ', '-');
                var weaponSlug = string.IsNullOrEmpty(SelectedWeaponType) ? null : SelectedWeaponType.ToLowerInvariant().Replace(' ', '-');
                await _creationService.SetEquipmentPreferencesAsync(
                    _sessionId.Value, new SetCreationEquipmentPreferencesRequest(armorSlug, weaponSlug, IncludeShield));
            }

            if (!string.IsNullOrEmpty(SelectedLocation))
            {
                var locationId = _locationList.FirstOrDefault(l => l.DisplayName == SelectedLocation)?.Slug ?? SelectedLocation;
                await _creationService.SetLocationAsync(_sessionId.Value, locationId);
            }

            var difficultyMode = IsHardcoreCreate ? "hardcore" : "normal";
            var (character, error) = await _creationService.FinalizeAsync(
                _sessionId.Value, new FinalizeCreationSessionRequest(null, difficultyMode));

            if (character is not null)
            {
                _sessionId = null; // consumed
                _navigation.NavigateTo<CharacterSelectViewModel>();
            }
            else
            {
                ErrorMessage = error?.Message ?? "Failed to create character.";
            }
        }
        finally { IsBusy = false; }
    }

    private async Task DoAbandonAsync()
    {
        if (_sessionId is not null)
        {
            await _creationService.AbandonAsync(_sessionId.Value);
            _sessionId = null;
        }
        _navigation.NavigateTo<CharacterSelectViewModel>();
    }

    private void IncreaseStat(string stat)
    {
        var current = GetStat(stat);
        if (current >= StatMax) return;
        var delta = StatCosts[current - StatMin + 1] - StatCosts[current - StatMin];
        if (RemainingPoints < delta) return;
        SetStat(stat, current + 1);
    }

    private void DecreaseStat(string stat)
    {
        var current = GetStat(stat);
        if (current <= StatMin) return;
        SetStat(stat, current - 1);
    }

    private int GetStat(string stat) => stat switch
    {
        "Strength"     => Strength,
        "Dexterity"    => Dexterity,
        "Constitution" => Constitution,
        "Intelligence" => Intelligence,
        "Wisdom"       => Wisdom,
        "Charisma"     => Charisma,
        _              => StatMin
    };

    private void SetStat(string stat, int value)
    {
        switch (stat)
        {
            case "Strength":     Strength     = value; break;
            case "Dexterity":    Dexterity    = value; break;
            case "Constitution": Constitution = value; break;
            case "Intelligence": Intelligence = value; break;
            case "Wisdom":       Wisdom       = value; break;
            case "Charisma":     Charisma     = value; break;
        }
    }

    private async Task LoadSelectedClassIconAsync(string className)
    {
        var path = ClassAssets.GetPath(className);
        if (path is null) { SelectedClassIcon = null; return; }
        var bytes = await _assetStore!.LoadImageAsync(path);
        SelectedClassIcon = bytes is null ? null : new Bitmap(new MemoryStream(bytes));
    }
}
