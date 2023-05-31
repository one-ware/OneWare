using Prism.Ioc;
using VHDPlus.Shared;
using VHDPlus.Shared.Services;

namespace OneWare.PackageManager.Models
{
    internal class LibraryPackage : ZipPackage
    {
        public override void AfterDownloadAction()
        {
            base.AfterDownloadAction();
            MainDock.Libraries.Refresh();
        }

        public override Task RemoveAsync(bool checkAfterRemoval)
        {
            InstalledVersion = null;
            Buttons.Remove(RemoveButton);
            try
            {
                Directory.Delete(Path.Combine(DestinationFolder, EntryPoint), true);
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            }

            Global.PackageManagerViewModel.SavePackageDatabase();

            UpdateStatus = UpdateStatus.Available;
            if (checkAfterRemoval) _ = CheckForUpdateAsync();
            MainDock.Libraries.Refresh();
            return Task.CompletedTask;
        }
    }
}