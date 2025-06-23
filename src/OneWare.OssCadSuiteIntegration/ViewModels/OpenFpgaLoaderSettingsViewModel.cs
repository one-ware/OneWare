using Microsoft.Extensions.Logging;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Models;
using OneWare.Essentials.ViewModels;
using OneWare.Settings;
using OneWare.Settings.ViewModels;
using OneWare.Settings.ViewModels.SettingTypes;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Parser;

namespace OneWare.OssCadSuiteIntegration.ViewModels
{
    public class OpenFpgaLoaderSettingsViewModel : FlexibleWindowViewModelBase
    {
        private readonly TextBoxSetting _boardSetting;
        private readonly IFpga _fpga;
        private readonly TextBoxSetting _longTermFlagsSetting;
        private readonly UniversalFpgaProjectRoot _projectRoot;
        private readonly Dictionary<string, string> _settings;
        private readonly TextBoxSetting _shortTermFlagsSetting;
        private readonly ILogger<OpenFpgaLoaderSettingsViewModel> _logger;

        public OpenFpgaLoaderSettingsViewModel(
            UniversalFpgaProjectRoot projectRoot,
            IFpga fpga,
            ILogger<OpenFpgaLoaderSettingsViewModel> logger)
        {
            _projectRoot = projectRoot ?? throw new ArgumentNullException(nameof(projectRoot));
            _fpga = fpga ?? throw new ArgumentNullException(nameof(fpga));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            Title = "OpenFPGALoader Settings";
            Id = "OpenFpgaLoaderSettings";

            var defaultProperties = fpga.Properties;
            _settings = FpgaSettingsParser.LoadSettings(projectRoot, fpga.Name, _logger);

            _boardSetting = new TextBoxSetting(
                "Board",
                defaultProperties.GetValueOrDefault("openFpgaLoaderBoard") ?? string.Empty,
                null)
            {
                HoverDescription = "OpenFPGALoader Board"
            };

            _shortTermFlagsSetting = new TextBoxSetting(
                "Short Term Arguments",
                defaultProperties.GetValueOrDefault("openFpgaLoaderShortTermFlags") ?? string.Empty,
                null)
            {
                HoverDescription = "OpenFPGALoader Flags for Short Term Programming"
            };

            _longTermFlagsSetting = new TextBoxSetting(
                "Long Term Arguments",
                defaultProperties.GetValueOrDefault("openFpgaLoaderLongTermFlags") ?? string.Empty,
                null)
            {
                HoverDescription = "OpenFPGALoader Flags for Long Term Programming"
            };

            if (_settings.TryGetValue("openFpgaLoaderBoard", out var oflBoard))
                _boardSetting.Value = oflBoard;

            if (_settings.TryGetValue("openFpgaLoaderShortTermFlags", out var oflSFlags))
                _shortTermFlagsSetting.Value = oflSFlags;

            if (_settings.TryGetValue("openFpgaLoaderLongTermFlags", out var oflLFlags))
                _longTermFlagsSetting.Value = oflLFlags;

            SettingsCollection.SettingModels.Add(_boardSetting);
            SettingsCollection.SettingModels.Add(_shortTermFlagsSetting);
            SettingsCollection.SettingModels.Add(_longTermFlagsSetting);
        }

        public SettingsCollectionViewModel SettingsCollection { get; } = new("OpenFPGALoader Settings")
        {
            ShowTitle = false
        };

        public void Save(FlexibleWindow flexibleWindow)
        {
            try
            {
                _settings["openFpgaLoaderBoard"] = _boardSetting.Value?.ToString() ?? string.Empty;
                _settings["openFpgaLoaderShortTermFlags"] = _shortTermFlagsSetting.Value?.ToString() ?? string.Empty;
                _settings["openFpgaLoaderLongTermFlags"] = _longTermFlagsSetting.Value?.ToString() ?? string.Empty;

                bool success = FpgaSettingsParser.SaveSettings(_projectRoot, _fpga.Name, _settings, _logger);
                if (success)
                {
                    Close(flexibleWindow);
                }
                else
                {
                    _logger.LogError("Failed to save settings.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while saving settings.");
            }
        }

        public void Reset()
        {
            foreach (var setting in SettingsCollection.SettingModels)
            {
                setting.Value = setting.DefaultValue;
            }
        }
    }
}
