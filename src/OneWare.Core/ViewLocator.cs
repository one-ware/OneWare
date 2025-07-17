using Avalonia.Controls;
using Avalonia.Controls.Templates;
using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Core;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Services;
using Prism.Ioc;

namespace OneWare.Core;

public class ViewLocator : IDataTemplate
{
    public bool SupportsRecycling => false;

    public Control Build(object? data)
    {
        var name = data?.GetType()?.AssemblyQualifiedName?.Replace("ViewModel", "View");
        if (name == null) return new TextBlock { Text = "Invalid Data Type" };
        var type = Type.GetType(name);
        if (type != null && name.Split(",")[0].EndsWith("view", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var instance = ContainerLocator.Current.Resolve(type);
                if (instance != null)
                    return (Control)instance;
            }
            catch (Exception e)
            {
                ContainerLocator.Current.Resolve<ILogger>().LogError(e, e.Message);
            }

            return new TextBlock { Text = "Create Instance Failed: " + type.FullName };
        }

        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object? data)
    {
        return data is ObservableObject or IDockable;
    }
}