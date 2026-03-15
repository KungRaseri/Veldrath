using ReactiveUI;

namespace RealmForge.ViewModels;

public class ActivitySectionViewModel(string key, string label, string iconPath) : ReactiveObject
{
    private bool _isActive;

    public string Key      { get; } = key;
    public string Label    { get; } = label;
    public string IconPath { get; } = iconPath;

    public bool IsActive
    {
        get => _isActive;
        set => this.RaiseAndSetIfChanged(ref _isActive, value);
    }
}
