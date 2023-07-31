using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using OneWare.Settings;
using OneWare.Settings.ViewModels;
using OneWare.Settings.ViewModels.SettingTypes;
using OneWare.Shared;
using OneWare.Shared.Services;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public class UniversalFpgaProjectCreatorViewModel : ObservableObject
{
    public IPaths Paths { get; }
    
    private readonly TextBoxSetting _nameSetting;
    private readonly ComboBoxSetting _templateSetting;
    private readonly FolderPathSetting _folderPathSetting;
    private readonly TitledSetting _createNewFolderSetting;

    public SettingsCollectionViewModel SettingsCollection { get; } = new("Project Properties")
    {
        ShowTitle = false
    };

    public UniversalFpgaProjectCreatorViewModel(IPaths paths)
    {
        Paths = paths;

        _nameSetting = new TextBoxSetting("Name", "Set the name for the project", "", "Enter name..."); 
        
        _templateSetting = new ComboBoxSetting("Template", "Set the template used for this project", "Empty",
            new[] { "Empty" });
        
        _folderPathSetting = new FolderPathSetting("Location", "Set the location where the new project is created",
            paths.ProjectsDirectory, "Enter path...", paths.ProjectsDirectory, Directory.Exists);

        _createNewFolderSetting = new TitledSetting("Create new Folder",
            "Set if a new folder should be created in the selected location", true);
        
        SettingsCollection.SettingModels.Add(new TextBoxSettingViewModel(_nameSetting));
        SettingsCollection.SettingModels.Add(new ComboBoxSettingViewModel(_templateSetting));
        SettingsCollection.SettingModels.Add(new PathSettingViewModel(_folderPathSetting));
        SettingsCollection.SettingModels.Add(new CheckBoxSettingViewModel(_createNewFolderSetting));
    }

    public void Save(FlexibleWindow window)
    {
        var name = (string)_nameSetting.Value;
        var template = (string)_templateSetting.Value;
        var folder = (string)_folderPathSetting.Value;
        var createNewFolder = (bool)_createNewFolderSetting.Value;
        
        window?.Close();
    }
}