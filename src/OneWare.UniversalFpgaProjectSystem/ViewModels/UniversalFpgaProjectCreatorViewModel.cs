using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Settings;
using OneWare.Settings.ViewModels;
using OneWare.Settings.ViewModels.SettingTypes;
using OneWare.Shared;
using OneWare.Shared.Services;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public class UniversalFpgaProjectCreatorViewModel : ObservableObject
{
    public IPaths Paths { get; }
    private readonly FolderPathSetting _folderPathSetting;
    private readonly TitledSetting _createNewFolderSetting;

    public SettingsCollectionViewModel SettingsCollection { get; } = new("Settings");

    public UniversalFpgaProjectCreatorViewModel(IPaths paths)
    {
        Paths = paths;
        
        _folderPathSetting = new FolderPathSetting("Project Folder", "Set the folder where the new project is created",
            paths.ProjectsDirectory, paths.ProjectsDirectory);

        _createNewFolderSetting = new TitledSetting("Create new Folder",
            "Set if a new folder should be created for the new project", true);
        
        SettingsCollection.SettingModels.Add(new PathSettingViewModel(_folderPathSetting));
        SettingsCollection.SettingModels.Add(new CheckBoxSettingViewModel(_createNewFolderSetting));
    }

    public void Save(FlexibleWindow window)
    {
        var folder = (string)_folderPathSetting.Value;
        var createNewFolder = (bool)_createNewFolderSetting.Value;
        window?.Close();
    }
}