using Avalonia.Controls;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using Prism.Ioc;

namespace OneWare.Essentials.Helpers;

public class WindowHelper
{
    /// <summary>
    ///     Asks to save all files and returns true if ready to close or false if operation was canceled
    /// </summary>
    public static async Task<bool> HandleUnsavedFilesAsync(List<IExtendedDocument> unsavedFiles, Window dialogOwner)
    {
        if (unsavedFiles.Count > 0)
        {
            var status = await ContainerLocator.Container.Resolve<IWindowService>().ShowYesNoCancelAsync("Warning", "Keep unsaved changes?", MessageBoxIcon.Warning, dialogOwner);
                
            if (status == MessageBoxStatus.Yes)
            {
                for (var i = 0; i < unsavedFiles.Count; i++)
                    if (await unsavedFiles[i].SaveAsync())
                    {
                        unsavedFiles.RemoveAt(i);
                        i--;
                    }

                if (unsavedFiles.Count == 0) return true;

                var message = "Critical error saving some files: \n";
                foreach (var file in unsavedFiles) message += file.Title + ", ";
                message = message.Remove(message.Length - 2);

                var status2 = await ContainerLocator.Container.Resolve<IWindowService>()
                    .ShowYesNoCancelAsync("Error", $"{message}\nQuit anyways?", MessageBoxIcon.Error);

                if (status2 == MessageBoxStatus.Yes) return true;
            }
            else if (status == MessageBoxStatus.No)
            {
                //Quit and discard changes
                return true;
            }
            else
            {
                return false;
            }
        }

        return true;
    }
}