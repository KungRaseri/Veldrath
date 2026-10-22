using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using RealmForge.ViewModels;

namespace RealmForge.Views;

/// <summary>Code-behind for the map-graph editor view; owns the JSON file pickers.</summary>
public partial class MapGraphEditorView : UserControl
{
    /// <summary>Creates the view.</summary>
    public MapGraphEditorView() => InitializeComponent();

    private MapGraphEditorViewModel? ViewModel => DataContext as MapGraphEditorViewModel;

    private static FilePickerFileType JsonFileType => new("JSON")
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"],
    };

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        var viewModel = ViewModel;
        var topLevel = TopLevel.GetTopLevel(this);
        if (viewModel is null || topLevel is null)
            return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export map graphs",
            SuggestedFileName = "map-graphs.json",
            DefaultExtension = "json",
            FileTypeChoices = [JsonFileType],
        });

        if (file is null)
            return;

        await viewModel.ExportToFileAsync(file.Path.LocalPath);
    }

    private async void OnImportClick(object? sender, RoutedEventArgs e)
    {
        var viewModel = ViewModel;
        var topLevel = TopLevel.GetTopLevel(this);
        if (viewModel is null || topLevel is null)
            return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import map graphs",
            AllowMultiple = false,
            FileTypeFilter = [JsonFileType],
        });

        if (files.Count == 0)
            return;

        await viewModel.ImportFromFileAsync(files[0].Path.LocalPath);
    }
}
