using System.Collections.ObjectModel;
using ReactiveUI;

namespace RealmUnbound.Client.ViewModels;

/// <summary>View model for a single attribute draft row in the allocation overlay.</summary>
public sealed class AttributeDraftViewModel : ViewModelBase
{
    private int _draft;

    /// <summary>Initializes a new instance of <see cref="AttributeDraftViewModel"/>.</summary>
    public AttributeDraftViewModel(string name, int baseValue, AttributeAllocationViewModel owner)
    {
        Name = name;
        BaseValue = baseValue;
        _owner = owner;
        IncrementCommand = ReactiveCommand.Create(
            () => { Draft++; },
            owner.WhenAnyValue(o => o.PointsToAllocate, p => p > 0));
        DecrementCommand = ReactiveCommand.Create(
            () => { Draft--; },
            this.WhenAnyValue(x => x.Draft, d => d > 0));
    }

    private readonly AttributeAllocationViewModel _owner;

    /// <summary>Attribute name (e.g. "Strength").</summary>
    public string Name { get; }

    /// <summary>Current server-confirmed value of the attribute.</summary>
    public int BaseValue { get; }

    /// <summary>Draft delta — points allocated to this attribute in the current overlay session.</summary>
    public int Draft
    {
        get => _draft;
        set
        {
            var delta = value - _draft;
            this.RaiseAndSetIfChanged(ref _draft, value);
            this.RaisePropertyChanged(nameof(DisplayValue));
            _owner.PointsToAllocate -= delta;
        }
    }

    /// <summary>Combined base + draft value shown in the overlay.</summary>
    public int DisplayValue => BaseValue + Draft;

    /// <summary>Increment the draft value by 1 (disabled when budget is exhausted).</summary>
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> IncrementCommand { get; }

    /// <summary>Decrement the draft value by 1 (disabled when draft is 0).</summary>
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> DecrementCommand { get; }
}

/// <summary>View model for the attribute allocation overlay panel.</summary>
public sealed class AttributeAllocationViewModel : ViewModelBase
{
    private int _pointsToAllocate;
    private readonly GameViewModel _gameVm;

    /// <summary>Initializes a new instance of <see cref="AttributeAllocationViewModel"/>.</summary>
    public AttributeAllocationViewModel(GameViewModel gameVm)
    {
        _gameVm = gameVm;
        _pointsToAllocate = gameVm.UnspentAttributePoints;

        Attributes =
        [
            new AttributeDraftViewModel("Strength",     gameVm.Strength,     this),
            new AttributeDraftViewModel("Dexterity",    gameVm.Dexterity,    this),
            new AttributeDraftViewModel("Constitution", gameVm.Constitution, this),
            new AttributeDraftViewModel("Intelligence", gameVm.Intelligence, this),
            new AttributeDraftViewModel("Wisdom",       gameVm.Wisdom,       this),
            new AttributeDraftViewModel("Charisma",     gameVm.Charisma,     this),
        ];

        var canConfirm = this.WhenAnyValue(x => x.PointsToAllocate, p => p < gameVm.UnspentAttributePoints);
        ConfirmCommand = ReactiveCommand.CreateFromTask(DoConfirmAsync, canConfirm);
        CancelCommand  = ReactiveCommand.Create(DoCancel);
    }

    /// <summary>Draft rows for the six core attributes.</summary>
    public IReadOnlyList<AttributeDraftViewModel> Attributes { get; }

    /// <summary>Remaining points available to allocate during this overlay session.</summary>
    public int PointsToAllocate
    {
        get => _pointsToAllocate;
        set => this.RaiseAndSetIfChanged(ref _pointsToAllocate, value);
    }

    /// <summary>Confirm the current draft and send the allocation delta to the server.</summary>
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> ConfirmCommand { get; }

    /// <summary>Close the overlay without applying changes.</summary>
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> CancelCommand { get; }

    private System.Threading.Tasks.Task DoConfirmAsync()
    {
        var allocations = Attributes
            .Where(a => a.Draft > 0)
            .ToDictionary(a => a.Name, a => a.Draft);

        if (allocations.Count == 0)
            return System.Threading.Tasks.Task.CompletedTask;

        _gameVm.AllocateAttributePointsCommand.Execute(allocations).Subscribe();
        return System.Threading.Tasks.Task.CompletedTask;
    }

    private void DoCancel()
    {
        _gameVm.CloseAttributeAllocationCommand.Execute(default).Subscribe();
    }
}
