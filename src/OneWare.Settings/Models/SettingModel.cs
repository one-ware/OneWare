using Avalonia.Media;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData.Binding;

namespace OneWare.Settings.Models
{
    public abstract class SettingModel : ObservableObject
    {
        private bool _isEnabled = true;

        public TitledSetting Setting { get; }

        protected SettingModel(TitledSetting setting)
        {
            Setting = setting;
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }
    }

    public class SettingModelCheckBox : SettingModel
    {
        public SettingModelCheckBox(TitledSetting setting, IObservable<bool>? needEnabled = null) :
            base(setting) 
        {
            needEnabled?.Subscribe(x =>
            {
                IsEnabled = x;
                if (!x) setting.Value = false;
            });
        }
    }

    public class SettingModelColorPicker : SettingModel
    {
        public SettingModelColorPicker(TitledSetting setting) : base(setting)
        {
        }
    }
    
    public class SettingModelTextBox : SettingModel
    {
        public SettingModelTextBox(TitledSetting setting) : base(setting)
        {
        }
    }

    public abstract class SettingModelPath : SettingModel
    {
        private IBrush _borderColor = Brushes.Gray;

        protected SettingModelPath(TitledSetting setting) : base(setting)
        {
        }

        public IBrush BorderColor
        {
            get => _borderColor;
            set => SetProperty(ref _borderColor, value);
        }

        public abstract Task SelectPathAsync();
    }

    public class SettingModelFilePathValid : SettingModelPath
    {
        private readonly List<FilePickerFileType>? _filters;

        public SettingModelFilePathValid(TitledSetting setting,
            Func<string?, bool> isValid,
            List<FilePickerFileType>? filter = null) : base(setting)
        {
            setting.WhenValueChanged(x => x.Value).Subscribe(x =>
            {
                if (isValid.Invoke(x as string)) BorderColor = Brushes.Green;
                else BorderColor = Brushes.Red;
            });

            _filters = filter;
        }

        public override async Task SelectPathAsync()
        {
            //var path = await Tools.SelectFileAsync(SettingsView.LastInstance, $"Select {Setting.Title}",
            //    ContainerLocator.Container.Resolve<IPaths>().DocumentsDirectory, _filters?.ToArray());

            //if (path == null) return;
            //Setting.Value = path;
        }
    }

    public class SettingModelFolderPathValid : SettingModelPath
    {
        public SettingModelFolderPathValid(TitledSetting setting,
            Func<string?, bool> isValid) :
            base(setting)
        {
            setting.WhenValueChanged(x => x.Value).Subscribe(x =>
            {
                if (isValid.Invoke(x as string)) BorderColor = Brushes.Green;
                else BorderColor = Brushes.Red;
            });
        }

        public override async Task SelectPathAsync()
        {
            // var folder = await Tools.SelectFolderAsync(SettingsWindow.LastInstance, ToolTip,
            //     App.Paths.DocumentsDirectory);
            //
            // if (folder == null) return;
            // OptionValue = folder;
        }
    }


    public class SettingModelComboBox : SettingModel
    {
        public new ComboBoxSetting Setting { get; }
        
        public SettingModelComboBox(ComboBoxSetting setting) : base(setting)
        {
            this.Setting = setting;
        }
    }
}