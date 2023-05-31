using System.Diagnostics;
using System.Runtime.InteropServices;
using OneWare.PackageManager.Views;
using ReactiveUI;

namespace OneWare.PackageManager.Models
{
    internal class SetPathPackage : ZipPackage
    {
        /// <summary>
        ///     Set option in settings
        /// </summary>
        public Action<OldSettings, string> PathSetter { get; set; }
        
        public ButtonModel SetPathButton { get; }

        public SetPathPackage()
        {
            SetPathButton = new ButtonModel()
            {
                Header = "Set Path",
                Command = ReactiveCommand.Create(() =>
                {
                    if (string.IsNullOrEmpty(EntryPoint))
                    {
                        ContainerLocator.Container.Resolve<ILogger>()?.Error("Unable to set path. Please reinstall package", null, false, true, PackageManagerWindow.LastInstance);
                        return;
                    }
                    AfterDownloadAction();
                    Tools.ShowMessage($"Path for {PackageName} successfully set!",  PackageManagerWindow.LastInstance);
                })
            };
        }

        public override void Initialize(HttpClient httpClient)
        {
            base.Initialize(httpClient);
            if (InstalledVersion != null)
            {
                Buttons.Add(SetPathButton);
            }
        }

        public override void AfterDownloadAction()
        {
            if(!Buttons.Contains(SetPathButton)) Buttons.Add(SetPathButton);
            
            var path = Path.Combine(Global.PackagesDirectory, PackageName, EntryPoint);
            if (File.Exists(path))
                Tools.ChmodFile(path);
            else
                ContainerLocator.Container.Resolve<ILogger>()?.Error("Downloaded executable not found! Expected location: " + path, null, true, true);

            PathSetter?.Invoke(Global.Options, path);
            
            AfterDownload?.Invoke();

            Global.SaveSettings();
        }

        public override Task RemoveAsync(bool checkAfterRemoval)
        {
            Progress = 0;
            ProgressText = "Stopping server";
            UpdateStatus = UpdateStatus.Removing;
            PathSetter?.Invoke(Global.Options, "");

            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) //not working on osx
                    foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(EntryPoint)))
                        process.Kill();
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            }

            Buttons.Remove(SetPathButton);

            Global.SaveSettings();
            Progress = 1;
            return base.RemoveAsync(checkAfterRemoval);
        }
    }
}