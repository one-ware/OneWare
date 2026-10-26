using Microsoft.Extensions.Logging;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.Settings.ViewModels;
using OneWare.SoftwareProjectSystem.Models;
using OneWare.SoftwareProjectSystem.Services;
using OneWare.SoftwareProjectSystem.Templates;

namespace OneWare.SoftwareProjectSystem.ViewModels;

public class SoftwareProjectCreatorViewModel : FlexibleWindowViewModelBase
{
    private const string AutoKit = "Auto (first host compiler)";

    private readonly CheckBoxSetting _createNewFolderSetting;
    private readonly FolderPathSetting _folderPathSetting;
    private readonly ComboBoxSetting _kitSetting;
    private readonly ILogger _logger;
    private readonly SoftwareProjectFactory _factory;
    private readonly TextBoxSetting _nameSetting;
    private readonly IMainDockService _mainDockService;
    private readonly ComboBoxSetting _templateSetting;
    private readonly ToolchainKitService _kitService;

    public SoftwareProjectCreatorViewModel(IPaths paths, IMainDockService mainDockService, ILogger logger,
        SoftwareProjectFactory factory, ToolchainKitService kitService)
    {
        Paths = paths;
        _mainDockService = mainDockService;
        _logger = logger;
        _factory = factory;
        _kitService = kitService;

        _nameSetting = new TextBoxSetting("Name", "", "Enter name...")
        {
            HoverDescription = "Set the name for the project"
        };

        _templateSetting = new ComboBoxSetting("Template", SoftwareProjectTemplates.All[0].Name,
            new[] { SoftwareProjectTemplates.Empty }.Concat(SoftwareProjectTemplates.All.Select(x => x.Name))
                .ToArray<object>())
        {
            HoverDescription = "Set the template used for this project"
        };

        _folderPathSetting = new FolderPathSetting("Location", paths.ProjectsDirectory, "Enter path...",
            paths.ProjectsDirectory, Directory.Exists)
        {
            HoverDescription = "Set the location where the new project is created"
        };

        _createNewFolderSetting = new CheckBoxSetting("Create new Folder", true)
        {
            HoverDescription = "Set if a new folder should be created in the selected location"
        };

        kitService.Refresh();
        _kitSetting = new ComboBoxSetting("Toolchain", AutoKit,
            new[] { AutoKit }.Concat(kitService.Kits.Select(x => x.Id)).ToArray<object>())
        {
            HoverDescription = "Compiler used to build the project (can be changed later)"
        };

        SettingsCollection.SettingModels.Add(_nameSetting);
        SettingsCollection.SettingModels.Add(_templateSetting);
        SettingsCollection.SettingModels.Add(_folderPathSetting);
        SettingsCollection.SettingModels.Add(_createNewFolderSetting);
        SettingsCollection.SettingModels.Add(_kitSetting);
    }

    public IPaths Paths { get; }

    public SettingsCollectionViewModel SettingsCollection { get; } = new("Project Properties")
    {
        ShowTitle = false
    };

    public async Task SaveAsync(FlexibleWindow window)
    {
        try
        {
            var result = await _factory.CreateAsync((string)_nameSetting.Value, (string)_folderPathSetting.Value,
                (bool)_createNewFolderSetting.Value,
                SoftwareProjectFactory.ResolveTemplate(_templateSetting.Value.ToString()),
                _kitService.GetKit(_kitSetting.Value.ToString()));

            Close(window);

            if (result.MainSourceFile != null) await _mainDockService.OpenFileAsync(result.MainSourceFile);
        }
        catch (SoftwareProjectCreationException e)
        {
            _logger.Error(e.Message, null, true, true, window.Host);
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }
    }
}
