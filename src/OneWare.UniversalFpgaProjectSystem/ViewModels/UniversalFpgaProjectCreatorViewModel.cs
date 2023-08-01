﻿using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using OneWare.Settings;
using OneWare.Settings.ViewModels;
using OneWare.Settings.ViewModels.SettingTypes;
using OneWare.Shared;
using OneWare.Shared.Services;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public class UniversalFpgaProjectCreatorViewModel : ObservableObject
{
    public IPaths Paths { get; }
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly ILogger _logger;
    private readonly UniversalFpgaProjectManager _manager;
    
    private readonly TextBoxSetting _nameSetting;
    private readonly ComboBoxSetting _templateSetting;
    private readonly FolderPathSetting _folderPathSetting;
    private readonly TitledSetting _createNewFolderSetting;

    public SettingsCollectionViewModel SettingsCollection { get; } = new("Project Properties")
    {
        ShowTitle = false
    };

    public UniversalFpgaProjectCreatorViewModel(IPaths paths, IProjectExplorerService projectExplorerService, ILogger logger, UniversalFpgaProjectManager manager)
    {
        _projectExplorerService = projectExplorerService;
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
        
        SettingsCollection.SettingModels.Add(new TextBoxSettingViewModel(_nameSetting));
        SettingsCollection.SettingModels.Add(new ComboBoxSettingViewModel(_templateSetting));
        SettingsCollection.SettingModels.Add(new PathSettingViewModel(_folderPathSetting));
        SettingsCollection.SettingModels.Add(new CheckBoxSettingViewModel(_createNewFolderSetting));
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
        
            var projectFile = Path.Combine(folder, name + ".fpgaproj");
            
            var root = new UniversalFpgaProjectRoot(projectFile);

            await _manager.SaveProjectAsync(root);
            
            _projectExplorerService.Insert(root);
            
            _projectExplorerService.ActiveProject = root;
            
            window?.Close();
            
            _ = _projectExplorerService.SaveLastProjectsFileAsync();
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }
    }
}