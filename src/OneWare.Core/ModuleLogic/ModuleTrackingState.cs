using Prism.Modularity;
using Prism.Mvvm;
using OneWare.Essentials.Enums;

namespace OneWare.Core.ModuleLogic;

class ModuleTrackingState : BindableBase
    {
        private string? _moduleName;
        private string _configuredDependencies = "(none)";
        private ModuleInitializationStatus _moduleInitializationStatus;
        private InitializationMode _expectedInitializationMode;
        private DiscoveryMethod _expectedDiscoveryMethod;

        public string? ModuleName
        {
            get => this._moduleName;
            set
            {
                if (this._moduleName != value)
                {
                    base.SetProperty(ref this._moduleName, value);
                }
            }
        }

        public ModuleInitializationStatus ModuleInitializationStatus
        {
            get { return this._moduleInitializationStatus; }
            set
            {
                if (this._moduleInitializationStatus != value)
                {
                    base.SetProperty(ref this._moduleInitializationStatus, value);
                }
            }
        }

        public DiscoveryMethod ExpectedDiscoveryMethod
        {
            get { return this._expectedDiscoveryMethod; }
            set
            {
                if (this._expectedDiscoveryMethod != value)
                {
                    base.SetProperty(ref this._expectedDiscoveryMethod, value);
                }
            }
        }

        public InitializationMode ExpectedInitializationMode
        {
            get { return this._expectedInitializationMode; }
            set
            {
                if (this._expectedInitializationMode != value)
                {
                    base.SetProperty(ref this._expectedInitializationMode, value);
                }
            }
        }

        public string ConfiguredDependencies
        {
            get { return this._configuredDependencies; }
            set
            {
                if (this._configuredDependencies != value)
                {
                    base.SetProperty(ref this._configuredDependencies, value);
                }
            }
        }
    }