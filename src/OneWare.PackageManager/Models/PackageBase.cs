using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Prism.Ioc;
using ReactiveUI;
using VHDPlus.Shared;
using VHDPlus.Shared.Models;
using VHDPlus.Shared.Services;
using VHDPlus.Shared.ViewModels;

namespace OneWare.PackageManager.Models
{
    public enum UpdateStatus
    {
        Available,
        UpdateAvailable,
        Downloading,
        ReadyForInstall,
        Installing,
        Installed,
        Removing,
        Unavailable
    }

    public abstract class PackageBase : ViewModelBase
    {
        public Action AfterDownload { get; set; }

        public PackageBase()
        {
            UpdateButton = new ButtonModel
                { Header = "Update", Command = ReactiveCommand.CreateFromTask(StartUpdateAsync), BackgroundBrush = Application.Current.FindResource("HighlightBrush") as IBrush};
            RemoveButton = new ButtonModel
            {
                Header = "Remove", Command = ReactiveCommand.CreateFromTask<bool>(RemoveAsync), CommandParameter = true
            };
        }

        public abstract Task StartUpdateAsync();

        public virtual async Task<bool> CheckForUpdateAsync()
        {
            try
            {
                var v = new Version(InstalledVersion ?? "0.0.0.0");
                // Check for new version
                var result = await DownloadManager.CheckForUpdatesAsync(v);
                if (result != null && result.CanUpdate)
                {
                    NewVersionIdentifier = result.LastVersion;
                    ContainerLocator.Container.Resolve<ILogger>()?.Log(PackageHeader + " " + NewVersionIdentifier + " is available for download. ",
                        ConsoleColor.Cyan, true, Brushes.CornflowerBlue);

                    //Add button
                    NewVersion = result.LastVersion.ToString(); //= "Update from v" + Global.VersionCode + " to v" + ;
                    Buttons.Add(UpdateButton);

                    if (InstalledVersion == null)
                    {
                        UpdateStatus = UpdateStatus.Available;
                        UpdateButton.Header = "Install";
                        return false;
                    }

                    UpdateStatus = UpdateStatus.UpdateAvailable;
                    UpdateButton.Header = "Update";
                    return true;
                }

                if (InstalledVersion == null) UpdateStatus = UpdateStatus.Unavailable;

                return false;
            }
            catch (Exception e)
            {
                if (!(e is HttpRequestException)) ContainerLocator.Container.Resolve<ILogger>()?.Error("Failed checking for updates", e);
                return false;
            }
        }

        public abstract void Initialize(HttpClient httpClient);

        public virtual void Cancel()
        {
            if (CancelSource != null && !CancelSource.IsCancellationRequested)
                CancelSource.Cancel();

            UpdateStatus = UpdateStatus.Installed;
            Progress = 0;
            ProgressText = "";
        }

        public abstract Task RemoveAsync(bool checkAfterRemoval);

        #region Properties

        protected readonly ButtonModel UpdateButton, RemoveButton;
        protected IDownloadManager DownloadManager;
        protected Progress<double> ParameterProgress = new();
        protected Version NewVersionIdentifier;
        protected CancellationTokenSource CancelSource;
        
        public string PackageName { get; set; }

        public string DestinationFolder { get; set; }

        public string? EntryPoint { get; set; }
        
        public List<PackageBase> Requirements { get; set; }

        public ObservableCollection<ButtonModel> Buttons { get; } = new();

        private string _packageHeader;

        public string PackageHeader
        {
            get => _packageHeader;
            set => this.RaiseAndSetIfChanged(ref _packageHeader, value);
        }

        private string _packageDescription;

        public string PackageDescription
        {
            get => _packageDescription;
            set => this.RaiseAndSetIfChanged(ref _packageDescription, value);
        }

        private IImage _icon;

        public IImage Icon
        {
            get => _icon;
            set => this.RaiseAndSetIfChanged(ref _icon, value);
        }

        private string _newVersion;

        public string NewVersion
        {
            get => _newVersion;
            set => this.RaiseAndSetIfChanged(ref _newVersion, value);
        }

        private string? _installedVersion;

        public string? InstalledVersion
        {
            get => _installedVersion;
            set => this.RaiseAndSetIfChanged(ref _installedVersion, value);
        }

        private double _progress;

        public double Progress
        {
            get => _progress;
            set => this.RaiseAndSetIfChanged(ref _progress, value);
        }

        private UpdateStatus _updateStatus;

        public UpdateStatus UpdateStatus
        {
            get => _updateStatus;
            set
            {
                this.RaiseAndSetIfChanged(ref _updateStatus, value);
                this.RaisePropertyChanged(nameof(IsLoading));
            }
        }

        private string _progressText;

        public string ProgressText
        {
            get => _progressText;
            set => this.RaiseAndSetIfChanged(ref _progressText, value);
        }

        private string _license;

        public string License
        {
            get => _license;
            set => this.RaiseAndSetIfChanged(ref _license, value);
        }

        private string _licenseUrl;

        public string LicenseUrl
        {
            get => _licenseUrl;
            set => this.RaiseAndSetIfChanged(ref _licenseUrl, value);
        }

        private string _warningText;

        public string WarningText
        {
            get => _warningText;
            set => this.RaiseAndSetIfChanged(ref _warningText, value);
        }

        public bool IsLoading => UpdateStatus == UpdateStatus.Installing || UpdateStatus == UpdateStatus.Removing ||
                                 UpdateStatus == UpdateStatus.Downloading;

        #endregion
    }
}