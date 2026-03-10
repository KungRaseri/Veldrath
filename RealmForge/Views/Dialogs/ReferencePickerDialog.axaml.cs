using Avalonia.Controls;
using Avalonia.Interactivity;
using RealmForge.ViewModels;

namespace RealmForge.Views.Dialogs;

public partial class ReferencePickerDialog : Window
{
    public ReferencePickerDialog()
    {
        InitializeComponent();
    }

    private void Insert_Click(object? sender, RoutedEventArgs e)
    {
        var vm = DataContext as ReferencePickerViewModel;
        Close(vm?.SelectedReference?.ReferenceString);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);
}
