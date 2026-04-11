using Veldrath.Client.ViewModels;

namespace Veldrath.Client.Services;

public interface INavigationService
{
    event Action<ViewModelBase>? CurrentPageChanged;
    void NavigateTo<TViewModel>() where TViewModel : ViewModelBase;
    void NavigateTo(ViewModelBase viewModel);
}

public class NavigationService : INavigationService
{
    private readonly IServiceProvider _services;

    public event Action<ViewModelBase>? CurrentPageChanged;

    public NavigationService(IServiceProvider services)
    {
        _services = services;
    }

    public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase
    {
        var vm = _services.GetService(typeof(TViewModel)) as ViewModelBase
            ?? throw new InvalidOperationException($"ViewModel {typeof(TViewModel).Name} is not registered.");
        NavigateTo(vm);
    }

    public void NavigateTo(ViewModelBase viewModel)
    {
        CurrentPageChanged?.Invoke(viewModel);
    }
}
