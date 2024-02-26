using System.Text.Json.Nodes;
using OneWare.Settings;
using OneWare.Settings.ViewModels;
using OneWare.Settings.ViewModels.SettingTypes;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public class UniversalFpgaProjectCreatorViewModel : FlexibleWindowViewModelBase
{
    public IPaths Paths { get; }
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly FpgaService _fpgaService;
    private readonly ILogger _logger;
    private readonly UniversalFpgaProjectManager _manager;
    
    private readonly TextBoxSetting _nameSetting;
    private readonly ComboBoxSetting _templateSetting;
    private readonly FolderPathSetting _folderPathSetting;
    private readonly TitledSetting _createNewFolderSetting;
    private readonly ComboBoxSetting _toolchainSetting;
    private readonly ComboBoxSetting _loaderSetting;

    public SettingsCollectionViewModel SettingsCollection { get; } = new("Project Properties")
    {
        ShowTitle = false
    };

    public UniversalFpgaProjectCreatorViewModel(IPaths paths, IProjectExplorerService projectExplorerService, ILogger logger, FpgaService fpgaService, UniversalFpgaProjectManager manager)
    {
        _projectExplorerService = projectExplorerService;
        _fpgaService = fpgaService;
        _logger = logger;
        _manager = manager;
        Paths = paths;
        
        _nameSetting = new TextBoxSetting("Name", "Set the name for the project", "", "Enter name..."); 
        
        _templateSetting = new ComboBoxSetting("Template", "Set the template used for this project", "Empty",
            new[] { "Empty" });
        
        _folderPathSetting = new FolderPathSetting("Location", "Set the location where the new project is created",
            paths.ProjectsDirectory, "Enter path...", paths.ProjectsDirectory, Directory.Exists);

        _createNewFolderSetting = new TitledSetting("Create new Folder",
            "Set if a new folder should be created in the selected location", true);

        _toolchainSetting = new ComboBoxSetting("Toolchain",
            "Set the toolchain to use for the project (can be changed later)", fpgaService.Toolchains.FirstOrDefault()?.Name ?? "Unset",
            new[] { "Unset" }.Concat(fpgaService.Toolchains
                .Select(x => x.Name)));
        
        _loaderSetting = new ComboBoxSetting("Loader",
            "Set the loader to use for the project (can be changed later)", fpgaService.Loaders.FirstOrDefault()?.Name ?? "Unset",
            new[] { "Unset" }.Concat(fpgaService.Loaders
                .Select(x => x.Name)));
        
        SettingsCollection.SettingModels.Add(new TextBoxSettingViewModel(_nameSetting));
        SettingsCollection.SettingModels.Add(new ComboBoxSettingViewModel(_templateSetting));
        SettingsCollection.SettingModels.Add(new PathSettingViewModel(_folderPathSetting));
        SettingsCollection.SettingModels.Add(new CheckBoxSettingViewModel(_createNewFolderSetting));
        SettingsCollection.SettingModels.Add(new ComboBoxSettingViewModel(_toolchainSetting));
        SettingsCollection.SettingModels.Add(new ComboBoxSettingViewModel(_loaderSetting));
    }

    public async Task SaveAsync(FlexibleWindow window)
    {
        var name = (string)_nameSetting.Value;
        var template = (string)_templateSetting.Value;
        var folder = (string)_folderPathSetting.Value;
        var createNewFolder = (bool)_createNewFolderSetting.Value;

        if (string.IsNullOrWhiteSpace(name))
        {
            _logger.Error("Invalid project name!", null, true, true, window.Host);
            return;
        }
        
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            _logger.Error("Invalid project folder!", null, true, true, window.Host);
            return;
        }

        try
        {
            if (createNewFolder)
            {
                folder = Path.Combine(folder, name);
                Directory.CreateDirectory(folder);
            }
        
            var projectFile = Path.Combine(folder, name + UniversalFpgaProjectRoot.ProjectFileExtension);

            var defaultProperties = new JsonObject()
            {
                ["Include"] = new JsonArray("*.vhd", "*.vhdl", "*.v"),
                ["Exclude"] = new JsonArray("build"),
            };
            var root = new UniversalFpgaProjectRoot(projectFile, defaultProperties);

            if (_fpgaService.Toolchains.FirstOrDefault(x => x.Name == _toolchainSetting.Value.ToString()) is {} tc) 
                root.Toolchain = tc;

            if (_fpgaService.Loaders.FirstOrDefault(x => x.Name == _loaderSetting.Value.ToString()) is {} loader) 
                root.Loader = loader;

            await _manager.SaveProjectAsync(root);
            
            _projectExplorerService.Insert(root);
            
            _projectExplorerService.ActiveProject = root;
            
            Close(window);
            
            _ = _projectExplorerService.SaveLastProjectsFileAsync();
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }
    }
}