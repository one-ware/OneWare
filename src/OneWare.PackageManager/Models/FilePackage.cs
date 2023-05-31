using System.Runtime.InteropServices;
using Avalonia.Controls;
using OneWare.PackageManager.Services;
using OneWare.PackageManager.Views;
using Prism.Ioc;
using VHDPlus.Shared;
using VHDPlus.Shared.Services;
using VHDPlus.Shared.ViewModels;
using VHDPlus.Shared.Views;

namespace OneWare.PackageManager.Models
{
    public class FilePackage : PackageBase
    {
        public FilePackage()
        {
            ParameterProgress.ProgressChanged += (o, i) =>
            {
                Progress = i;
                if (i >= 0.9) ProgressText = "Extracting...";
            };
        }
        
        public override void Initialize(HttpClient httpClient)
        {
            if (Environment.Is64BitProcess && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                DownloadManager =
                    new DownloadManager(
                        new WebPackageResolver(httpClient,
                            $"https://cdn.vhdplus.com/{PackageName}/win-x64.txt"), new ZipPackageExtractor(),
                        PackageName);
            else if (Environment.Is64BitProcess && RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                DownloadManager =
                    new DownloadManager(
                        new WebPackageResolver(httpClient,
                            $"https://cdn.vhdplus.com/{PackageName}/linux-x64.txt"), new ZipPackageExtractor(),
                        PackageName);
            else if (RuntimeInformation.OSArchitecture == Architecture.X64 &&
                     RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                DownloadManager =
                    new DownloadManager(
                        new WebPackageResolver(httpClient,
                            $"https://cdn.vhdplus.com/{PackageName}/osx-x64.txt"), new ZipPackageExtractor(),
                        PackageName);
            else if (RuntimeInformation.OSArchitecture == Architecture.Arm64 &&
                     RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                DownloadManager =
                    new DownloadManager(
                        new WebPackageResolver(httpClient,
                            $"https://cdn.vhdplus.com/{PackageName}/osx-arm64.txt"), new ZipPackageExtractor(),
                        PackageName);

            if (UpdateStatus == UpdateStatus.Installed || UpdateStatus == UpdateStatus.UpdateAvailable)
                Buttons.Add(RemoveButton);
        }

        public virtual async Task<bool> PrepareUpdateAsync()
        {
            CancelSource = new CancellationTokenSource();

            var result = await DownloadManager.CheckForUpdatesAsync(new Version(InstalledVersion ?? "0.0.0.0"));

            try
            {
                if (result.CanUpdate)
                {
                    var pkg = await DownloadManager.DownloadFile(result.LastVersion,
                        CustomDestinationFolder ?? Path.Combine(Global.PackagesDirectory, PackageName),
                        ParameterProgress, CancelSource.Token);
                    EntryPoint = Path.GetFileName(pkg.Url);

                    return true;
                }
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error("Preparing update failed", e);
            }

            return false;
        }

        public virtual void AfterDownloadAction()
        {
            AfterDownload?.Invoke();
        }

        public override async Task StartUpdateAsync()
        {
            if (Requirements != null && Requirements.Any(x => x.InstalledVersion == null))
            {
                var needed = Requirements.Where(x => x.InstalledVersion == null).ToList();
                
                MessageBoxWindow msg = new MessageBoxWindow("Package needed",
                    $"Package {PackageHeader} requires the following uninstalled packages:\n- {string.Join("\n- ", needed.Select(x => x.PackageHeader))}\nDo you want to install them now?",
                    MessageBoxMode.AllButtons, MessageBoxIcon.Info);

                await msg.ShowDialog(PackageManagerWindow.LastInstance);

                if (msg.BoxStatus is MessageBoxStatus.Yes)
                {
                    Progress = 0;
                    UpdateStatus = UpdateStatus.Installing;
                    ProgressText = "Waiting for dependencies...";
                    var requirementPackages = needed.Select(x => x.StartUpdateAsync());
                    await Task.WhenAll(requirementPackages);
                }
                else if (msg.BoxStatus is MessageBoxStatus.Canceled) return;
            }
            
            if (InstalledVersion != null) await RemoveAsync(false);
            Progress = 0;
            UpdateStatus = UpdateStatus.Installing;
            ProgressText = "Downloading...";
            Buttons.Remove(UpdateButton);
            var prepareSuccess = await PrepareUpdateAsync();
            if (prepareSuccess)
            {
                UpdateStatus = UpdateStatus.Installed;
                InstalledVersion = NewVersion;
                NewVersion = null;
                Buttons.Insert(0, RemoveButton);
                AfterDownloadAction();
                Global.PackageManagerViewModel.SavePackageDatabase();
            }
            else
            {
                Buttons.Add(UpdateButton);
                UpdateStatus = InstalledVersion == null ? UpdateStatus.Available : UpdateStatus.UpdateAvailable;
            }
        }

        public override Task RemoveAsync(bool checkAfterRemoval)
        {
            InstalledVersion = null;
            Buttons.Remove(RemoveButton);
            try
            {
                if (CustomDestinationFolder == null &&
                    Directory.Exists(Path.Combine(Global.PackagesDirectory, PackageName)))
                    Directory.Delete(Path.Combine(Global.PackagesDirectory, PackageName), true);
                else if (CustomDestinationFolder != null && EntryPoint != null)
                    File.Delete(Path.Combine(CustomDestinationFolder, EntryPoint));
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            }

            Global.PackageManagerViewModel.SavePackageDatabase();

            UpdateStatus = UpdateStatus.Available;
            if (checkAfterRemoval) _ = CheckForUpdateAsync();
            return Task.CompletedTask;
        }

        public string ParsePath(string input)
        {
            input = input.Replace("$version$", InstalledVersion);

            try
            {
                var v = new Version(InstalledVersion);
                input = input.Replace("$major$", v.Major.ToString());
                input = input.Replace("$minor$", v.Minor.ToString());
                input = input.Replace("$build$", v.Build.ToString());
                input = input.Replace("$revision$", v.Revision.ToString());
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            }

            return input;
        }
    }
}