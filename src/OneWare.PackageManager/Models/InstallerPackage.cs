using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OneWare.PackageManager.Models
{
    internal class InstallerPackage : ZipInstallerPackage
    {
        public Action AfterInstall { get; set; }

        public override async Task<bool> PrepareUpdateAsync()
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

                    EntryPoint = pkg.RelativePath;
                    return true;
                }
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error("Preparing update failed", e);
            }

            return false;
        }

        public override void AfterDownloadAction()
        {
            base.AfterDownloadAction();
        }

        public override async Task LaunchInstallerAsync()
        {
            var packageDir = Path.Combine(Global.PackagesDirectory, PackageName);

            var executable = Path.Combine(packageDir, EntryPoint);

            if (File.Exists(executable))
            {
                ProcessStartInfo processInfo;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    processInfo = new ProcessStartInfo
                    {
                        FileName = executable,
                        Verb = "runas",
                        UseShellExecute = true
                    };
                }
                else
                {
                    Tools.ChmodFile(executable);
                    processInfo = new ProcessStartInfo //TODO LINUX
                    {
                        FileName = executable,
                        UseShellExecute = true
                    };
                }

                try
                {
                    using var process = new Process { StartInfo = processInfo };

                    process.Start();

                    await Task.Run(() => process.WaitForExit());
                    AfterInstall?.Invoke();
                }
                catch (Exception e)
                {
                    ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
                }
            }
            else
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error("Can't launch updater! Executable not found " + PackageName + " " + executable);
            }
        }

        public override Task RemoveAsync(bool checkAfterRemoval)
        {
            return base.RemoveAsync(checkAfterRemoval);
        }
    }
}