using System.Collections.ObjectModel;
using ReactiveUI;
using RealmForge.Models;
using RealmForge.Services;

namespace RealmForge.ViewModels;

public class ReferencePickerViewModel : ReactiveObject
{
    private readonly ReferenceResolverService _resolver;
    private string _searchText = string.Empty;
    private string? _selectedCategory;
    private ReferenceInfo? _selectedReference;
    private bool _isLoading;

    public ReferencePickerViewModel(ReferenceResolverService resolver, string? initialReference = null)
    {
        _resolver = resolver;
        AllReferences = new ObservableCollection<ReferenceInfo>();
        FilteredReferences = new ObservableCollection<ReferenceInfo>();
        Categories = new ObservableCollection<string>();

        if (initialReference != null)
            SearchText = initialReference;
    }

    public ObservableCollection<ReferenceInfo> AllReferences { get; }
    public ObservableCollection<ReferenceInfo> FilteredReferences { get; }
    public ObservableCollection<string> Categories { get; }

    public ReferenceInfo? SelectedReference
    {
        get => _selectedReference;
        set => this.RaiseAndSetIfChanged(ref _selectedReference, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            this.RaiseAndSetIfChanged(ref _searchText, value);
            ApplyFilter();
        }
    }

    public string? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedCategory, value);
            ApplyFilter();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public async Task InitializeAsync(string dataPath)
    {
        if (_resolver.IsInitialized) return;
        IsLoading = true;
        try
        {
            await _resolver.BuildReferenceCatalogAsync(dataPath);
            var cats = _resolver.GetCategories().Select(c => c.Id).ToList();
            Categories.Clear();
            Categories.Add("(All)");
            foreach (var cat in cats) Categories.Add(cat);

            ApplyFilter();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        var category = _selectedCategory == "(All)" ? null : _selectedCategory;
        var results = _resolver.SearchReferences(_searchText, category);
        FilteredReferences.Clear();
        foreach (var r in results.Take(200))
            FilteredReferences.Add(r);
    }
}
