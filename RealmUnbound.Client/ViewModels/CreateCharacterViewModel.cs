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

    private int _currentStepIndex;
    private string _stepError = string.Empty;
    private string _nameValidationError = string.Empty;
    private CharacterPreviewDto? _characterPreview;

    private static readonly System.Text.RegularExpressions.Regex NameLettersPattern =
        new(@"^[a-zA-Z]+$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static string? ValidateNameFormat(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var trimmed = name.Trim();
        if (trimmed.Length < 2) return "Name must be at least 2 characters.";
        if (trimmed.Length > 20) return "Name must be at most 20 characters.";
        if (!NameLettersPattern.IsMatch(trimmed)) return "Name may only contain letters.";
        return null;
    }

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
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedSpecies, value);
            this.RaisePropertyChanged(nameof(SelectedSpeciesDescription));
        }
    }

    /// <summary>Gets the lore description of the currently selected species, or an empty string when none is selected.</summary>
    public string SelectedSpeciesDescription =>
        _speciesList.FirstOrDefault(s => s.DisplayName == SelectedSpecies)?.Description ?? string.Empty;

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
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedBackground, value);
            this.RaisePropertyChanged(nameof(SelectedBackgroundDescription));
        }
    }

    /// <summary>Gets the lore description of the currently selected background, or an empty string when none is selected.</summary>
    public string SelectedBackgroundDescription =>
        _backgroundList.FirstOrDefault(b => b.DisplayName == SelectedBackground)?.Description ?? string.Empty;

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

    /// <summary>Gets the ordered titles for each creation step.</summary>
    public static IReadOnlyList<string> StepTitles { get; } = ["Name", "Class", "Species", "Background", "Attributes", "Equipment", "Review"];

    /// <summary>Gets the total number of creation steps.</summary>
    public static int StepCount => StepTitles.Count;

    /// <summary>Gets the zero-based index of the currently displayed creation step.</summary>
    public int CurrentStepIndex
    {
        get => _currentStepIndex;
        private set
        {
            this.RaiseAndSetIfChanged(ref _currentStepIndex, value);
            this.RaisePropertyChanged(nameof(NextButtonLabel));
            this.RaisePropertyChanged(nameof(CurrentStepNumber));
            this.RaisePropertyChanged(nameof(StepLabel));
        }
    }

    /// <summary>Gets the one-based display index of the current step (1 … StepCount).</summary>
    public int CurrentStepNumber => CurrentStepIndex + 1;

    /// <summary>Gets the formatted step counter label, e.g. "Step 1 of 7".</summary>
    public string StepLabel => $"Step {CurrentStepNumber} of {StepCount}";

    /// <summary>Gets the per-step error message; cleared when advancing to a new step.</summary>
    public string StepError
    {
        get => _stepError;
        private set => this.RaiseAndSetIfChanged(ref _stepError, value);
    }

    /// <summary>Gets the inline validation error for the character name field, or empty string when the name is valid.</summary>
    public string NameValidationError
    {
        get => _nameValidationError;
        private set => this.RaiseAndSetIfChanged(ref _nameValidationError, value);
    }

    /// <summary>Gets the live character preview, populated once a class has been confirmed.</summary>
    public CharacterPreviewDto? CharacterPreview
    {
        get => _characterPreview;
        private set => this.RaiseAndSetIfChanged(ref _characterPreview, value);
    }

    /// <summary>Gets the label for the primary action button; changes to "Create Character" on the review step.</summary>
    public string NextButtonLabel => CurrentStepIndex == StepTitles.Count - 1 ? "Create Character" : "Next";

    /// <summary>Advances to the next creation step, persisting the current step's choices to the server.</summary>
    public ReactiveCommand<Unit, Unit> NextCommand { get; }

    /// <summary>Returns to the previous creation step without making any API calls.</summary>
    public ReactiveCommand<Unit, Unit> BackCommand { get; }

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

        var canNext = this.WhenAnyValue(
            x => x.CurrentStepIndex, x => x.Name, x => x.SelectedClass,
            x => x.SelectedSpecies, x => x.SelectedBackground, x => x.IsBusy,
            x => x.NameValidationError, x => x.RemainingPoints,
            (step, name, cls, species, background, busy, nameError, remaining) => !busy && step switch
            {
                0 => !string.IsNullOrWhiteSpace(name) && string.IsNullOrEmpty(nameError),
                1 => !string.IsNullOrWhiteSpace(cls),
                2 => !string.IsNullOrWhiteSpace(species),
                3 => !string.IsNullOrWhiteSpace(background),
                4 => remaining == 0,
                _ => true
            });

        var canBack = this.WhenAnyValue(
            x => x.CurrentStepIndex, x => x.IsBusy,
            (step, busy) => step > 0 && !busy);

        NextCommand         = ReactiveCommand.CreateFromTask(DoNextStepAsync, canNext);
        BackCommand         = ReactiveCommand.Create(() => { CurrentStepIndex--; StepError = string.Empty; }, canBack);
        CancelCommand       = ReactiveCommand.CreateFromTask(DoAbandonAsync);
        IncreaseStatCommand = ReactiveCommand.Create<string>(IncreaseStat);
        DecreaseStatCommand = ReactiveCommand.Create<string>(DecreaseStat);

        this.WhenAnyValue(
                x => x.Strength, x => x.Dexterity, x => x.Constitution,
                x => x.Intelligence, x => x.Wisdom, x => x.Charisma)
            .Subscribe(ignored => { _ = RefreshPreviewAsync(); });

        this.WhenAnyValue(x => x.SelectedSpecies)
            .Skip(1)
            .Subscribe(species => { _ = EagerSetSpeciesAsync(species); });

        this.WhenAnyValue(x => x.SelectedBackground)
            .Skip(1)
            .Subscribe(background => { _ = EagerSetBackgroundAsync(background); });

        this.WhenAnyValue(x => x.Name)
            .Throttle(TimeSpan.FromMilliseconds(400), RxApp.TaskpoolScheduler)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Select(name => Observable.FromAsync(() => ComputeNameErrorAsync(name)))
            .Switch()
            .Subscribe(err => NameValidationError = err);

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            var sessionTask     = _creationService.BeginSessionAsync();
            var classesTask     = _content.GetClassesAsync();
            var speciesTask     = _content.GetSpeciesAsync();
            var backgroundsTask = _content.GetBackgroundsAsync();

            await Task.WhenAll(sessionTask, classesTask, speciesTask, backgroundsTask);

            _sessionId = await sessionTask;
            if (_sessionId is null)
                ErrorMessage = "Could not start creation session. Please check your connection.";

            var classes = await classesTask;
            if (classes.Count > 0)
            {
                AvailableClasses = classes.Select(c => c.DisplayName).ToArray();
                SelectedClass = AvailableClasses[0];
            }

            _speciesList = await speciesTask;
            if (_speciesList.Count > 0)
            {
                AvailableSpecies = _speciesList.Select(s => s.DisplayName).ToArray();
                SelectedSpecies = AvailableSpecies[0];
            }

            _backgroundList = await backgroundsTask;
            if (_backgroundList.Count > 0)
            {
                AvailableBackgrounds = _backgroundList.Select(b => b.DisplayName).ToArray();
                SelectedBackground = AvailableBackgrounds[0];
            }
        }
        catch
        {
            ErrorMessage = "Could not start creation session. Please check your connection.";
        }
        finally { IsBusy = false; }
    }

    private async Task DoNextStepAsync()
    {
        if (_sessionId is null)
        {
            StepError = "No active creation session. Please cancel and try again.";
            return;
        }

        IsBusy = true;
        StepError = string.Empty;
        try
        {
            switch (CurrentStepIndex)
            {
                case 0:
                {
                    var nameFormatError = ValidateNameFormat(Name);
                    if (nameFormatError is not null)
                    { StepError = nameFormatError; return; }
                    if (!await _creationService.SetNameAsync(_sessionId.Value, Name))
                    { StepError = "Could not save character name. Please try again."; return; }
                    break;
                }
                case 1:
                    if (!await _creationService.SetClassAsync(_sessionId.Value, SelectedClass))
                    { StepError = "Could not save class selection. Please try again."; return; }
                    break;
                case 2:
                {
                    var slug = _speciesList.FirstOrDefault(s => s.DisplayName == SelectedSpecies)?.Slug ?? SelectedSpecies;
                    if (!await _creationService.SetSpeciesAsync(_sessionId.Value, slug))
                    { StepError = "Could not save species selection. Please try again."; return; }
                    break;
                }
                case 3:
                {
                    var id = _backgroundList.FirstOrDefault(b => b.DisplayName == SelectedBackground)?.Slug ?? SelectedBackground;
                    if (!await _creationService.SetBackgroundAsync(_sessionId.Value, id))
                    { StepError = "Could not save background selection. Please try again."; return; }
                    break;
                }
                case 4:
                {
                    var allocations = new Dictionary<string, int>
                    {
                        ["Strength"] = Strength, ["Dexterity"] = Dexterity, ["Constitution"] = Constitution,
                        ["Intelligence"] = Intelligence, ["Wisdom"] = Wisdom, ["Charisma"] = Charisma
                    };
                    if (!await _creationService.SetAttributesAsync(_sessionId.Value, allocations))
                    { StepError = "Could not save attribute choices. Please try again."; return; }
                    break;
                }
                case 5:
                    if (!string.IsNullOrEmpty(SelectedArmorType) || !string.IsNullOrEmpty(SelectedWeaponType) || IncludeShield)
                    {
                        var armorSlug  = string.IsNullOrEmpty(SelectedArmorType)  ? null : SelectedArmorType.ToLowerInvariant().Replace(' ', '-');
                        var weaponSlug = string.IsNullOrEmpty(SelectedWeaponType) ? null : SelectedWeaponType.ToLowerInvariant().Replace(' ', '-');
                        if (!await _creationService.SetEquipmentPreferencesAsync(
                                _sessionId.Value, new SetCreationEquipmentPreferencesRequest(armorSlug, weaponSlug, IncludeShield)))
                        { StepError = "Could not save equipment preferences. Please try again."; return; }
                    }
                    break;
                case 6:
                {
                    var difficultyMode = IsHardcoreCreate ? "hardcore" : "normal";
                    var (character, error) = await _creationService.FinalizeAsync(
                        _sessionId.Value, new FinalizeCreationSessionRequest(null, difficultyMode));
                    if (character is not null)
                    {
                        _sessionId = null;
                        _navigation.NavigateTo<CharacterSelectViewModel>();
                    }
                    else
                    {
                        StepError = error?.Message ?? "Failed to create character.";
                    }
                    return;
                }
            }
            CurrentStepIndex++;
            await RefreshPreviewAsync();
        }
        finally { IsBusy = false; }
    }

    private async Task RefreshPreviewAsync()
    {
        if (_sessionId is null || CurrentStepIndex < 2) return;
        CharacterPreview = await _creationService.GetPreviewAsync(_sessionId.Value);
    }

    private async Task EagerSetSpeciesAsync(string displayName)
    {
        if (_sessionId is null) return;
        var slug = _speciesList.FirstOrDefault(s => s.DisplayName == displayName)?.Slug ?? displayName;
        await _creationService.SetSpeciesAsync(_sessionId.Value, slug);
        await RefreshPreviewAsync();
    }

    private async Task EagerSetBackgroundAsync(string displayName)
    {
        if (_sessionId is null) return;
        var id = _backgroundList.FirstOrDefault(b => b.DisplayName == displayName)?.Slug ?? displayName;
        await _creationService.SetBackgroundAsync(_sessionId.Value, id);
        await RefreshPreviewAsync();
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

    private async Task<string> ComputeNameErrorAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        var formatError = ValidateNameFormat(name);
        if (formatError is not null) return formatError;
        var (available, error) = await _creationService.CheckNameAvailabilityAsync(name.Trim());
        return available ? string.Empty : (error ?? "That name is not available.");
    }

    private async Task LoadSelectedClassIconAsync(string className)
    {
        var path = ClassAssets.GetPath(className);
        if (path is null) { SelectedClassIcon = null; return; }
        var bytes = await _assetStore!.LoadImageAsync(path);
        SelectedClassIcon = bytes is null ? null : new Bitmap(new MemoryStream(bytes));
    }
}
