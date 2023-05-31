using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using OneWare.PackageManager.Views;
using ReactiveUI;
using VHDPlus.Shared.Views;

namespace OneWare.PackageManager.Models
{
    internal class ZipInstallerPackage : ZipPackage
    {
        protected ButtonModel LaunchInstallerButton;

        public ZipInstallerPackage()
        {
            LaunchInstallerButton = new ButtonModel
                { Header = "Launch Installer", Command = ReactiveCommand.Create(LaunchInstallerAsync)};
        }


        public override void Initialize(HttpClient httpClient)
        {
            base.Initialize(httpClient);
            if (UpdateStatus == UpdateStatus.Installed) Buttons.Add(LaunchInstallerButton);
        }

        public override void AfterDownloadAction()
        {
            base.AfterDownloadAction();

            Buttons.Add(LaunchInstallerButton);

            _ = LaunchInstallerAsync();
        }

        public virtual async Task LaunchInstallerAsync()
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(Global.PackagesDirectory, PackageName, EntryPoint),
                Verb = "runas"
            };

            try
            {
                using var process = new Process { StartInfo = processInfo };

                process.Start();
                await Task.Run(() => process.WaitForExit());
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            }
        }

        public override async Task RemoveAsync(bool checkAfterRemoval)
        {
            var msg = new MessageBoxWindow("Warning",
                "This will only remove the installer for this package. You will have to uninstall the package manually. Do you want to continue deleting the installer?",
                MessageBoxMode.NoCancel);
            await msg.ShowDialog(PackageManagerWindow.LastInstance as Window ?? App.MainWindow);

            if (msg.BoxStatus == MessageBoxStatus.Yes)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    try
                    {
                        var info = new ProcessStartInfo { FileName = "appwiz.cpl", UseShellExecute = true };
                        using var pr = new Process { StartInfo = info };
                        pr.Start();
                    }
                    catch (Exception e)
                    {
                        ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
                    }

                await base.RemoveAsync(checkAfterRemoval);
                Buttons.Remove(LaunchInstallerButton);
            }
        }
    }
}