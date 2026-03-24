using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;

namespace RealmForge.ViewModels;

/// <summary>Identifies which junction table the editor manages.</summary>
public enum JunctionEditorType
{
    AbilityPool,        // Species / ActorInstance — ability slug only
    ArchetypePool,      // ActorArchetype — ability slug + use-chance
    ClassUnlock,        // ActorClass — ability slug + level required + rank
    LootEntry,          // LootTable — item domain + slug + weight + quantity range + guaranteed flag
    RecipeIngredient,   // Recipe — item domain + slug + quantity + optional flag
}

/// <summary>
/// A single editable row in a junction sub-editor.
/// Which columns are visible is determined by the owning <see cref="JunctionEditorType"/>
/// passed at construction time — all <c>ShowXxx</c> properties are computed once and stable.
/// </summary>
public class JunctionRowViewModel : ReactiveObject
{
    private readonly JunctionEditorType _type;

    private string  _abilitySlug   = "";
    private string  _itemDomain    = "";
    private string  _itemSlug      = "";
    private int     _dropWeight    = 50;
    private int     _quantityMin   = 1;
    private int     _quantityMax   = 1;
    private bool    _isGuaranteed;
    private int     _quantity      = 1;
    private bool    _isOptional;
    private decimal _useChance     = 1.0m;
    private int     _levelRequired = 1;
    private int     _rank          = 1;

    public event EventHandler? RowChanged;

    public JunctionRowViewModel(JunctionEditorType type)
    {
        _type         = type;
        RemoveCommand = ReactiveCommand.Create(() => { });
    }

    // Column visibility
    public bool ShowAbilitySlug   => _type is JunctionEditorType.AbilityPool
                                          or JunctionEditorType.ArchetypePool
                                          or JunctionEditorType.ClassUnlock;

    public bool ShowItemReference => _type is JunctionEditorType.LootEntry
                                          or JunctionEditorType.RecipeIngredient;

    public bool ShowUseChance     => _type == JunctionEditorType.ArchetypePool;
    public bool ShowLevelRequired => _type == JunctionEditorType.ClassUnlock;
    public bool ShowRank          => _type == JunctionEditorType.ClassUnlock;
    public bool ShowDropWeight    => _type == JunctionEditorType.LootEntry;
    public bool ShowQuantityRange => _type == JunctionEditorType.LootEntry;
    public bool ShowIsGuaranteed  => _type == JunctionEditorType.LootEntry;
    public bool ShowQuantity      => _type == JunctionEditorType.RecipeIngredient;
    public bool ShowIsOptional    => _type == JunctionEditorType.RecipeIngredient;

    // Data fields
    public string  AbilitySlug   { get => _abilitySlug;   set { this.RaiseAndSetIfChanged(ref _abilitySlug,   value); Fire(); } }
    public string  ItemDomain    { get => _itemDomain;    set { this.RaiseAndSetIfChanged(ref _itemDomain,    value); Fire(); } }
    public string  ItemSlug      { get => _itemSlug;      set { this.RaiseAndSetIfChanged(ref _itemSlug,      value); Fire(); } }
    public int     DropWeight    { get => _dropWeight;    set { this.RaiseAndSetIfChanged(ref _dropWeight,    value); Fire(); } }
    public int     QuantityMin   { get => _quantityMin;   set { this.RaiseAndSetIfChanged(ref _quantityMin,   value); Fire(); } }
    public int     QuantityMax   { get => _quantityMax;   set { this.RaiseAndSetIfChanged(ref _quantityMax,   value); Fire(); } }
    public bool    IsGuaranteed  { get => _isGuaranteed;  set { this.RaiseAndSetIfChanged(ref _isGuaranteed,  value); Fire(); } }
    public int     Quantity      { get => _quantity;      set { this.RaiseAndSetIfChanged(ref _quantity,      value); Fire(); } }
    public bool    IsOptional    { get => _isOptional;    set { this.RaiseAndSetIfChanged(ref _isOptional,    value); Fire(); } }
    public decimal UseChance     { get => _useChance;     set { this.RaiseAndSetIfChanged(ref _useChance,     value); Fire(); } }
    public int     LevelRequired { get => _levelRequired; set { this.RaiseAndSetIfChanged(ref _levelRequired, value); Fire(); } }
    public int     Rank          { get => _rank;          set { this.RaiseAndSetIfChanged(ref _rank,          value); Fire(); } }

    public ReactiveCommand<Unit, Unit> RemoveCommand { get; }

    private void Fire() => RowChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// Manages an inline editable list of <see cref="JunctionRowViewModel"/> rows for one
/// junction relationship type. Provides add / remove operations and fires
/// <see cref="DataChanged"/> whenever the collection or any row's data changes.
/// </summary>
public class JunctionEditorViewModel : ReactiveObject
{
    public JunctionEditorType EditorType { get; }
    public string             Title      { get; }
    public Guid               OwnerId    { get; }

    public ObservableCollection<JunctionRowViewModel> Rows { get; } = [];

    public ReactiveCommand<Unit, Unit> AddRowCommand { get; }

    public event EventHandler? DataChanged;

    // Column header visibility (mirrors row ShowXxx)
    public bool ShowAbilitySlug   => EditorType is JunctionEditorType.AbilityPool
                                               or JunctionEditorType.ArchetypePool
                                               or JunctionEditorType.ClassUnlock;

    public bool ShowItemReference => EditorType is JunctionEditorType.LootEntry
                                               or JunctionEditorType.RecipeIngredient;

    public bool ShowUseChance     => EditorType == JunctionEditorType.ArchetypePool;
    public bool ShowLevelRequired => EditorType == JunctionEditorType.ClassUnlock;
    public bool ShowRank          => EditorType == JunctionEditorType.ClassUnlock;
    public bool ShowDropWeight    => EditorType == JunctionEditorType.LootEntry;
    public bool ShowQuantityRange => EditorType == JunctionEditorType.LootEntry;
    public bool ShowIsGuaranteed  => EditorType == JunctionEditorType.LootEntry;
    public bool ShowQuantity      => EditorType == JunctionEditorType.RecipeIngredient;
    public bool ShowIsOptional    => EditorType == JunctionEditorType.RecipeIngredient;

    public JunctionEditorViewModel(JunctionEditorType type, string title, Guid ownerId)
    {
        EditorType    = type;
        Title         = title;
        OwnerId       = ownerId;
        AddRowCommand = ReactiveCommand.Create(AddRow);
    }

    /// <summary>Replaces current rows with the provided set (called after loading from DB).</summary>
    public void LoadRows(IEnumerable<JunctionRowViewModel> rows)
    {
        Rows.Clear();
        foreach (var row in rows)
            WireAndAdd(row);
    }

    private void AddRow()
    {
        WireAndAdd(new JunctionRowViewModel(EditorType));
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    private void WireAndAdd(JunctionRowViewModel row)
    {
        row.RemoveCommand.Subscribe(_ =>
        {
            Rows.Remove(row);
            DataChanged?.Invoke(this, EventArgs.Empty);
        });
        row.RowChanged += (_, _) => DataChanged?.Invoke(this, EventArgs.Empty);
        Rows.Add(row);
    }
}
