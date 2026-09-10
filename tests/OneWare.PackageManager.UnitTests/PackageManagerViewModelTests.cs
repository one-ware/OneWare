using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.PackageManager.Compatibility;
using OneWare.Essentials.Services;
using OneWare.PackageManager.ViewModels;
using OneWare.PackageManager.Views;
using Xunit;

namespace OneWare.PackageManager.UnitTests;

public class PackageManagerViewModelTests
{
    private readonly IPackageService _service = Substitute.For<IPackageService>();
    private readonly IWindowService _windowService = Substitute.For<IWindowService>();

    [AvaloniaFact]
    public void List_OrdersByNameThenIdRegardlessOfStatus()
    {
        var vm = CreateManager(
            CreateState("z", "Zulu", PackageStatus.UpdateAvailable),
            CreateState("b", "alpha", PackageStatus.Installed),
            CreateState("a", "Alpha", PackageStatus.Available));

        Assert.Equal(["a", "b", "z"], vm.VisiblePackages.Select(x => x.PackageState.Package.Id));
        Assert.Equal("All categories", vm.SelectedCategory?.DisplayName);
    }

    [AvaloniaTheory]
    [InlineData(PackageStatus.Available, PackageStatus.Installed, "Installed")]
    [InlineData(PackageStatus.UpdateAvailable, PackageStatus.NeedRestart, "Restart required")]
    public void StatusChange_KeepsSelectedRowAndScrollPosition(
        PackageStatus initialStatus, PackageStatus finalStatus, string statusText)
    {
        var states = Enumerable.Range(0, 30)
            .Select(i => CreateState($"p{i:D2}", $"Package {i:D2}", initialStatus)).ToArray();
        var vm = CreateManager(states);
        var view = new PackageManagerView { DataContext = vm };
        var window = new Window { Content = view, Width = 1050, Height = 600 };
        try
        {
            window.Show();
            Pump(window);
            var list = view.FindControl<ListBox>("PluginList")!;
            vm.SelectedPackage = vm.VisiblePackages[15];
            Pump(window);
            var selected = vm.SelectedPackage;
            var row = list.ContainerFromItem(selected!);
            var scroll = list.GetVisualDescendants().OfType<ScrollViewer>().First();
            var offset = scroll.Offset;
            Assert.True(offset.Y > 0);
            var collectionChanges = 0;
            ((INotifyCollectionChanged)vm.VisiblePackages).CollectionChanged += (_, _) => collectionChanges++;

            ChangeStatus(states[15], PackageStatus.Installing);
            Pump(window);
            ChangeStatus(states[15], finalStatus);
            Pump(window);

            Assert.Equal(0, collectionChanges);
            Assert.Same(selected, vm.SelectedPackage);
            Assert.Same(selected, list.SelectedItem);
            Assert.Same(row, list.ContainerFromItem(selected!));
            Assert.Equal(offset, scroll.Offset);
            Assert.Equal(statusText, selected!.StatusText);
            Assert.Equal(states.Select(x => x.Package.Id), vm.VisiblePackages.Select(x => x.PackageState.Package.Id));
            Assert.Equal(76, row!.GetVisualDescendants().OfType<Grid>().First(x => x.Height == 76).Bounds.Height);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Filters_IncludeDescendantsAndRetainOnlyMatchingSelection()
    {
        var vm = CreateManager(
            CreateState("language", "Language", category: "Plugins/Languages"),
            CreateState("tool", "Tool", category: "Plugins/Tools"),
            CreateState("board", "Board", category: "Hardware/FPGA Boards"));
        var view = new PackageManagerView { DataContext = vm };
        var window = new Window { Content = view, Width = 1050, Height = 600 };
        try
        {
            window.Show();
            Pump(window);
            var list = view.FindControl<ListBox>("PluginList")!;
            vm.SelectedPackage = vm.VisiblePackages.Single(x => x.PackageState.Package.Id == "language");
            var selected = vm.SelectedPackage;

            vm.SelectedCategory = vm.CategoryOptions.Single(x => x.DisplayName == "Plugins");
            Pump(window);
            Assert.Equal(["language", "tool"], vm.VisiblePackages.Select(x => x.PackageState.Package.Id));
            Assert.Same(selected, list.SelectedItem);

            vm.Filter = "LANG";
            Pump(window);
            Assert.Same(selected, Assert.Single(vm.VisiblePackages));
            Assert.Same(selected, list.SelectedItem);

            vm.SelectedCategory = vm.CategoryOptions.Single(x => x.DisplayName == "Hardware / FPGA Boards");
            Pump(window);
            Assert.Empty(vm.VisiblePackages);
            Assert.Null(vm.SelectedPackage);
            Assert.True(vm.HasNoResults);

            vm.IsLoading = true;
            Assert.False(vm.HasNoResults);
            vm.IsLoading = false;
            vm.Filter = "";
            Assert.Equal("board", Assert.Single(vm.VisiblePackages).PackageState.Package.Id);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void CustomCategories_UseFullPathsAndPreserveCurrentSelection()
    {
        var vm = CreateManager(CreateState("custom", "Custom", category: "Custom/Tools/Nested"));
        vm.SelectedPackage = Assert.Single(vm.VisiblePackages);
        var selected = vm.SelectedPackage;
        vm.RegisterCategory("Custom/More/Nested");
        vm.RegisterCategory("custom\\more\\nested");

        Assert.Single(vm.CategoryOptions, x => x.DisplayName == "Custom / More / Nested");
        Assert.Contains(vm.CategoryOptions, x => x.DisplayName == "Custom / Tools / Nested");
        Assert.Same(selected, vm.SelectedPackage);
        Assert.NotNull(vm.ShowExtensionManager("Custom", "Tools/Nested"));
        Assert.Equal("Custom / Tools / Nested", vm.SelectedCategory?.DisplayName);
        Assert.Same(selected, vm.SelectedPackage);
    }

    [AvaloniaFact]
    public async Task FocusPackage_RevealsHiddenPackageAndPreservesMatchingSearch()
    {
        var vm = CreateManager(
            CreateState("language", "Language", category: "Plugins/Languages"),
            CreateState("board", "Board", category: "Hardware/FPGA Boards"));
        vm.SelectedCategory = vm.CategoryOptions.Single(x => x.DisplayName == "Plugins");
        vm.Filter = "Board";

        Assert.True(await vm.ShowExtensionManagerAsync("board"));
        Assert.Equal("Board", vm.Filter);
        Assert.Equal("board", vm.SelectedPackage?.PackageState.Package.Id);
        Assert.Contains(vm.SelectedPackage!, vm.VisiblePackages);

        Assert.True(await vm.ShowExtensionManagerAsync("language"));
        Assert.Equal("", vm.Filter);
        Assert.Equal("language", vm.SelectedPackage?.PackageState.Package.Id);
        Assert.False(await vm.ShowExtensionManagerAsync("missing"));
    }

    [AvaloniaFact]
    public void CatalogRefresh_RestoresCategoryAndSelectionById()
    {
        var vm = CreateManager(CreateState("language", "Language", category: "Plugins/Languages"));
        var view = new PackageManagerView { DataContext = vm };
        var window = new Window { Content = view, Width = 1050, Height = 600 };
        try
        {
            window.Show();
            vm.SelectedCategory = vm.CategoryOptions.Single(x => x.DisplayName == "Plugins / Languages");
            vm.SelectedPackage = Assert.Single(vm.VisiblePackages);
            Pump(window);
            var category = vm.SelectedCategory;
            var replacement = CreateState("language", "Language", PackageStatus.Installed, "Plugins/Languages");
            _service.Packages.Returns(new Dictionary<string, IPackageState> { ["language"] = replacement });

            _service.PackagesUpdated += Raise.Event<EventHandler>(this, EventArgs.Empty);
            Pump(window);

            Assert.Same(category, vm.SelectedCategory);
            Assert.Same(replacement, vm.SelectedPackage?.PackageState);
            Assert.Same(vm.SelectedPackage, view.FindControl<ListBox>("PluginList")!.SelectedItem);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task UpdateAll_FocusesEachPackageWithoutReordering()
    {
        var first = CreateState("a", "Alpha", PackageStatus.UpdateAvailable, "Plugins/Languages");
        var last = CreateState("z", "Zulu", PackageStatus.UpdateAvailable, "Hardware/FPGA Boards");
        var vm = CreateManager(last, first);
        var order = vm.VisiblePackages.ToArray();
        var focusedIds = new List<string>();
        _windowService.ShowMessageBoxAsync(Arg.Any<MessageBoxRequest>(), Arg.Any<Window?>())
            .Returns(new MessageBoxResult { Button = new MessageBoxButton { Role = MessageBoxButtonRole.Yes } });
        _service.UpdateAsync(Arg.Any<string>(), Arg.Any<PackageVersion>(), false, true)
            .Returns(call =>
            {
                var id = call.ArgAt<string>(0);
                Assert.Equal(id, vm.SelectedPackage?.PackageState.Package.Id);
                focusedIds.Add(id);
                ChangeStatus(id == "a" ? first : last, PackageStatus.NeedRestart);
                return new PackageInstallResult { Status = PackageInstallResultReason.Installed };
            });

        Assert.True(vm.UpdateAllCommand.CanExecute(null));
        Assert.True(await vm.UpdateAllAsync());
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["a", "z"], focusedIds);
        Assert.Equal(order, vm.VisiblePackages);
        Assert.Equal("z", vm.SelectedPackage?.PackageState.Package.Id);
        Assert.False(vm.UpdateAllCommand.CanExecute(null));
    }

    private PackageManagerViewModel CreateManager(params IPackageState[] states)
    {
        var packages = states.ToDictionary(x => x.Package.Id!);
        _service.Packages.Returns(packages);
        return new PackageManagerViewModel(_service, Substitute.For<IHttpService>(), Substitute.For<ILogger>(),
            _windowService, Substitute.For<IApplicationStateService>());
    }

    private static IPackageState CreateState(string id, string name,
        PackageStatus status = PackageStatus.Available, string category = "Plugins/Tools")
    {
        var state = Substitute.For<IPackageState>();
        var versions = new[] { new PackageVersion { Version = "1.0.0" }, new PackageVersion { Version = "2.0.0" } };
        state.Package.Returns(new Package { Id = id, Name = name, Type = "Plugin", Category = category, Versions = versions });
        state.InstalledVersion.Returns(status == PackageStatus.Available ? null : versions[0]);
        state.Status.Returns(status);
        return state;
    }

    private static void ChangeStatus(IPackageState state, PackageStatus status)
    {
        state.Status.Returns(status);
        state.PropertyChanged += Raise.Event<PropertyChangedEventHandler>(state,
            new PropertyChangedEventArgs(nameof(IPackageState.Status)));
    }

    private static void Pump(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
