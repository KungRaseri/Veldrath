using Avalonia.Controls;
using Avalonia.Controls.Templates;
using RealmUnbound.Client.ViewModels;
using System;

namespace RealmUnbound.Client;

public class ViewLocator : IDataTemplate
{
    public Control? Build(object? data)
    {
        if (data is null) return null;

        var viewModelName = data.GetType().FullName!;
        var viewName = viewModelName
            .Replace(".ViewModels.", ".Views.")
            .Replace("ViewModel", "View");

        var type = Type.GetType(viewName);

        if (type is not null)
            return (Control)Activator.CreateInstance(type)!;

        return new TextBlock { Text = $"View not found: {viewName}" };
    }

    public bool Match(object? data) => data is ViewModelBase;
}
