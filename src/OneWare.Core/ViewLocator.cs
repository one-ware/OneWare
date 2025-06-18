using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Core;
using OneWare.Essentials.Services; // Ensure ILogger is in this namespace
using Autofac; // Add this for ILifetimeScope

namespace OneWare.Core;

public class ViewLocator : IDataTemplate
{
    public bool SupportsRecycling => false;

    private readonly ILifetimeScope _lifetimeScope; // Injected dependency
    private readonly ILogger _logger;             // Injected dependency

    /// <summary>
    /// Initializes a new instance of the <see cref="ViewLocator"/> class.
    /// </summary>
    /// <param name="lifetimeScope">The Autofac lifetime scope used to resolve view instances.</param>
    /// <param name="logger">The logger service for reporting errors.</param>
    public ViewLocator(ILifetimeScope lifetimeScope, ILogger logger)
    {
        _lifetimeScope = lifetimeScope ?? throw new ArgumentNullException(nameof(lifetimeScope));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Control Build(object? data)
    {
        if (data == null)
        {
            return new TextBlock { Text = "Invalid Data Type (null)" };
        }

        // Attempt to derive the view type name from the view model's assembly qualified name
        // Replace "ViewModel" with "View"
        var viewTypeName = data.GetType().AssemblyQualifiedName?.Replace("ViewModel", "View");

        if (string.IsNullOrWhiteSpace(viewTypeName))
        {
            return new TextBlock { Text = $"Could not determine view type for data: {data.GetType().FullName}" };
        }

        var viewType = Type.GetType(viewTypeName);

        // Basic validation for the derived type
        // Ensure it's not null and its short name ends with "View" (case-insensitive)
        if (viewType != null && viewTypeName.Split(',')[0].EndsWith("View", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                // Resolve the view instance using the injected lifetime scope
                // Autofac will handle injecting any dependencies of the view's constructor
                var instance = _lifetimeScope.Resolve(viewType);

                if (instance is Control controlInstance)
                {
                    return controlInstance;
                }
                else
                {
                    _logger.Error($"Resolved instance of type {viewType.FullName} is not an Avalonia Control.");
                    return new TextBlock { Text = $"Resolved instance is not a Control: {viewType.FullName}" };
                }
            }
            catch (Exception e)
            {
                // Use the injected logger
                _logger.Error($"Failed to create instance of view '{viewType.FullName}'. Error: {e.Message}", e);
                return new TextBlock { Text = $"Error creating View: {viewType.FullName}\n{e.Message}" };
            }
        }

        return new TextBlock { Text = $"View Not Found or Invalid: {viewTypeName}" };
    }

    public bool Match(object? data)
    {
        // This logic remains the same as it determines if the DataTemplate can handle the data type.
        return data is ObservableObject or IDockable;
    }
}