namespace OneWare.PackageManager.Models
{
    internal class LanguageServerPackage<T> : SetPathPackage
    {
        /// <summary>
        ///     Set option in settings
        /// </summary>
        public Action<OldSettings, bool> LspActivator { get; set; }
        public Func<bool> LspValid { get; set; }

        public override void AfterDownloadAction()
        {
            base.AfterDownloadAction();

            var valid = LspValid?.Invoke() ?? false;
            LspActivator?.Invoke(Global.Options, valid);
            Global.SaveSettings();
        }

        public override async Task RemoveAsync(bool checkAfterRemoval)
        {
            Progress = 0;
            ProgressText = "Stopping server";
            UpdateStatus = UpdateStatus.Removing;

            //Stop language server
            await Task.WhenAll(LanguageServiceManager.GetServices<T>().Cast<LanguageServiceBase>().Select(x => x.DeactivateAsync()));
            await Task.Delay(100);
            
            LspActivator?.Invoke(Global.Options, false);
            
            await base.RemoveAsync(checkAfterRemoval);
        }
    }
}