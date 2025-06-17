using Avalonia.Controls;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OneWare.Essentials.Helpers
{
    public class WindowHelper
    {
        private readonly IWindowService _windowService;

        public WindowHelper(IWindowService windowService)
        {
            _windowService = windowService;
        }

        /// <summary>
        ///     Asks to save all files and returns true if ready to close or false if operation was canceled.
        /// </summary>
        public async Task<bool> HandleUnsavedFilesAsync(List<IExtendedDocument> unsavedFiles, Window dialogOwner)
        {
            if (unsavedFiles.Count > 0)
            {
                var status = await _windowService.ShowYesNoCancelAsync(
                    "Warning",
                    "Keep unsaved changes?",
                    MessageBoxIcon.Warning,
                    dialogOwner
                );

                if (status == MessageBoxStatus.Yes)
                {
                    for (int i = 0; i < unsavedFiles.Count; i++)
                    {
                        if (await unsavedFiles[i].SaveAsync())
                        {
                            unsavedFiles.RemoveAt(i);
                            i--;
                        }
                    }

                    if (unsavedFiles.Count == 0)
                        return true;

                    var message = "Critical error saving some files:\n";
                    foreach (var file in unsavedFiles)
                        message += file.Title + ", ";

                    message = message[..^2]; // Remove trailing ", "

                    var status2 = await _windowService.ShowYesNoCancelAsync(
                        "Error",
                        $"{message}\nQuit anyways?",
                        MessageBoxIcon.Error,
                        dialogOwner
                    );

                    if (status2 == MessageBoxStatus.Yes)
                        return true;
                }
                else if (status == MessageBoxStatus.No)
                {
                    // Quit and discard changes
                    return true;
                }
                else
                {
                    // Cancel
                    return false;
                }
            }

            return true;
        }
    }
}
