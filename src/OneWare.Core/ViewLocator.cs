using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Core;
using Autofac;
using OneWare.Essentials.Services;

namespace OneWare.Core
{
    public class ViewLocator : IDataTemplate
    {
        private readonly ILifetimeScope _scope;
        private readonly ILogger _logger;

        public ViewLocator(ILifetimeScope scope, ILogger logger)
        {
            _scope = scope;
            _logger = logger;
        }

        public bool SupportsRecycling => false;

        public Control Build(object? data)
        {
            var name = data?.GetType()?.AssemblyQualifiedName?.Replace("ViewModel", "View");

            if (name == null)
                return new TextBlock { Text = "Invalid Data Type" };

            var type = Type.GetType(name);

            if (type != null && name.Split(',')[0].EndsWith("view", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    if (_scope.ResolveOptional(type) is Control control)
                        return control;

                    return new TextBlock { Text = "Instance is not a Control: " + type.FullName };
                }
                catch (Exception e)
                {
                    _logger.Error(e.Message, e);
                    return new TextBlock { Text = "Exception creating: " + type.FullName };
                }
            }

            return new TextBlock { Text = "Not Found: " + name };
        }

        public bool Match(object? data)
        {
            return data is ObservableObject or IDockable;
        }
    }
}
