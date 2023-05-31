using System.Runtime.InteropServices;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using OneWare.PackageManager.Services;
using Prism.Ioc;
using ReactiveUI;
using VHDPlus.Shared;
using VHDPlus.Shared.Converters;
using VHDPlus.Shared.Services;

namespace OneWare.PackageManager.Models
{
    public class IdeUpdateManagerModel : PackageBase
    {
        private readonly ButtonModel _installButton, _changeLogButton;

        public IdeUpdateManagerModel()
        {
            _installButton = new ButtonModel
                { Header = "Install", Command = ReactiveCommand.Create(Install), BackgroundBrush = Brushes.Green };
            _changeLogButton = new ButtonModel
                { Header = "Changelog", Command = ReactiveCommand.Create(ShowChangeLog)};
            PackageHeader = "VHDPlus IDE";
            PackageName = "vhdplus";
            PackageDescription = "The FPGA Programming Revolution";
            InstalledVersion = Global.VersionCode;
            Icon = (Bitmap)PathToBitmapConverter.Instance.Convert("avares://VHDPlus/Assets/VHDP-Logo.ico",
                typeof(IBitmap), null, null);
            Progress = 0;
            UpdateStatus = UpdateStatus.Installed;
            License = "Freeware";
            LicenseUrl = "https://www.vhdplus.com";

            ParameterProgress.ProgressChanged += (o, i) =>
            {
                Progress = i;
                if (i >= 0.9) ProgressText = "Extracting...";
            };
            
            Buttons.Add(_changeLogButton);
        }

        public override async Task<bool> CheckForUpdateAsync()
        {
            var status = await base.CheckForUpdateAsync();
            if (UpdateStatus == UpdateStatus.UpdateAvailable) UpdateButton.Header = "Download";
            return status;
        }

        public override async Task StartUpdateAsync()
        {
            Progress = 0;
            UpdateStatus = UpdateStatus.Installing;
            ProgressText = "Downloading...";
            Buttons.Remove(UpdateButton);
            var prepareSuccess = await PrepareUpdateAsync();
            if (prepareSuccess)
            {
                ProgressText = "";
                WarningText =
                    "Please save your work and press install to continue";
                UpdateStatus = UpdateStatus.ReadyForInstall;
                Buttons.Add(_installButton);
            }
            else
            {
                Buttons.Add(UpdateButton);
                UpdateStatus = UpdateStatus.UpdateAvailable;
            }
        }

        public void Install()
        {
            InstallUpdate();
        }
        
        public void ShowChangeLog()
        {
            var w = new ChangelogWindow();
            w.Show();
        }

        public override void Cancel()
        {
            base.Cancel();
        }

        public override Task RemoveAsync(bool checkAfterRemoval)
        {
            //not supported :O
            return Task.CompletedTask;
        }

        #region Update

        public override void Initialize(HttpClient httpClient)
        {
            if (Environment.Is64BitProcess && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                DownloadManager =
                    new DownloadManager(
                        new WebPackageResolver(httpClient, "https://cdn.vhdplus.com/vhdpluside/win-x64.txt"),
                        new ZipPackageExtractor(), "VHDPlus");
            else if (Environment.Is64BitProcess && RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                DownloadManager =
                    new DownloadManager(
                        new WebPackageResolver(httpClient, "https://cdn.vhdplus.com/vhdpluside/linux-x64.txt"),
                        new ZipPackageExtractor(), "VHDPlus");
            else if (RuntimeInformation.OSArchitecture == Architecture.X64 &&
                     RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                DownloadManager =
                    new DownloadManager(
                        new WebPackageResolver(httpClient, "https://cdn.vhdplus.com/vhdpluside/osx-x64.txt"),
                        new ZipPackageExtractor(), "VHDPlus");
            else if (RuntimeInformation.OSArchitecture == Architecture.Arm64 &&
                     RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                DownloadManager =
                    new DownloadManager(
                        new WebPackageResolver(httpClient, "https://cdn.vhdplus.com/vhdpluside/osx-arm64.txt"),
                        new ZipPackageExtractor(), "VHDPlus");
        }

        public async Task<bool> PrepareUpdateAsync()
        {
            CancelSource = new CancellationTokenSource();

            var result = await DownloadManager.CheckForUpdatesAsync();

            try
            {
                if (result.CanUpdate)
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        Tools.OpenHyperLink("https://vhdplus.com/docs/getstarted/#install-vhdplus-ide");
                        return false;
                    }
                    else
                        await DownloadManager.PrepareUpdateAsync(result.LastVersion, ParameterProgress, CancelSource.Token);
                    return true;
                }
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error("Preparing update failed", e);
            }

            return false;
        }

        public void InstallUpdate()
        {
            if (!DownloadManager.IsUpdatePrepared(NewVersionIdentifier))
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error("Can't launch updater. Update not prepared!");
                return;
            }

            App.LaunchUpdaterOnExit = true;
            App.Exit();
        }

        public void LaunchUpdater()
        {
            DownloadManager.LaunchUpdater(NewVersionIdentifier);
        }

        #endregion
    }
}