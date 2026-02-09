using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Xaml.Interactions.Custom;
using Avalonia.Xaml.Interactivity;
using OneWare.Essentials.Models;

namespace OneWare.ProjectExplorer.Behaviors;

public class ProjectExplorerEntryModificationBehavior : AttachedToVisualTreeBehavior<Control>
{
    protected override IDisposable OnAttachedToVisualTreeOverride()
    {
        var compositeDisposable = new CompositeDisposable();

        if (DataContext is IProjectEntry { Root: IProjectRootWithFile root } entry)
        {
            Observable.FromEventPattern(root, nameof(IProjectRootWithFile.ProjectPropertyChanged)).Subscribe(x =>
            {
                root.InvalidateModifications(entry);
            }).DisposeWith(compositeDisposable);
                
            root.InvalidateModifications(entry);
        }
        
        return compositeDisposable;
    }
}