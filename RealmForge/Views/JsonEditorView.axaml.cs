using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using AvaloniaEdit.Highlighting;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using RealmForge.ViewModels;
using RealmForge.Views.Dialogs;

namespace RealmForge.Views;

public partial class JsonEditorView : UserControl
{
    private JsonEditorViewModel? _vm;
    private IDisposable? _contentSubscription;

    public JsonEditorView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _contentSubscription?.Dispose();

        _vm = DataContext as JsonEditorViewModel;
        if (_vm == null) return;

        // Set up JSON syntax highlighting
        var jsonHighlighting = HighlightingManager.Instance.GetDefinition("JavaScript")
                              ?? HighlightingManager.Instance.GetDefinitionByExtension(".js");
        if (jsonHighlighting != null)
            JsonEditor.SyntaxHighlighting = jsonHighlighting;

        // Bind the TextDocument so ViewModel can set text
        JsonEditor.Document = _vm.TextDocument;

        // Register the reference picker interaction handler
        _contentSubscription = _vm.ShowReferencePickerInteraction
            .RegisterHandler(async interaction =>
            {
                var pickerVm = new ReferencePickerViewModel(
                    App.Services.GetRequiredService<Services.ReferenceResolverService>(),
                    interaction.Input);

                var dialog = new ReferencePickerDialog { DataContext = pickerVm };
                await pickerVm.InitializeAsync(_vm.DataFolderPath);

                var result = await dialog.ShowDialog<string?>(
                    this.FindAncestorOfType<Window>()!);
                interaction.SetOutput(result);
            });
    }

    private void FileTreeView_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_vm == null) return;
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is FileTreeNodeViewModel node && !node.IsDirectory)
            _vm.LoadFileCommand.Execute(node.FullPath).Subscribe();
    }

    private void JsonModeBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm?.IsFormMode == true)
            _vm.ToggleModeCommand.Execute().Subscribe();
    }

    private void FormModeBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm?.IsJsonMode == true)
            _vm.ToggleModeCommand.Execute().Subscribe();
    }
}
