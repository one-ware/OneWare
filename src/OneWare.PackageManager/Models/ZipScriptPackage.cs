using Avalonia.Controls;
using OneWare.PackageManager.Views;
using Prism.Ioc;
using VHDPlus.Shared;
using VHDPlus.Shared.Services;
using VHDPlus.Shared.ViewModels;
using VHDPlus.Shared.Views;

namespace OneWare.PackageManager.Models
{
    internal class ZipScriptPackage : ZipInstallerPackage
    {
        public string ScriptPath { get; set; }

        public override async Task LaunchInstallerAsync()
        {
            var mssg = new MessageBoxWindow("Warning",
                "This installation will need elevated privileges! VHDPlus IDE will try to execute the installation script in an external terminal. Do you want to continue?");
            await mssg.ShowDialog(PackageManagerWindow.LastInstance as Window ?? App.MainWindow);
            if (mssg.BoxStatus == MessageBoxStatus.Yes)
                try
                {
                    var script = await File.ReadAllTextAsync(ScriptPath);
                    script = ParseScript(script);
                    var parsedScriptPath = Path.Combine(Global.PackagesDirectory, PackageName, "installScript.sh");
                    await File.WriteAllTextAsync(parsedScriptPath, script);
                    Tools.ExecScriptInTerminal(parsedScriptPath, true, "Arrow USB Driver installation");
                }
                catch (Exception e)
                {
                    ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
                }
        }

        public string ParseScript(string script)
        {
            return script
                .Replace("$version$", InstalledVersion)
                .Replace("$quartuspath$", Global.Options.QuartusPath)
                .Replace("$packagepath$", Path.Combine(Global.PackagesDirectory, PackageName));
        }
    }
}