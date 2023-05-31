namespace OneWare.PackageManager.Models
{
    internal class ZipPackage : FilePackage
    {
        public override async Task<bool> PrepareUpdateAsync()
        {
            CancelSource = new CancellationTokenSource();

            var result = await DownloadManager.CheckForUpdatesAsync(new Version(InstalledVersion ?? "0.0.0.0"));

            try
            {
                if (result.CanUpdate)
                {
                    var pkg = await DownloadManager.DownloadAndExtractPackage(result.LastVersion,
                        CustomDestinationFolder ?? Path.Combine(Global.PackagesDirectory, PackageName), ParameterProgress,
                        CancelSource.Token);

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
    }
}