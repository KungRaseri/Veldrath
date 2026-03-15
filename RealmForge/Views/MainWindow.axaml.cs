using Avalonia.Controls;

namespace RealmForge.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is RealmForge.ViewModels.MainWindowViewModel vm)
                vm.SetOwnerWindow(this);
        };
    }
}
