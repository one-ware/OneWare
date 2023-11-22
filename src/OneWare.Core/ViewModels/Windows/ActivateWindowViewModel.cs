using CommunityToolkit.Mvvm.ComponentModel;
using DeviceId;
using OneWare.SDK.Services;

namespace OneWare.Core.ViewModels.Windows
{
    internal class ActivateWindowViewModel : ObservableObject
    {
        private ILogger _logger;
        private IPaths _paths;
        
        public string? Key { get; set; }

        private string LicensePath => Path.Combine(_paths.AppDataDirectory, ".license");

        private static string DeviceId => new DeviceIdBuilder()
            .AddMachineName()
            .AddMacAddress()
            .ToString();
        
        public ActivateWindowViewModel(ILogger logger, IPaths paths)
        {
            _logger = logger;
            _paths = paths;
        }

        public Task ActivateAsync()
        {
            // var request = ApiInterface.LicenseRequest(LicenseRequestType.Validate, Key);
            // if (request.APIReturnedSuccess && request.Licences.Count == 1)
            // {
            //     var license = request.Licences.First();
            //     
            //     var msg = new MessageBoxWindow("Activate License",
            //         $"Are you sure you want to activate OneWare.Core Pro using this license?" +
            //          $"\nRemaining Activations: {license.RemainingActivations} / {license.TimesActivatedMax}", MessageBoxMode.NoCancel);
            //
            //     await msg.ShowDialog(ActivateWindow.LastInstance);
            //
            //     if (msg.BoxStatus is MessageBoxStatus.Yes)
            //     {
            //         var activateRequest = ApiInterface.LicenseRequest(LicenseRequestType.Activate, Key);
            //
            //         if (!activateRequest.APIReturnedSuccess)
            //         {
            //             ContainerLocator.Container.Resolve<ILogger>()?.Error("Error activating license");
            //             return;
            //         }
            //         
            //         license = activateRequest.Licences.First();
            //         
            //
            //         _logger.Log("OneWare.Core Pro IDE successfully activated!" +
            //                     $"\nValid until: {(license.ExpiresAt.HasValue ? license.ExpiresAt.Value.ToShortDateString() : "Unknown")}", ConsoleColor.Green, true, Brushes.Green);
            //
            //         //Create license file
            //         await File.WriteAllTextAsync(LicensePath, $"{DeviceId}|{Key}");
            //         
            //         if(ActivateWindow.LastInstance != null) Close(ActivateWindow.LastInstance);
            //     }
            // }
            // else
            // {
            //     ContainerLocator.Container.Resolve<ILogger>()?.Error("Invalid License");
            // }
            return Task.CompletedTask;
        }
        
        public Task<bool> VerifyAsync()
        {
            // var licenseFile = await File.ReadAllTextAsync(LicensePath);
            //
            // var parts = licenseFile.Split("|");
            //
            // if (parts.Length == 2 && parts[0] == DeviceId)
            // {
            //     var request = ApiInterface.LicenseRequest(LicenseRequestType.Validate, parts[1]);
            //     return request.APIReturnedSuccess;
            // }
            //
            // return false;

            return Task.FromResult(true);
        }
    }
}