using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Adapters;
using OneWare.Essentials.Interfaces;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.FolderProjectSystem.Models;

namespace OneWare.FolderProjectSystem.Modules
{
    public class FolderProjectSystemModule: IOneWareModule
    {
        private readonly IContainerAdapter _containerAdapter;
       
        public FolderProjectSystemModule(IContainerAdapter containerAdapter)
        {
            _containerAdapter = containerAdapter;
        }

        public void RegisterTypes()
        {

            OnExecute();
        }

        public void OnExecute()
        {
            var manager = _containerAdapter.Resolve<FolderProjectManager>();

            _containerAdapter
                .Resolve<IProjectManagerService>()
                .RegisterProjectManager(FolderProjectRoot.ProjectType, manager);

            _containerAdapter.Resolve<IWindowService>().RegisterMenuItem("MainWindow_MainMenu/File/Open",
                new MenuItemViewModel("Folder")
                {
                    Header = "Folder",
                    Command = new RelayCommand(() =>
                        _ = _containerAdapter.Resolve<IProjectExplorerService>().LoadProjectFolderDialogAsync(manager)),
                    IconObservable = Application.Current!.GetResourceObservable("VsImageLib.OpenFolder16X")
                });
        }
    }
}